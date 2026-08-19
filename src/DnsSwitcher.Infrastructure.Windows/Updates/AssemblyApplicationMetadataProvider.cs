using System.Reflection;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Infrastructure.Windows.Updates;

public static class AssemblyApplicationMetadataProvider
{
    public static ApplicationMetadata FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var versionText = informationalVersion?.Split('+', 2)[0]
            ?? assembly.GetName().Version?.ToString(3)
            ?? throw new InvalidOperationException("Application version metadata is missing.");

        if (!SemanticVersion.TryParse(versionText, out var version))
        {
            throw new InvalidOperationException($"Application version metadata is not valid SemVer: {versionText}");
        }

        var repositoryUrl = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "RepositoryUrl", StringComparison.Ordinal))
            ?.Value;

        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var repositoryUri)
            || repositoryUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("RepositoryUrl assembly metadata must be an absolute HTTPS URI.");
        }

        var productName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        if (string.IsNullOrWhiteSpace(productName))
        {
            productName = assembly.GetName().Name ?? "DnsSwitcher";
        }

        return new ApplicationMetadata(productName, version, versionText, repositoryUri);
    }
}
