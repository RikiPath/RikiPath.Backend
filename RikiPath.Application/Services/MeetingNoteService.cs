using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services
{
    public sealed class MeetingNoteService(IUnitOfWork unitOfWork) : IMeetingNoteService
    {
        private const int MaxContentLength = 20_000;

        public async Task<MeetingNoteResponse> UpsertAsync(
            Guid roomId,
            int mentorBookingId,
            int authorUserId,
            string authorName,
            string authorRole,
            string? content,
            CancellationToken ct)
        {
            var normalized = Normalize(content);

            var entity = await unitOfWork.Notes.GetByRoomAndAuthorAsync(roomId, authorUserId, ct);

            var now = DateTime.UtcNow;
            if (entity is null)
            {
                entity = new Note
                {
                    MentorBookingId = mentorBookingId,
                    UserId = authorUserId,
                    AuthorRole = authorRole,
                    Content = normalized,
                };
                await unitOfWork.Notes.AddAsync(entity);
                await unitOfWork.SaveChangesAsync(ct);
            }
            else if (entity.Content != normalized)
            {
                entity.Content = normalized;
                unitOfWork.Notes.Update(entity);
                await unitOfWork.SaveChangesAsync(ct);
            }

            return new MeetingNoteResponse
            {
                UserId = authorUserId,
                Name = authorName,
                Role = authorRole,
                Content = entity.Content,
            };
        }
        public Task<IReadOnlyList<MeetingNoteResponse>> GetByRoomAsync(Guid roomId, CancellationToken ct)
        => unitOfWork.Notes.GetByRoomAsync(roomId, ct);

        public Task<IReadOnlyList<MeetingNoteResponse>> GetByBookingAsync(int mentorBookingId, CancellationToken ct)
         => unitOfWork.Notes.GetByBookingAsync(mentorBookingId, ct);

        private static string Normalize(string? content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            if (content.Length <= MaxContentLength) return content;

            // Không cắt giữa 1 cặp surrogate (emoji): chuỗi UTF-16 lỗi làm DB driver / serializer ném exception.
            var cut = char.IsHighSurrogate(content[MaxContentLength - 1]) ? MaxContentLength - 1 : MaxContentLength;
            return content[..cut];
        }
    }
}