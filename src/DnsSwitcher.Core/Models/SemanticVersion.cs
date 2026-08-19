namespace DnsSwitcher.Core.Models;

public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string? Prerelease = null) : IComparable<SemanticVersion>
{
    public bool IsPrerelease => !string.IsNullOrWhiteSpace(Prerelease);

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var buildIndex = text.IndexOf('+');
        if (buildIndex >= 0)
        {
            if (buildIndex == text.Length - 1)
            {
                return false;
            }

            text = text[..buildIndex];
        }

        string? prerelease = null;
        var prereleaseIndex = text.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            if (prereleaseIndex == text.Length - 1)
            {
                return false;
            }

            prerelease = text[(prereleaseIndex + 1)..];
            text = text[..prereleaseIndex];
            if (!IsValidPrerelease(prerelease))
            {
                return false;
            }
        }

        var parts = text.Split('.');
        if (parts.Length != 3
            || !TryParseCoreNumber(parts[0], out var major)
            || !TryParseCoreNumber(parts[1], out var minor)
            || !TryParseCoreNumber(parts[2], out var patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public static SemanticVersion Parse(string value)
    {
        return TryParse(value, out var version)
            ? version
            : throw new FormatException($"Invalid semantic version: {value}");
    }

    public int CompareTo(SemanticVersion other)
    {
        var coreComparison = Major.CompareTo(other.Major);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        coreComparison = Minor.CompareTo(other.Minor);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        coreComparison = Patch.CompareTo(other.Patch);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        if (!IsPrerelease && !other.IsPrerelease)
        {
            return 0;
        }

        if (!IsPrerelease)
        {
            return 1;
        }

        if (!other.IsPrerelease)
        {
            return -1;
        }

        return ComparePrerelease(Prerelease!, other.Prerelease!);
    }

    public override string ToString()
    {
        var core = $"{Major}.{Minor}.{Patch}";
        return IsPrerelease ? $"{core}-{Prerelease}" : core;
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    private static bool TryParseCoreNumber(string value, out int number)
    {
        number = 0;
        if (value.Length == 0 || (value.Length > 1 && value[0] == '0'))
        {
            return false;
        }

        return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out number)
            && number >= 0;
    }

    private static bool IsValidPrerelease(string value)
    {
        foreach (var identifier in value.Split('.'))
        {
            if (identifier.Length == 0)
            {
                return false;
            }

            if (identifier.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            {
                return false;
            }

            if (identifier.All(char.IsDigit) && identifier.Length > 1 && identifier[0] == '0')
            {
                return false;
            }
        }

        return true;
    }

    private static int ComparePrerelease(string left, string right)
    {
        var leftIdentifiers = left.Split('.');
        var rightIdentifiers = right.Split('.');
        var commonLength = Math.Min(leftIdentifiers.Length, rightIdentifiers.Length);

        for (var index = 0; index < commonLength; index++)
        {
            var comparison = ComparePrereleaseIdentifier(leftIdentifiers[index], rightIdentifiers[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftIdentifiers.Length.CompareTo(rightIdentifiers.Length);
    }

    private static int ComparePrereleaseIdentifier(string left, string right)
    {
        var leftNumeric = left.All(char.IsDigit);
        var rightNumeric = right.All(char.IsDigit);

        if (leftNumeric && rightNumeric)
        {
            var lengthComparison = left.Length.CompareTo(right.Length);
            return lengthComparison != 0 ? lengthComparison : string.CompareOrdinal(left, right);
        }

        if (leftNumeric != rightNumeric)
        {
            return leftNumeric ? -1 : 1;
        }

        return string.CompareOrdinal(left, right);
    }
}
