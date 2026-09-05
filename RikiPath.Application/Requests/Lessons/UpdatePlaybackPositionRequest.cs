namespace RikiPath.Application.Requests.Lessons
{
    public class UpdatePlaybackPositionRequest
    {
        public int PositionSeconds { get; set; }
        public int DurationSeconds { get; set; }
    }
}
