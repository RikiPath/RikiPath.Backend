using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Helpers
{
    /// <summary>
    /// Records learner journey milestones that have no timestamp of their own on the learner row (learning goal
    /// confirmed, standard roadmap opened). Stored as a read, history-only <see cref="Notification"/> whose unique
    /// IdempotencyKey (<c>journey:{kind}:{userId}:{goalVersion}</c>) makes the write idempotent — no schema change.
    /// Learning Moments derives the celebration from these rows; the client never declares a milestone itself.
    /// The caller saves the unit of work, so the event commits atomically with the business change.
    /// </summary>
    public static class JourneyEventRecorder
    {
        public const string NotificationType = "JourneyEvent";
        public const string GoalConfirmed = "goal-confirmed";
        public const string StandardRoadmapReady = "standard-roadmap";

        public static string Key(string kind, int userId, int goalVersion) =>
            $"journey:{kind}:{userId}:{goalVersion}";

        public static string Prefix(string kind, int userId) => $"journey:{kind}:{userId}:";

        /// <summary>Adds the event row (not saved). Returns false when the same event was already recorded.</summary>
        public static async Task<bool> RecordAsync(
            IUnitOfWork unitOfWork,
            int userId,
            string kind,
            int goalVersion,
            string title,
            string message,
            string linkUrl,
            CancellationToken cancellationToken)
        {
            var key = Key(kind, userId, goalVersion);
            if (await unitOfWork.Notifications.AnyAsync(n => n.IdempotencyKey == key, cancellationToken))
                return false;

            await unitOfWork.Notifications.AddAsync(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = NotificationType,
                // History only: the celebration dialog is the announcement, so no unread badge or push.
                IsRead = true,
                LinkUrl = linkUrl,
                IdempotencyKey = key,
                CreatedDate = DateTime.UtcNow,
            }, cancellationToken);
            return true;
        }
    }
}
