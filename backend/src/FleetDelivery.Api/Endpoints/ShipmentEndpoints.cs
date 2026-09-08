using System.Security.Claims;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Application.Shipments;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Api.Endpoints;

/// <summary>
/// Maps <c>/api/shipments/*</c>. Role restrictions are enforced per-route via
/// <c>RequireRole</c>; ownership (a Driver may only act on/see a shipment
/// currently assigned to them) is enforced inside the Application-layer
/// handlers, not here — this file only extracts the caller's id/role from
/// the JWT and maps <see cref="FleetDelivery.BuildingBlocks.Results.Result{TValue}"/>
/// failures to HTTP responses.
/// </summary>
public static class ShipmentEndpoints
{
    public static IEndpointRouteBuilder MapShipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/shipments").WithTags("Shipments").RequireAuthorization();

        group.MapPost("", CreateShipmentAsync).RequireAuthorization(policy => policy.RequireRole("Dispatcher", "Admin"));
        group.MapGet("", ListShipmentsAsync);
        group.MapGet("/drivers", ListAvailableDriversAsync).RequireAuthorization(policy => policy.RequireRole("Dispatcher", "Admin"));
        group.MapGet("/{id:guid}", GetShipmentByIdAsync);
        group.MapGet("/{id:guid}/timeline", GetShipmentTimelineAsync);
        group.MapGet("/{id:guid}/proof-of-delivery", GetProofOfDeliveryPhotoAsync);

        group.MapPost("/{id:guid}/ready-for-dispatch", ReadyForDispatchAsync).RequireAuthorization(policy => policy.RequireRole("Dispatcher", "Admin"));
        group.MapPost("/{id:guid}/assign", AssignAsync).RequireAuthorization(policy => policy.RequireRole("Dispatcher", "Admin"));
        group.MapPost("/{id:guid}/pickup", MarkPickedUpAsync).RequireAuthorization(policy => policy.RequireRole("Driver"));
        group.MapPost("/{id:guid}/in-transit", MarkInTransitAsync).RequireAuthorization(policy => policy.RequireRole("Driver"));
        group.MapPost("/{id:guid}/out-for-delivery", MarkOutForDeliveryAsync).RequireAuthorization(policy => policy.RequireRole("Driver"));
        group.MapPost("/{id:guid}/deliver", MarkDeliveredAsync).RequireAuthorization(policy => policy.RequireRole("Driver"));
        group.MapPost("/{id:guid}/proof-of-delivery", AttachProofOfDeliveryAsync)
            .DisableAntiforgery()
            .RequireAuthorization(policy => policy.RequireRole("Driver"));
        group.MapPost("/{id:guid}/fail", MarkFailedAsync).RequireAuthorization(policy => policy.RequireRole("Driver"));
        group.MapPost("/{id:guid}/reschedule", RescheduleAsync).RequireAuthorization(policy => policy.RequireRole("Dispatcher", "Admin"));
        group.MapPost("/{id:guid}/return", ReturnAsync).RequireAuthorization(policy => policy.RequireRole("Dispatcher", "Admin"));
        group.MapPost("/{id:guid}/cancel", CancelAsync).RequireAuthorization(policy => policy.RequireRole("Dispatcher", "Admin"));

