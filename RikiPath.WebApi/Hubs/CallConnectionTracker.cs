using System.Collections.Concurrent;

namespace RikiPath.WebApi.Hubs
{
    public interface ICallConnectionTracker
    {
        void Track(string connectionId, int consultationRequestId, int userId);
        (int MentorBookingId, int UserId)? Untrack(string connectionId);
        int GetParticipantCount(int consultationRequestId);
    }

    public class CallConnectionTracker : ICallConnectionTracker
    {
        private readonly ConcurrentDictionary<string, (int MentorBookingId, int UserId)> _byConnection = new();
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _byCall = new();

        public void Track(string connectionId, int consultationRequestId, int userId)
        {
            _byConnection[connectionId] = (consultationRequestId, userId);
            var participants = _byCall.GetOrAdd(consultationRequestId, _ => new ConcurrentDictionary<string, byte>());
            participants[connectionId] = 0;
        }

        public (int MentorBookingId, int UserId)? Untrack(string connectionId)
        {
            if (!_byConnection.TryRemove(connectionId, out var info))
                return null;

            if (_byCall.TryGetValue(info.MentorBookingId, out var participants))
            {
                participants.TryRemove(connectionId, out _);
                if (participants.IsEmpty)
                    _byCall.TryRemove(info.MentorBookingId, out _);
            }

            return info;
        }

        public int GetParticipantCount(int consultationRequestId)
            => _byCall.TryGetValue(consultationRequestId, out var participants) ? participants.Count : 0;
    }
}
