using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Collections.Concurrent;

namespace RikiPath.Application.Services
{
    public sealed class MentorMeetingAccessService : IMentorMeetingAccessService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly ICohortEnrollmentDeadlineService? enrollmentDeadlines;

        public MentorMeetingAccessService(
            IUnitOfWork unitOfWork,
            ICohortEnrollmentDeadlineService? enrollmentDeadlines = null)
        {
            this.unitOfWork = unitOfWork;
            this.enrollmentDeadlines = enrollmentDeadlines;
        }

        private static readonly TimeSpan EmptyChatRoomGrace = TimeSpan.FromMinutes(10);
        private const int MaxChatMessagesPerRoom = 200;
        private const int MaxChatTextLength = 2000;

        private sealed class RoomChat
        {
            public readonly object Sync = new();
            public readonly Queue<MeetingChatMessageResponse> Messages = new();
            public int Participants;
            public DateTime? EmptySinceUtc;
        }

        private static readonly ConcurrentDictionary<Guid, RoomChat> ChatRooms = new();

        public MeetingChatMessageResponse? AddChatMessage(Guid roomId, int senderUserId, string senderName, string role, string? text)
        {
            var normalized = text?.Trim();
            if (string.IsNullOrEmpty(normalized)) return null;

            if (normalized.Length > MaxChatTextLength)
            {
                // Không cắt giữa 1 cặp surrogate (emoji): chuỗi UTF-16 lỗi làm SignalR ném exception khi serialize.
                var cut = char.IsHighSurrogate(normalized[MaxChatTextLength - 1]) ? MaxChatTextLength - 1 : MaxChatTextLength;
                normalized = normalized[..cut];
            }

            var message = new MeetingChatMessageResponse
            {
                Id = Guid.NewGuid().ToString("N"),
                SenderUserId = senderUserId,
                Sender = senderName,
                Role = role,
                Text = normalized,
                SentAt = DateTime.UtcNow
            };

            var room = ChatRooms.GetOrAdd(roomId, _ => new RoomChat());
            lock (room.Sync)
            {
                room.Messages.Enqueue(message);
                while (room.Messages.Count > MaxChatMessagesPerRoom)
                    room.Messages.Dequeue();
            }

            return message;
        }
        public IReadOnlyList<MeetingChatMessageResponse> GetChatHistory(Guid roomId)
        {
            if (!ChatRooms.TryGetValue(roomId, out var room)) return [];
            lock (room.Sync)
                return room.Messages.ToList();
        }
        public async Task<MeetingAccessInfoResponse?> GetMeetingAsync(Guid roomId, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var slot = await unitOfWork.MentorAvailabilities.GetActiveSlotByRoomIdAsync(roomId, cancellationToken);
            if (slot is null || slot.IsDeleted || !slot.IsApproved || slot.RejectedAt != null
                || nowUtc < slot.StartTime.AddMinutes(-15) || nowUtc >= slot.EndTime.AddMinutes(30))
                return null;

            var cohort = await unitOfWork.StudyCohorts.Query().AsNoTracking()
                .Include(x => x.GroupPolicy)
                .Include(x => x.Enrollments)
                .Include(x => x.Sessions)
                    .ThenInclude(s => s.Availability)
                        .ThenInclude(a => a.GroupPolicy)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Sessions.Any(s => s.AvailabilityId == slot.Id), cancellationToken);

