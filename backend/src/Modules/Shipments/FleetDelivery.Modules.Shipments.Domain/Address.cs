namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>
/// EF-owned value type — not its own aggregate/entity, no identity of its
/// own. Two addresses with the same field values are interchangeable.
/// </summary>
public sealed record Address(string Street, string City, string State, string PostalCode, string Country)
{
    public static Address Create(string street, string city, string state, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            throw new ArgumentException("Street cannot be empty.", nameof(street));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("City cannot be empty.", nameof(city));
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("State cannot be empty.", nameof(state));
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new ArgumentException("Postal code cannot be empty.", nameof(postalCode));
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ArgumentException("Country cannot be empty.", nameof(country));
        }

        return new Address(street.Trim(), city.Trim(), state.Trim(), postalCode.Trim(), country.Trim());
    }
}
