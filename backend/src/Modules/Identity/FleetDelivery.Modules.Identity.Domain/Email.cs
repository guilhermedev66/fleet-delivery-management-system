using System.Text.RegularExpressions;

namespace FleetDelivery.Modules.Identity.Domain;

/// <summary>
/// Value object wrapping a validated email address. Equality/comparison is
/// case-insensitive, matching the case-insensitive unique index enforced on
/// <see cref="User.NormalizedEmail"/> by the Infrastructure layer.
/// </summary>
public sealed partial class Email : IEquatable<Email>
{
    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>
    /// Creates an <see cref="Email"/> from raw input, validating format.
    /// Throws <see cref="ArgumentException"/> on invalid input — this is a
    /// programmer/input error, not an expected business failure, so it is
    /// not modeled as a <see cref="FleetDelivery.BuildingBlocks.Results.Result"/>.
    /// </summary>
    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Email cannot be empty.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > 320 || !EmailRegex().IsMatch(trimmed))
        {
            throw new ArgumentException($"'{value}' is not a valid email address.", nameof(value));
        }

        return new Email(trimmed);
    }

    /// <summary>Uppercase-invariant form used for case-insensitive comparison/storage.</summary>
    public string Normalize() => Value.ToUpperInvariant();

    public override string ToString() => Value;

    public bool Equals(Email? other) => other is not null && string.Equals(Normalize(), other.Normalize(), StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as Email);

    public override int GetHashCode() => Normalize().GetHashCode(StringComparison.Ordinal);

    // Deliberately simple RFC-5322-ish check: local@domain.tld, no leading/trailing dots,
    // no consecutive dots. Good enough to reject obvious garbage without pulling in a
    // full RFC 5322 parser.
    [GeneratedRegex(@"^[a-zA-Z0-9.!#$%&'*+\/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+$")]
    private static partial Regex EmailRegex();
}
