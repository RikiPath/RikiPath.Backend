namespace RikiPath.Application.Responses.LearningPaths
{
    public class DailyScheduleResponse
    {
        public int Day { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Focus { get; set; } = string.Empty;
        public List<string> Lessons { get; set; } = new();

        // Các thuộc tính có thể null, chỉ trả về khi người dùng chọn tương ứng
        public List<string>? Vocabularies { get; set; }
        public List<string>? Kanjis { get; set; }
        public List<string>? Grammars { get; set; }
    }
}
