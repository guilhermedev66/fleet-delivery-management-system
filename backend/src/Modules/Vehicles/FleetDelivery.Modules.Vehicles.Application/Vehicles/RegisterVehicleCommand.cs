using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Vehicles.Application.Abstractions;
using FleetDelivery.Modules.Vehicles.Application.Contracts;
using FleetDelivery.Modules.Vehicles.Domain;
using MediatR;

namespace FleetDelivery.Modules.Vehicles.Application.Vehicles;

public sealed record RegisterVehicleCommand(string PlateNumber, VehicleType Type, decimal CapacityKg) : IRequest<Result<VehicleDto>>;

public sealed class RegisterVehicleCommandHandler(IVehicleRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterVehicleCommand, Result<VehicleDto>>
{
    public async Task<Result<VehicleDto>> Handle(RegisterVehicleCommand request, CancellationToken cancellationToken)
    {
        Vehicle vehicle;

        try
        {
            vehicle = Vehicle.Register(request.PlateNumber, request.Type, request.CapacityKg);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<VehicleDto>(VehicleErrors.Validation(ex.Message));
        }

        if (await repository.ExistsByPlateNumberAsync(vehicle.PlateNumber, cancellationToken))
        {
            return Result.Failure<VehicleDto>(VehicleErrors.DuplicatePlateNumber);
        }

        repository.Add(vehicle);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicatePlateNumberException)
        {
            // Backstop for the race the pre-check above can't close.
            return Result.Failure<VehicleDto>(VehicleErrors.DuplicatePlateNumber);
        }

        return vehicle.ToDto();
    }
}
