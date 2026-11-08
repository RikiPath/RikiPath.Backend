using RikiPath.Application.Requests.ContentAuthoring;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.ContentAuthoring;

namespace RikiPath.Application.IServices
{
    /// <summary>
    /// AI hỗ trợ content author soạn nội dung. Luồng 2 bước cho mọi loại nội dung:
    /// 1) Suggest: AI đề xuất + hệ thống tự kiểm tra/dò trùng, trả về để author xem/chỉnh (CHƯA lưu gì vào DB).
    /// 2) Confirm: author xác nhận bản cuối, hệ thống ghi vào ContentApprovalRequest (Draft) để admin duyệt.
    /// </summary>
    public interface IContentAuthoringService
    {
        // ------------------------------- Kanji -------------------------------
        Task<ApiResponse<SuggestKanjiResponse>> SuggestKanjiAsync(
            SuggestKanjiRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<SubmitKanjiApprovalResponse>> ConfirmKanjiSuggestionsAsync(
            ConfirmKanjiSuggestionsRequest request, CancellationToken cancellationToken);

        // ------------------------------- Vocabulary -------------------------------
        Task<ApiResponse<SuggestVocabularyResponse>> SuggestVocabularyAsync(
            SuggestVocabularyRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmVocabularySuggestionsAsync(
            ConfirmVocabularySuggestionsRequest request, CancellationToken cancellationToken);

        // ------------------------------- GrammarPattern -------------------------------
        Task<ApiResponse<SuggestGrammarResponse>> SuggestGrammarAsync(
            SuggestGrammarRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmGrammarSuggestionsAsync(
            ConfirmGrammarSuggestionsRequest request, CancellationToken cancellationToken);

        // ------------------------------- Lesson -------------------------------
        /// <summary>Gợi ý tiêu đề/mô tả + danh sách Kanji/Vocabulary/Grammar ĐÃ CÓ để gắn vào bài học. Video do author tự upload.</summary>
        Task<ApiResponse<SuggestLessonResponse>> SuggestLessonAsync(
            SuggestLessonRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmLessonSuggestionAsync(
            ConfirmLessonSuggestionRequest request, CancellationToken cancellationToken);

        // ------------------------------- PracticeExercise -------------------------------
        Task<ApiResponse<SuggestPracticeExerciseResponse>> SuggestPracticeExerciseAsync(
            SuggestPracticeExerciseRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmPracticeExerciseSuggestionAsync(
            ConfirmPracticeExerciseRequest request, CancellationToken cancellationToken);

        // ------------------------------- MockTest -------------------------------
        Task<ApiResponse<SuggestMockTestResponse>> SuggestMockTestAsync(
            SuggestMockTestRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmMockTestSuggestionAsync(
            ConfirmMockTestRequest request, CancellationToken cancellationToken);

        // ------------------------------- Scan -------------------------------
        /// <summary>
        /// Quét nội dung Draft của author hiện tại. Kanji/Vocabulary/Grammar: AI đối chiếu kiến thức chuẩn.
        /// PracticeExercise/MockTest: kiểm tra cấu trúc bằng code + AI giải lại đề (không thấy đáp án) để bắt đáp án sai.
        /// KHÔNG tạo/sửa/xóa gì trong DB — chỉ trả về báo cáo.
        /// </summary>
        Task<ApiResponse<ScanContentAccuracyResponse>> ScanMyDraftContentAsync(
            ScanContentAccuracyRequest request, CancellationToken cancellationToken);
    }
}