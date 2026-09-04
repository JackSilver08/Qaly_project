using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class ConfigurationSeedCredentialProviderTests
{
    [Fact]
    public void DevelopmentUsesConfiguredCredentialsForRichDemoAccounts()
    {
        var configuration = CreateConfiguration(
            ("Seed:AdminPassword", "ConfiguredAdminPassword"),
            ("Seed:DefaultUserPassword", "ConfiguredUserPassword"));
        var provider = new ConfigurationSeedCredentialProvider(
            configuration,
            new TestHostEnvironment(Environments.Development));

        provider.GetPassword("admin@qaly.dev").Should().Be("ConfiguredAdminPassword");
        provider.GetPassword("bao.ngoc@qaly.dev").Should().Be("ConfiguredUserPassword");
        provider.GetPassword("customer@qaly.dev").Should().BeNull();
    }

    [Fact]
    public void ProductionNeverExposesSeedCredentials()
    {
        var configuration = CreateConfiguration(
            ("Seed:AdminPassword", "ConfiguredAdminPassword"));
        var provider = new ConfigurationSeedCredentialProvider(
            configuration,
            new TestHostEnvironment(Environments.Production));

        provider.GetPassword("admin@qaly.dev").Should().BeNull();
    }

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(item => item.Key, item => (string?)item.Value))
            .Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Qaly.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
