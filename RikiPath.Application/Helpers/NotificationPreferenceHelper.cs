using System.Text.Json;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Helpers;

/// <summary>
/// Manages per-role notification categories and real Backend delivery enforcement.
/// </summary>
public static class NotificationPreferenceHelper
{
    public const string SecurityAlertsKey = "securityAlerts";

    public static IReadOnlyList<string> KeysForRole(Role role) => role switch
    {
        Role.Learner => ["schedule", "homework", "studyReminder"],
        Role.Mentor => ["scheduleReview", "teachingSchedule", "cohort", "homework"],
        Role.ContentAuthor => ["approvalResult", "rejectedContent", "revisionRequest"],
        Role.Admin => ["approvalQueue", "complaints", "operations"],
        _ => ["schedule", "homework", "studyReminder"],
    };

    public static Dictionary<string, bool> ParseForRole(Role role, string? json)
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var key in KeysForRole(role))
            result[key] = true;
        result[SecurityAlertsKey] = true;

        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            if (parsed is null) return result;
            foreach (var key in KeysForRole(role))
            {
                if (parsed.TryGetValue(key, out var enabled))
                    result[key] = enabled;
            }
        }
        catch (JsonException)
        {
            // Keep safe default true flags when legacy/malformed JSON appears.
        }

        return result;
    }

    public static string SerializeMerged(Role role, string? existingJson, IReadOnlyDictionary<string, bool>? updates)
    {
        var merged = ParseForRole(role, existingJson);
        if (updates is not null)
        {
            var allowed = new HashSet<string>(KeysForRole(role), StringComparer.Ordinal);
            foreach (var (key, value) in updates)
            {
                if (allowed.Contains(key))
                    merged[key] = value;
            }
        }
        merged[SecurityAlertsKey] = true;
        return JsonSerializer.Serialize(merged);
    }

    public static bool ShouldDeliverInApp(UserAccount user, string? notificationType)
    {
        if (IsMandatorySecurityType(notificationType))
            return true;

        if (!user.SystemNotificationsEnabled)
            return false;

        var category = MapTypeToCategory(user.Role, notificationType);
        if (category is null)
            return true;

        var prefs = ParseForRole(user.Role, user.NotificationPreferencesJson);
        return !prefs.TryGetValue(category, out var enabled) || enabled;
    }

    public static bool IsMandatorySecurityType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        return type.Contains("Security", StringComparison.OrdinalIgnoreCase)
            || type.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || type.Contains("EmailVerification", StringComparison.OrdinalIgnoreCase);
    }

    private static string? MapTypeToCategory(Role role, string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        var t = type.Trim();

        return role switch
        {
            Role.Learner => t.Contains("Homework", StringComparison.OrdinalIgnoreCase) ? "homework"
                : t.Contains("Review", StringComparison.OrdinalIgnoreCase) || t.Contains("Reminder", StringComparison.OrdinalIgnoreCase) || t.Contains("Streak", StringComparison.OrdinalIgnoreCase) ? "studyReminder"
                : t.Contains("Cohort", StringComparison.OrdinalIgnoreCase) || t.Contains("Schedule", StringComparison.OrdinalIgnoreCase) || t.Contains("Slot", StringComparison.OrdinalIgnoreCase) ? "schedule"
                : null,
            Role.Mentor => t.Contains("Review", StringComparison.OrdinalIgnoreCase) || t.Contains("Approval", StringComparison.OrdinalIgnoreCase) ? "scheduleReview"
                : t.Contains("Homework", StringComparison.OrdinalIgnoreCase) ? "homework"
                : t.Contains("Cohort", StringComparison.OrdinalIgnoreCase) || t.Contains("Complaint", StringComparison.OrdinalIgnoreCase) ? "cohort"
                : t.Contains("Schedule", StringComparison.OrdinalIgnoreCase) || t.Contains("Slot", StringComparison.OrdinalIgnoreCase) ? "teachingSchedule"
                : null,
            Role.ContentAuthor => t.Contains("Reject", StringComparison.OrdinalIgnoreCase) ? "rejectedContent"
                : t.Contains("Revision", StringComparison.OrdinalIgnoreCase) || t.Contains("Edit", StringComparison.OrdinalIgnoreCase) ? "revisionRequest"
                : t.Contains("Content", StringComparison.OrdinalIgnoreCase) || t.Contains("Approval", StringComparison.OrdinalIgnoreCase) ? "approvalResult"
                : null,
            Role.Admin => t.Contains("Complaint", StringComparison.OrdinalIgnoreCase) ? "complaints"
                : t.Contains("Approval", StringComparison.OrdinalIgnoreCase) || t.Contains("Verification", StringComparison.OrdinalIgnoreCase) || t.Contains("Content", StringComparison.OrdinalIgnoreCase) ? "approvalQueue"
                : "operations",
            _ => null,
        };
    }
}
