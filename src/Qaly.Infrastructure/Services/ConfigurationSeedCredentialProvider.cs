using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Qaly.Application.Common.Interfaces;

namespace Qaly.Infrastructure.Services;

public sealed class ConfigurationSeedCredentialProvider : ISeedCredentialProvider
{
    private const string AdminEmail = "admin@qaly.dev";

    private static readonly HashSet<string> DemoUserEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        "minh.anh@qaly.dev",
        "bao.ngoc@qaly.dev",
        "quoc.huy@qaly.dev",
        "thu.ha@qaly.dev",
        "gia.khang@qaly.dev",
        "linh.chi@qaly.dev",
        "tuan.kiet@qaly.dev",
        "mai.phuong@qaly.dev",
        "thanh.tam@qaly.dev",
        "viet.long@qaly.dev",
        "yen.nhi@qaly.dev",
        "nguyenvana@qaly.dev",
        "tranthib@qaly.dev",
        "levancuong@qaly.dev",
        "phamminhduc@qaly.dev",
        "hoangthuha@qaly.dev",
        "danghonglien@qaly.dev",
        "vuquanghuy@qaly.dev",
        "buituyetmai@qaly.dev",
        "ngogiabao@qaly.dev"
    };

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ConfigurationSeedCredentialProvider(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public string? GetPassword(string normalizedEmail)
    {
        if (!_environment.IsDevelopment() &&
            !_configuration.GetValue<bool>("UseInMemoryDatabase"))
        {
            return null;
        }

        if (string.Equals(normalizedEmail, AdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return ReadSecret("Seed:AdminPassword", "QALY_SEED_ADMIN_PASSWORD");
        }

        return DemoUserEmails.Contains(normalizedEmail)
            ? ReadSecret("Seed:DefaultUserPassword", "QALY_SEED_DEFAULT_USER_PASSWORD")
            : null;
    }

    private string? ReadSecret(string configurationKey, string environmentVariable)
    {
        var value = _configuration[configurationKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            value = _configuration[environmentVariable];
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
