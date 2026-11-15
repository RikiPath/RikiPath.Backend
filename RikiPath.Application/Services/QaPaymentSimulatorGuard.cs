using System.Data.Common;
using Microsoft.Extensions.Hosting;
using RikiPath.Domain;

namespace RikiPath.Application.Services;

public static class QaPaymentSimulatorGuard
{
    public static bool IsEnabled(AppSettings settings, IHostEnvironment environment)
    {
        if (!(environment.IsEnvironment("MentorQA") || environment.IsEnvironment("Testing")) ||
            settings.QaPaymentSimulator?.Enabled != true ||
            string.IsNullOrWhiteSpace(settings.QaPaymentSimulator.AllowedDatabaseName))
            return false;

        try
        {
            var connection = new DbConnectionStringBuilder
            {
                ConnectionString = settings.ConnectionStrings?.DefaultConnection ?? string.Empty
            };
            var host = GetValue(connection, "Host", "Server");
            var database = GetValue(connection, "Database", "Initial Catalog");
            return (string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)) &&
                   string.Equals(database, settings.QaPaymentSimulator.AllowedDatabaseName, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string? GetValue(DbConnectionStringBuilder connection, string key, string alternate)
        => connection.TryGetValue(key, out var value) ? Convert.ToString(value) :
            connection.TryGetValue(alternate, out value) ? Convert.ToString(value) : null;
}
