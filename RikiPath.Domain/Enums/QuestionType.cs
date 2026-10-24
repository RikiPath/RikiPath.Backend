namespace RikiPath.Domain.Enums
{
    public enum QuestionType
    {
        MultipleChoice = 1,       // Trắc nghiệm thông thường (A, B, C, D)
        SentenceOrdering = 2,     // Dạng sắp xếp câu có dấu sao (★)
        ReadingComprehension = 3, // Câu hỏi đọc hiểu (gắn với đoạn văn)
        Listening = 4             // Câu hỏi nghe hiểu (có file audio)
    }
}