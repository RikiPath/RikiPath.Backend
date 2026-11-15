using Microsoft.Extensions.Hosting;
using Moq;
using RikiPath.Application.Services;
using RikiPath.Domain;
using Xunit;

namespace RikiPath.Tests;

public sealed class QaPaymentSimulatorGuardTests
{
    private const string Database = "rikipath_b82_guarded_utf8";

    [Theory]
    [InlineData("MentorQA", "127.0.0.1", Database, true)]
    [InlineData("Testing", "localhost", Database, true)]
    [InlineData("Production", "127.0.0.1", Database, false)]
    [InlineData("MentorQA", "10.0.0.4", Database, false)]
    [InlineData("MentorQA", "127.0.0.1", "postgres", false)]
    public void Simulator_requires_qa_environment_and_exact_loopback_database(
        string environmentName, string host, string database, bool expected)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        var settings = new AppSettings
        {
            ConnectionStrings = new ConnectionStrings
            {
                DefaultConnection = $"Host={host};Port=5549;Database={database};Username=qa"
            },
            QaPaymentSimulator = new QaPaymentSimulatorSettings
            {
                Enabled = true,
                AllowedDatabaseName = Database
            }
        };

        Assert.Equal(expected, QaPaymentSimulatorGuard.IsEnabled(settings, environment.Object));
    }

    [Fact]
    public void Simulator_is_disabled_without_explicit_feature_flag()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns("MentorQA");
        var settings = new AppSettings
        {
            ConnectionStrings = new ConnectionStrings
            {
                DefaultConnection = $"Host=127.0.0.1;Port=5549;Database={Database}"
            },
            QaPaymentSimulator = new QaPaymentSimulatorSettings { AllowedDatabaseName = Database }
        };

        Assert.False(QaPaymentSimulatorGuard.IsEnabled(settings, environment.Object));
    }
}
