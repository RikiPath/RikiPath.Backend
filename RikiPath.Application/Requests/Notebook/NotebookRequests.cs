using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RikiPath.Application.Requests.Notebook
{
    public class CreateNotebookFolderRequest
    {
        [Required(ErrorMessage = "Tên thư mục không được để trống.")]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "Tên thư mục không được vượt quá 120 ký tự.")]
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateNotebookFolderRequest
    {
        [Required(ErrorMessage = "Tên thư mục không được để trống.")]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "Tên thư mục không được vượt quá 120 ký tự.")]
        public string Name { get; set; } = string.Empty;

        public int? ExpectedRevision { get; set; }
    }

    public class CreateNotebookFileRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn thư mục hợp lệ.")]
        public int? FolderId { get; set; }

        [Required(ErrorMessage = "Tên tệp ghi chú không được để trống.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Tên tệp ghi chú không được vượt quá 200 ký tự.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
        public string? Description { get; set; }

        public bool CreateInitialPage { get; set; } = true;
    }

    public class UpdateNotebookFileRequest
    {
        [StringLength(200, ErrorMessage = "Tên tệp ghi chú không được vượt quá 200 ký tự.")]
        public string? Name { get; set; }

        [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
        public string? Description { get; set; }

        public int? FolderId { get; set; }
        public int? SortOrder { get; set; }
        public int? ExpectedRevision { get; set; }
    }

    public class CreateNotebookPageRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tệp ghi chú hợp lệ.")]
        public int FileId { get; set; }

        [Required(ErrorMessage = "Tiêu đề trang không được để trống.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Tiêu đề trang không được vượt quá 200 ký tự.")]
        public string Title { get; set; } = string.Empty;

        public string? ContentJson { get; set; }

        [StringLength(40)]
        public string? SourceType { get; set; }
        public int? SourceId { get; set; }
        [StringLength(300)]
        public string? SourcePath { get; set; }
        [StringLength(200)]
        public string? SourceTitle { get; set; }
    }

    public class UpdateNotebookPageRequest
    {
        [StringLength(200, ErrorMessage = "Tiêu đề trang không được vượt quá 200 ký tự.")]
        public string? Title { get; set; }

        public string? ContentJson { get; set; }
        public int? FileId { get; set; }
        public bool? IsFavorite { get; set; }
        public int? SortOrder { get; set; }
        public int? ExpectedRevision { get; set; }
    }

    public class TogglePageFavoriteRequest
    {
        public bool IsFavorite { get; set; }
    }

    public class QuickSaveNotebookRequest
    {
        public int? NotebookPageId { get; set; }

        [Required(ErrorMessage = "Nội dung cần lưu không được để trống.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Từ hoặc cụm văn bản không được vượt quá 200 ký tự.")]
        public string Text { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Cách đọc không được vượt quá 200 ký tự.")]
        public string? Reading { get; set; }

        [StringLength(500, ErrorMessage = "Nghĩa hoặc ghi chú tóm tắt không được vượt quá 500 ký tự.")]
        public string? Meaning { get; set; }

        [StringLength(2000)]
        public string? Note { get; set; }

        [StringLength(32)]
        public string? ItemKind { get; set; }

        [StringLength(500)]
        public string? ExampleSentence { get; set; }

        [StringLength(500)]
        public string? ExampleMeaning { get; set; }

        public int? VocabularyId { get; set; }
        public int? KanjiId { get; set; }
        public int? GrammarPatternId { get; set; }

        [StringLength(40)]
        public string? SourceType { get; set; }
        public int? SourceId { get; set; }
        [StringLength(300)]
        public string? SourcePath { get; set; }
        [StringLength(200)]
        public string? SourceTitle { get; set; }
        [StringLength(600)]
        public string? ContextSnippet { get; set; }

        /// <summary>Notebook V3 boundary: saving text into Notebook never creates SRS cards unless explicitly requested.</summary>
        public bool EnrollSrs { get; set; } = false;
    }

    // Legacy + compatible request contracts kept intact while hardened with validation attributes.
    public class CreateLearnerNoteRequest
    {
        [Required(ErrorMessage = "Tên tệp ghi chú không được để trống.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Tên tệp ghi chú không được vượt quá 200 ký tự.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public int? FolderId { get; set; }
    }

    public class UpdateLearnerNoteRequest
    {
        [Required(ErrorMessage = "Tên tệp ghi chú không được để trống.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Tên tệp ghi chú không được vượt quá 200 ký tự.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public int? FolderId { get; set; }
        public int? ExpectedRevision { get; set; }
    }

    public class BookmarkVocabularyRequest
    {
        public int LearnerNoteId { get; set; }
        public int? NotebookPageId { get; set; }
        public int VocabularyId { get; set; }
        public bool EnrollSrs { get; set; } = false;
    }

    public class BookmarkKanjiRequest
    {
        public int LearnerNoteId { get; set; }
        public int? NotebookPageId { get; set; }
        public int KanjiId { get; set; }
        public bool EnrollSrs { get; set; } = false;
    }

    public class AddManualEntryRequest
    {
        public int LearnerNoteId { get; set; }
        public int? NotebookPageId { get; set; }

        [Required(ErrorMessage = "Từ hoặc tiêu đề mục không được để trống.")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Nội dung mục không được vượt quá 200 ký tự.")]
        public string Word { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Cách đọc không được vượt quá 200 ký tự.")]
        public string? Reading { get; set; }

        [Required(ErrorMessage = "Nghĩa hoặc nội dung chính không được để trống.")]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "Nghĩa không được vượt quá 500 ký tự.")]
        public string Meaning { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Note { get; set; }

        [StringLength(32)]
        public string? ItemKind { get; set; }
        [StringLength(24)]
        public string? HighlightColor { get; set; }
        [StringLength(500)]
        public string? ExampleSentence { get; set; }
        [StringLength(500)]
        public string? ExampleMeaning { get; set; }
        public List<string>? Tags { get; set; }

        public int? GrammarPatternId { get; set; }
        [StringLength(40)]
        public string? SourceType { get; set; }
        public int? SourceId { get; set; }
        [StringLength(300)]
        public string? SourcePath { get; set; }
        [StringLength(200)]
        public string? SourceTitle { get; set; }
        [StringLength(600)]
        public string? ContextSnippet { get; set; }

        public bool EnrollSrs { get; set; } = false;
    }

    public class UpdateNotebookEntryRequest
    {
        /// <summary>Tùy chọn di chuyển mục sang tệp / trang ghi chú khác thuộc cùng Learner.</summary>
        public int? LearnerNoteId { get; set; }
        public int? NotebookPageId { get; set; }

        [StringLength(200)]
        public string? Word { get; set; }
        [StringLength(200)]
        public string? Reading { get; set; }
        [StringLength(500)]
        public string? Meaning { get; set; }
        [StringLength(2000)]
        public string? Note { get; set; }

        [StringLength(32)]
        public string? ItemKind { get; set; }
        [StringLength(24)]
        public string? HighlightColor { get; set; }
        [StringLength(500)]
        public string? ExampleSentence { get; set; }
        [StringLength(500)]
        public string? ExampleMeaning { get; set; }
        public List<string>? Tags { get; set; }
        public bool? IsFavorite { get; set; }
        public int? ExpectedRevision { get; set; }
    }
}