        return app;
    }

    private static async Task<IResult> CreateShipmentAsync(CreateShipmentRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var command = new CreateShipmentCommand(
            request.RecipientName,
            request.RecipientPhone,
            request.Origin.ToDto(),
            request.Destination.ToDto(),
            callerId);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> ListShipmentsAsync(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        Guid? driverId = null)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        ShipmentStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ShipmentStatus>(status, ignoreCase: true, out var value))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid status filter.", detail: $"'{status}' is not a valid shipment status.");
            }

            parsedStatus = value;
        }

        var callerRole = GetCallerRole(user);
        var query = new ListShipmentsQuery(page, pageSize, parsedStatus, driverId, callerId, callerRole);

        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> ListAvailableDriversAsync(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListAvailableDriversQuery(), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.Select(d => d.ToResponse()).ToList()) : MapFailure(result.Error);
    }

    private static async Task<IResult> GetShipmentByIdAsync(Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new GetShipmentByIdQuery(id, callerId, GetCallerRole(user)), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> GetShipmentTimelineAsync(Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new GetShipmentTimelineQuery(id, callerId, GetCallerRole(user)), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> GetProofOfDeliveryPhotoAsync(Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new GetProofOfDeliveryPhotoQuery(id, callerId, GetCallerRole(user)), cancellationToken);

        return result.IsSuccess
            ? Results.File(result.Value.Content, result.Value.ContentType)
            : MapFailure(result.Error);
    }

    private static async Task<IResult> ReadyForDispatchAsync(Guid id, ExpectedVersionRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new ReadyForDispatchCommand(id, callerId, request.ExpectedVersion), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> AssignAsync(Guid id, AssignRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new AssignCommand(id, request.DriverId, request.VehicleId, callerId, request.ExpectedVersion), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> MarkPickedUpAsync(Guid id, ExpectedVersionRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new MarkPickedUpCommand(id, callerId, request.ExpectedVersion), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> MarkInTransitAsync(Guid id, ExpectedVersionRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new MarkInTransitCommand(id, callerId, request.ExpectedVersion), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> MarkOutForDeliveryAsync(Guid id, ExpectedVersionRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new MarkOutForDeliveryCommand(id, callerId, request.ExpectedVersion), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> MarkDeliveredAsync(Guid id, MarkDeliveredRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new MarkDeliveredCommand(id, callerId, request.ExpectedVersion, request.RecipientName, request.Notes), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> AttachProofOfDeliveryAsync(
        Guid id,
        IFormFile file,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        // Checked against IFormFile.Length — known from the multipart part's
        // headers — before ever reading the body, so an oversized upload is
        // rejected without first buffering the whole thing into memory.
        if (file.Length > ProofOfDeliveryPhoto.MaxContentBytes)
        {
            return MapFailure(ShipmentErrors.ProofOfDeliveryTooLarge);
        }

        await using var contentStream = new MemoryStream();
        await file.CopyToAsync(contentStream, cancellationToken);

        var result = await sender.Send(
            new AttachProofOfDeliveryCommand(id, callerId, contentStream.ToArray(), file.ContentType),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> MarkFailedAsync(Guid id, MarkFailedRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new MarkFailedCommand(id, callerId, request.ExpectedVersion, request.Reason), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> RescheduleAsync(Guid id, ExpectedVersionRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RescheduleCommand(id, callerId, request.ExpectedVersion), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> ReturnAsync(Guid id, ReturnRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new ReturnCommand(id, callerId, request.ExpectedVersion, request.Reason), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> CancelAsync(Guid id, CancelRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(user, out var callerId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new CancelCommand(id, callerId, request.ExpectedVersion, request.Reason), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static bool TryGetCallerId(ClaimsPrincipal user, out Guid userId)
    {
        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        return Guid.TryParse(subject, out userId);
    }

    private static string GetCallerRole(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    /// <summary>
    /// Not-found and driver-not-permitted-to-see-it share <see cref="ShipmentErrors.NotFound"/>
    /// and therefore the same 404 — deliberately indistinguishable (IDOR
    /// hardening). Invalid-transition and stale-version both map to 409 but
    /// carry distinct <c>type</c>/<c>title</c> so callers can tell them apart.
    /// </summary>
    private static IResult MapFailure(FleetDelivery.BuildingBlocks.Results.Error error) => error.Code switch
    {
        "Shipment.NotFound" => Results.NotFound(),

        "Shipment.InvalidTransition" => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Invalid shipment transition.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-invalid-transition"),

        "Shipment.ConcurrencyConflict" => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Concurrency conflict.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-concurrency-conflict"),

        "Shipment.InvalidDriver" => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid driver.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-invalid-driver"),

        "Shipment.DriverBusy" => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Driver unavailable.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-driver-busy"),

        "Shipment.InvalidVehicle" => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid vehicle.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-invalid-vehicle"),

        "Shipment.ProofOfDeliveryTooLarge" => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Proof of Delivery photo too large.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-proof-of-delivery-too-large"),

        "Shipment.InvalidProofOfDeliveryContent" => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid Proof of Delivery photo.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-invalid-proof-of-delivery-content"),

        "Shipment.NotDelivered" => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Shipment not delivered.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-not-delivered"),

        "Shipment.ProofOfDeliveryAlreadyAttached" => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Proof of Delivery already attached.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-proof-of-delivery-already-attached"),

        "Shipment.Validation" => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation error.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/shipment-validation"),

        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error."),
    };
}

public sealed record AddressRequest(string Street, string City, string State, string PostalCode, string Country)
{
    public AddressDto ToDto() => new(Street, City, State, PostalCode, Country);
}

public sealed record AddressResponse(string Street, string City, string State, string PostalCode, string Country);

public sealed record CreateShipmentRequest(string RecipientName, string RecipientPhone, AddressRequest Origin, AddressRequest Destination);

public sealed record ExpectedVersionRequest(int ExpectedVersion);

public sealed record AssignRequest(Guid DriverId, Guid VehicleId, int ExpectedVersion);

public sealed record MarkDeliveredRequest(int ExpectedVersion, string? RecipientName, string? Notes);

public sealed record MarkFailedRequest(int ExpectedVersion, string Reason);

public sealed record ReturnRequest(int ExpectedVersion, string Reason);

public sealed record CancelRequest(int ExpectedVersion, string Reason);

public sealed record ShipmentResponse(
    Guid Id,
    string TrackingNumber,
    string Status,
    string RecipientName,
    string RecipientPhone,
    AddressResponse Origin,
    AddressResponse Destination,
    Guid? AssignedDriverId,
    Guid? AssignedVehicleId,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool HasProofOfDelivery,
    int Version);

public sealed record ShipmentListResponse(IReadOnlyList<ShipmentResponse> Items, int Page, int PageSize, int TotalCount);

public sealed record AvailableDriverResponse(Guid Id, string FullName, string Email, bool IsAvailable);

public sealed record TrackingEventResponse(string Type, DateTimeOffset OccurredAt, Guid? ActorUserId, string? Notes);

public sealed record ShipmentTimelineResponse(IReadOnlyList<TrackingEventResponse> Events);

internal static class ShipmentApiMapping
{
    public static AddressResponse ToResponse(this AddressDto dto) => new(dto.Street, dto.City, dto.State, dto.PostalCode, dto.Country);

    public static ShipmentResponse ToResponse(this ShipmentDto dto) => new(
        dto.Id,
        dto.TrackingNumber,
        dto.Status,
        dto.RecipientName,
        dto.RecipientPhone,
        dto.Origin.ToResponse(),
        dto.Destination.ToResponse(),
        dto.AssignedDriverId,
        dto.AssignedVehicleId,
        dto.CreatedByUserId,
        dto.CreatedAt,
        dto.HasProofOfDelivery,
        dto.Version);

    public static AvailableDriverResponse ToResponse(this AvailableDriverDto dto) => new(dto.Id, dto.FullName, dto.Email, dto.IsAvailable);

    public static ShipmentListResponse ToResponse(this ShipmentListPageDto dto) => new(
        dto.Items.Select(ToResponse).ToList(),
        dto.Page,
        dto.PageSize,
        dto.TotalCount);

    public static ShipmentTimelineResponse ToResponse(this ShipmentTimelineDto dto) => new(
        dto.Events.Select(e => new TrackingEventResponse(e.Type, e.OccurredAt, e.ActorUserId, e.Notes)).ToList());
}
