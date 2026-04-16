using DnsSwitcher.Core.Abstractions;
using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class SplitDnsRuleService(ISplitDnsRulesStore rulesStore, DnsProfileService profileService)
{
    public async Task<SplitDnsConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await rulesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return NormalizeConfiguration(configuration);
    }

    public async Task SaveConfigurationAsync(
        SplitDnsConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var normalized = NormalizeConfiguration(configuration);
        await ValidateAsync(normalized, cancellationToken).ConfigureAwait(false);
        await rulesStore.SaveAsync(normalized, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SplitDnsRule> AddRuleAsync(
        string @namespace,
        string profileId,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var configuration = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var rule = new SplitDnsRule
        {
            Id = CreateRuleId(@namespace, configuration.Rules),
            Namespace = NormalizeNamespace(@namespace),
            ProfileId = profileId.Trim(),
            Enabled = true,
            Priority = 0,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
        };

        await SaveConfigurationAsync(configuration with { Rules = [.. configuration.Rules, rule] }, cancellationToken)
            .ConfigureAwait(false);
        return rule;
    }

    public async Task RemoveRuleAsync(string idOrNamespace, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrNamespace);

        var configuration = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var normalizedNamespace = NormalizeNamespace(idOrNamespace);
        var rules = configuration.Rules
            .Where(rule =>
                !string.Equals(rule.Id, idOrNamespace, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(rule.Namespace, normalizedNamespace, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rules.Count == configuration.Rules.Count)
        {
            throw new InvalidDataException($"Split DNS rule '{idOrNamespace}' was not found.");
        }

        await SaveConfigurationAsync(configuration with { Rules = rules }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetRuleEnabledAsync(
        string id,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var configuration = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var changed = false;
        var rules = configuration.Rules
            .Select(rule =>
            {
                if (!string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return rule;
                }

                changed = true;
                return rule with { Enabled = enabled };
            })
            .ToList();

        if (!changed)
        {
            throw new InvalidDataException($"Split DNS rule '{id}' was not found.");
        }

        await SaveConfigurationAsync(configuration with { Rules = rules }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SplitDnsRule> UpdateRuleAsync(
        string id,
        string @namespace,
        string profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var configuration = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        SplitDnsRule? updatedRule = null;
        var rules = configuration.Rules
            .Select(rule =>
            {
                if (!string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return rule;
                }

                updatedRule = rule with
                {
                    Namespace = NormalizeNamespace(@namespace),
                    ProfileId = profileId.Trim(),
                };
                return updatedRule;
            })
            .ToList();

        if (updatedRule is null)
        {
            throw new InvalidDataException($"Split DNS rule '{id}' was not found.");
        }

        await SaveConfigurationAsync(configuration with { Rules = rules }, cancellationToken).ConfigureAwait(false);
        return updatedRule;
    }

    public async Task<SplitDnsRuleMatch> TestMatchAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        var configuration = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var normalizedDomain = NormalizeDomain(domain);
        var rule = SelectMatchingRule(configuration.Rules, normalizedDomain);

        return rule is null
            ? new SplitDnsRuleMatch(normalizedDomain, Matched: false, Rule: null, "No Split DNS rule matches this domain.")
            : new SplitDnsRuleMatch(
                normalizedDomain,
                Matched: true,
                Rule: rule,
                $"Matched rule '{rule.Id}' ({rule.Namespace}) -> profile '{rule.ProfileId}'.");
    }

    private async Task ValidateAsync(SplitDnsConfiguration configuration, CancellationToken cancellationToken)
    {
        var appConfig = await profileService.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var profileIds = appConfig.Profiles.Select(profile => profile.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in configuration.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new InvalidDataException("Split DNS rule id must not be empty.");
            }

            if (rule.Id.Any(char.IsWhiteSpace))
            {
                throw new InvalidDataException($"Split DNS rule id '{rule.Id}' must not contain whitespace.");
            }

            if (string.IsNullOrWhiteSpace(rule.Namespace))
            {
                throw new InvalidDataException($"Split DNS rule '{rule.Id}' namespace must not be empty.");
            }

            ValidateNamespace(rule.Id, rule.Namespace);

            if (!profileIds.Contains(rule.ProfileId))
            {
                throw new InvalidDataException($"Split DNS rule '{rule.Id}' references missing profile '{rule.ProfileId}'.");
            }
        }

        var duplicateId = configuration.Rules
            .GroupBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateId is not null)
        {
            throw new InvalidDataException($"Split DNS rule id '{duplicateId.Key}' is duplicated.");
        }

        var conflicts = configuration.Rules
            .Where(rule => rule.Enabled)
            .GroupBy(rule => NormalizeNamespace(rule.Namespace), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(rule => rule.ProfileId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .ToArray();

        if (conflicts.Length > 0)
        {
            throw new InvalidDataException(
                $"Split DNS rule conflict for namespace '{conflicts[0].Key}'. Disable or remove duplicate rules first.");
        }
    }

    private static SplitDnsConfiguration NormalizeConfiguration(SplitDnsConfiguration configuration)
    {
        return configuration with
        {
            Rules = configuration.Rules
                .Where(rule => rule is not null)
                .Select(rule => rule with
                {
                    Id = string.IsNullOrWhiteSpace(rule.Id) ? CreateRuleId(rule.Namespace, configuration.Rules) : rule.Id.Trim(),
                    Namespace = NormalizeNamespace(rule.Namespace),
                    ProfileId = rule.ProfileId.Trim(),
                    Comment = string.IsNullOrWhiteSpace(rule.Comment) ? null : rule.Comment.Trim(),
                })
                .ToList(),
        };
    }

    private static SplitDnsRule? SelectMatchingRule(IReadOnlyList<SplitDnsRule> rules, string domain)
    {
        return rules
            .Where(rule => rule.Enabled && NamespaceMatches(rule.Namespace, domain))
            .OrderByDescending(rule => rule.Priority)
            .ThenByDescending(rule => NormalizeNamespace(rule.Namespace).TrimStart('.').Length)
            .FirstOrDefault();
    }

    private static bool NamespaceMatches(string @namespace, string domain)
    {
        var normalizedNamespace = NormalizeNamespace(@namespace);
        var suffix = normalizedNamespace.TrimStart('.');

        return string.Equals(domain, suffix, StringComparison.OrdinalIgnoreCase)
            || domain.EndsWith($".{suffix}", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeNamespace(string @namespace)
    {
        var value = @namespace.Trim().TrimEnd('.').ToLowerInvariant();

        if (value.StartsWith("*.", StringComparison.Ordinal))
        {
            value = $".{value[2..]}";
        }

        return value;
    }

    private static void ValidateNamespace(string ruleId, string @namespace)
    {
        var normalized = NormalizeNamespace(@namespace);

        if (normalized.Contains("://", StringComparison.Ordinal)
            || normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains('\\', StringComparison.Ordinal)
            || normalized.Contains("..", StringComparison.Ordinal)
            || normalized.Any(char.IsWhiteSpace)
            || normalized.Contains('*', StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Split DNS rule '{ruleId}' namespace '{@namespace}' is not a valid domain suffix.");
        }

        var suffix = normalized.TrimStart('.');

        if (string.IsNullOrWhiteSpace(suffix))
        {
            throw new InvalidDataException($"Split DNS rule '{ruleId}' namespace must contain a domain suffix.");
        }

        var labels = suffix.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (labels.Length == 0 || labels.Any(label => label.Length == 0))
        {
            throw new InvalidDataException($"Split DNS rule '{ruleId}' namespace '{@namespace}' is not a valid domain suffix.");
        }

        foreach (var label in labels)
        {
            if (label.StartsWith("-", StringComparison.Ordinal)
                || label.EndsWith("-", StringComparison.Ordinal)
                || label.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            {
                throw new InvalidDataException($"Split DNS rule '{ruleId}' namespace '{@namespace}' contains invalid label '{label}'.");
            }
        }
    }

    private static string NormalizeDomain(string domain)
    {
        return domain.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private static string CreateRuleId(string @namespace, IReadOnlyList<SplitDnsRule> existingRules)
    {
        var normalized = NormalizeNamespace(@namespace).TrimStart('.');
        var chars = normalized
            .Select(character => char.IsAsciiLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var baseId = new string(chars).Trim('-');

        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "rule";
        }

        var candidate = baseId;
        var suffix = 2;

        while (existingRules.Any(rule => string.Equals(rule.Id, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
