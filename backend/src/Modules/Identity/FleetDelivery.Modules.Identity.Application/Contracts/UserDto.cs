namespace FleetDelivery.Modules.Identity.Application.Contracts;

/// <summary>Public, cross-module-safe projection of a <see cref="Domain.User"/>.</summary>
public sealed record UserDto(Guid Id, string Email, string FullName, string Role);
