using RikiPath.Application.IServices;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Collections.Concurrent;

namespace RikiPath.Application.Services
{
    public sealed class MentorMeetingAccessService(IUnitOfWork unitOfWork) : IMentorMeetingAccessService
    {
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
            var slot = await unitOfWork.MentorAvailabilities.GetActiveSlotByRoomIdAsync(roomId, cancellationToken);
            if (slot is null)
                return null;

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

            // Mỗi booking hợp lệ (Paid + Assigned/Accepted) = 1 learner đã đặt chung slot này.
            // GroupBy phòng hờ trường hợp hiếm: cùng 1 learner lỡ có 2 booking cho cùng 1 slot.
            var learners = (slot.MentorBookings ?? [])
                .Select(b => ToLearner(b.UserSubscription))
                .GroupBy(l => l.UserId)
                .Select(g => g.First())
                .ToList();

            return new MeetingAccessInfoResponse
            {
                Mentor = mentor,
                Learners = learners
            };
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