            if (cohort is not null)
            {
                var cohortStatus = cohort.Status?.Trim() ?? string.Empty;
                if (string.Equals(cohortStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cohortStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cohortStatus, "Draft", StringComparison.OrdinalIgnoreCase))
                    return null;

                var confirmedEnrolledCount = cohort.Enrollments.Count(c =>
                    c.PaymentStatus is "Paid" or "PartiallyRefunded"
                    && c.Status is not ("PaymentReview" or "Cancelled" or "Transferred" or "PendingPayment" or "PaymentExpired" or "PaymentFailed"));

                var effectivePolicy = cohort.GroupPolicy
                    ?? slot.GroupPolicy
                    ?? cohort.Sessions.Select(s => s.Availability.GroupPolicy).FirstOrDefault(p => p is not null);
                var firstSession = cohort.Sessions
                    .Where(s => s.Availability != null && s.Availability.IsApproved && !s.Availability.IsDeleted && s.Availability.RejectedAt == null)
                    .OrderBy(s => s.Availability.StartTime)
                    .FirstOrDefault();
                var enrollmentCutoffUtc = cohort.EnrollmentClosesAtUtc
                    ?? (firstSession is null ? cohort.StartsAt : firstSession.Availability.StartTime);

                // Khi đã qua hạn tuyển sinh mà lớp vẫn trống -> xử lý đóng/hủy tức thời trước khi cho vào phòng.
                if (confirmedEnrolledCount == 0 && enrollmentCutoffUtc <= nowUtc && enrollmentDeadlines is not null)
                {
                    await enrollmentDeadlines.ProcessCohortIfPastCutoffAsync(cohort.Id, cancellationToken);
                    return null;
                }

                if (confirmedEnrolledCount < 1)
                    return null;

                if (!string.Equals(cohortStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                    && cohort.EnrollmentClosedAtUtc is null
                    && enrollmentCutoffUtc > nowUtc)
                    return null;
            }

            // Giữ đúng guard cũ: chỉ coi là Mentor hợp lệ nếu Role thực sự là Mentor
            // (phòng vệ trường hợp dữ liệu Mentor bị sai/thiếu).
            var mentorIsValid = slot.Mentor?.Role == Role.Mentor;
            var mentor = new MeetingParticipantResponse
            {
                UserId = mentorIsValid ? slot.MentorId : 0,
                Name = mentorIsValid
                    ? $"{slot.Mentor!.FirstName} {slot.Mentor.LastName}".Trim()
                    : "Mentor",
                IsMentor = true
            };

            var courseLearners = await unitOfWork.StudyCourses.Query()
                .Where(c => (c.PaymentStatus == "Paid" || c.PaymentStatus == "PartiallyRefunded")
                    && (c.Status == "Active" || c.Status == "Ready")
                    && c.Sessions.Any(s => s.AvailabilityId == slot.Id && s.Status == "Scheduled"))
                .Select(c => new MeetingParticipantResponse { UserId = c.LearnerId,
                    Name = (c.Learner.FirstName + " " + c.Learner.LastName).Trim(), IsMentor = false }).ToListAsync(cancellationToken);
            if (cohort is null && courseLearners.Count == 0)
                return null;
            var learners = courseLearners.DistinctBy(x => x.UserId).ToList();
            return new MeetingAccessInfoResponse
            {
                Capacity = slot.MaxLearners,
                Mentor = mentor,
                Learners = learners
            };
        }

        public Task RecordAttendanceAsync(Guid roomId, int mentorId, int[] learnerIds,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>Hub gọi khi có 1 connection mới vào phòng (đếm người để biết khi nào phòng trống).</summary>
        public void TrackChatParticipantJoined(Guid roomId)
        {
            var room = ChatRooms.GetOrAdd(roomId, _ => new RoomChat());
            lock (room.Sync)
            {
                // Phòng đã trống quá lâu rồi mới có người vào = buổi họp mới -> không hiện lại chat của buổi cũ.
                if (room.Participants == 0 && room.EmptySinceUtc is { } since && DateTime.UtcNow - since > EmptyChatRoomGrace)
                    room.Messages.Clear();

                room.Participants++;
                room.EmptySinceUtc = null;
            }
            PurgeIdleChatRooms();
        }

        private static void PurgeIdleChatRooms()
        {
            var now = DateTime.UtcNow;
            foreach (var (roomId, room) in ChatRooms)
            {
                bool expired;
                lock (room.Sync)
                    expired = room.Participants == 0 && room.EmptySinceUtc is { } since && now - since > EmptyChatRoomGrace;

                if (expired)
                    ChatRooms.TryRemove(new KeyValuePair<Guid, RoomChat>(roomId, room));
            }
        }

        private static MeetingParticipantResponse ToLearner(UserSubscription subscription)
        {
            var account = subscription.UserAccount;
            return new MeetingParticipantResponse
            {
                UserId = subscription.UserId,
                Name = account is null
                    ? $"User #{subscription.UserId}"
                    : $"{account.FirstName} {account.LastName}".Trim(),
                IsMentor = false
            };
        }
    }
}
