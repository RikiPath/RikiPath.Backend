using RikiPath.Application.DTOs.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>
/// Ánh xạ ContentEntityType (loại nội dung Author thao tác) sang ImportEntityType
/// (loại lưu trong ContentApprovalRequest). Chỉ các loại đi qua bản nháp/duyệt mới có mapping:
/// Lesson, Kanji, Vocabulary, GrammarPattern.
/// </summary>
public static class ContentStagingMap
{
    /// <summary>Loại nội dung đi qua ContentApprovalRequest (staging) thay vì ghi thẳng vào bảng chính.</summary>
    public static bool IsStaged(ContentEntityType type) =>
        type is ContentEntityType.Lesson or ContentEntityType.Kanji
             or ContentEntityType.Vocabulary or ContentEntityType.GrammarPattern;

    public static ImportEntityType ToImportType(ContentEntityType type)
    {
        if (!IsStaged(type))
            throw new InvalidOperationException($"Loại nội dung {type} không đi qua bản nháp duyệt.");

        // Map theo tên enum để không phụ thuộc thứ tự/giá trị số. Thử thêm tên gần nghĩa
        // phòng khi ImportEntityType đặt khác (vd "Grammar" thay vì "GrammarPattern").
        var candidates = type switch
        {
            ContentEntityType.GrammarPattern => new[] { "GrammarPattern", "Grammar" },
            _ => new[] { type.ToString() },
        };

        foreach (var name in candidates)
            if (Enum.TryParse<ImportEntityType>(name, ignoreCase: true, out var result) &&
                Enum.IsDefined(result))
                return result;

        throw new InvalidOperationException(
            $"ImportEntityType không có giá trị tương ứng với {type}. Hãy bổ sung vào ContentStagingMap.");
    }
}