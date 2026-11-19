using System.Text.RegularExpressions;

namespace RikiPath.Application.Helpers;

/// <summary>
/// Chuẩn hóa và đồng bộ <c>FullName</c> trên API với các cột <c>FirstName</c>/<c>LastName</c> (tối đa 100 ký tự mỗi cột).
/// </summary>
public static partial class UserNameHelper
{
    public const int MaxPartLength = 100;
    public const int MaxFullNameLength = MaxPartLength * 2;

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    public static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return MultiSpaceRegex().Replace(value.Trim(), " ");
    }

    public static string FormatFullName(string? firstName, string? lastName) =>
        NormalizeWhitespace($"{firstName} {lastName}");

    public static (string? FirstName, string? LastName) SplitFullName(string? fullName)
    {
        var normalized = NormalizeWhitespace(fullName);
        if (string.IsNullOrEmpty(normalized))
            return (null, null);

        var clamped = TakeMaxChars(normalized, MaxFullNameLength);
        if (clamped.Length <= MaxPartLength)
            return (clamped, null);

        var splitAt = clamped.LastIndexOf(' ', MaxPartLength);
        if (splitAt <= 0 || clamped.Length - splitAt - 1 > MaxPartLength)
            splitAt = MaxPartLength;

        var first = clamped[..splitAt].TrimEnd();
        var second = clamped[splitAt..].TrimStart();
        if (string.IsNullOrEmpty(second))
            return (first, null);

        if (second.Length > MaxPartLength)
            second = TakeMaxChars(second, MaxPartLength);

        return (first, second);
    }

    private static string TakeMaxChars(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        var slice = value[..maxLength];
        if (char.IsHighSurrogate(slice[^1]))
            slice = slice[..^1];
        return slice.TrimEnd();
    }
}
