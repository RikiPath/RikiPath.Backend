using System;
using System.Collections.Generic;

namespace RikiPath.Application.Helpers;

/// <summary>
/// Centralized server-side password policy aligned with NIST 800-63B &amp; RikiPath Frontend rules.
/// Preserves exact user characters (never trims) and rejects common, predictable, or user-linked passwords.
/// </summary>
public static class PasswordPolicyHelper
{
    public const int MinLength = 6;
    public const int MaxLength = 128;

    private static readonly HashSet<string> CommonWeakPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456",
        "1234567",
        "12345678",
        "123456789",
        "1234567890",
        "123456789a",
        "123123",
        "111111",
        "000000",
        "654321",
        "987654321",
        "password",
        "password1",
        "password123",
        "passw0rd",
        "p@ssword",
        "p@ssw0rd",
        "qwerty",
        "qwerty123",
        "qwertyuiop",
        "asdfgh",
        "asdfghjkl",
        "zxcvbn",
        "zxcvbnm",
        "1q2w3e",
        "1q2w3e4r",
        "1qaz2wsx",
        "abc123",
        "admin123",
        "administrator",
        "welcome",
        "welcome1",
        "iloveyou",
        "sunshine",
        "princess",
        "football",
        "dragon",
        "monkey",
        "letmein",
        "baseball",
        "superman",
        "matkhau",
        "matkhau123",
        "anhyeuem",
        "khongbiet",
        "khongcomatkhau",
        "rikipath",
        "rikipath123",
        "nihongo",
        "jlpt123",
    };

    private static readonly string[] PredictableSequences =
    [
        "01234567890",
        "09876543210",
        "abcdefghijklmnopqrstuvwxyz",
        "zyxwvutsrqponmlkjihgfedcba",
        "qwertyuiopasdfghjklzxcvbnm",
        "mnbvcxzlkjhgfdsapoiuytrewq",
        "1qaz2wsx3edc4rfv5tgb6yhn7ujm",
    ];

    public static string? Validate(
        string? password,
        string? email = null,
        string? fullName = null,
        string fieldLabel = "Mật khẩu")
    {
        if (string.IsNullOrEmpty(password))
            return $"Vui lòng nhập {fieldLabel.ToLowerInvariant()}.";

        if (password.Length < MinLength || password.Length > MaxLength)
            return $"{fieldLabel} phải từ {MinLength} đến {MaxLength} ký tự.";

        if (string.IsNullOrWhiteSpace(password))
            return $"{fieldLabel} không thể chỉ gồm khoảng trắng.";

        if (AllSameCharacters(password))
            return $"{fieldLabel} quá dễ đoán (các ký tự giống hệt nhau). Vui lòng chọn mật khẩu khác.";

        var lower = password.ToLowerInvariant();
        if (CommonWeakPasswords.Contains(lower) || IsCommonBaseWithDigits(lower))
            return $"{fieldLabel} nằm trong danh sách mật khẩu phổ biến dễ bị đoán. Vui lòng chọn mật khẩu khác.";

        if (IsSinglePredictableSequence(lower))
            return $"{fieldLabel} là chuỗi ký tự liên tiếp hoặc dãy phím dễ đoán. Vui lòng chọn mật khẩu khó đoán hơn.";

        if (ContainsPersonalToken(lower, email, fullName))
            return $"{fieldLabel} quá giống với email hoặc tên tài khoản của bạn. Vui lòng chọn mật khẩu khó đoán hơn.";

        return null;
    }

    private static bool AllSameCharacters(string value)
    {
        var first = value[0];
        for (var i = 1; i < value.Length; i++)
        {
            if (value[i] != first) return false;
        }
        return true;
    }

    private static bool IsCommonBaseWithDigits(string lower)
    {
        var lettersOnly = lower.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '!', '@');
        return lettersOnly.Length >= 5 && lower.Length <= 12 && CommonWeakPasswords.Contains(lettersOnly);
    }

    private static bool IsSinglePredictableSequence(string lower)
    {
        if (lower.Length > 12) return false;
        foreach (var seq in PredictableSequences)
        {
            if (seq.Contains(lower, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool ContainsPersonalToken(string lowerPassword, string? email, string? fullName)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var at = email.IndexOf('@');
            var localPart = (at > 0 ? email[..at] : email).Trim().ToLowerInvariant();
            if (localPart.Length >= 4 && lowerPassword.Contains(localPart, StringComparison.Ordinal))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var collapsed = string.Concat(fullName.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
            if (collapsed.Length >= 5 && lowerPassword.Contains(collapsed, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
