using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Repositories;

public sealed class ProofOfDeliveryPhotoRepository(ShipmentsDbContext dbContext) : IProofOfDeliveryPhotoRepository
{
    public Task<ProofOfDeliveryPhoto?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken cancellationToken = default) =>
        dbContext.ProofOfDeliveryPhotos
            .AsNoTracking()
            .FirstOrDefaultAsync(photo => photo.Id == shipmentId, cancellationToken);

    public void Add(ProofOfDeliveryPhoto photo) => dbContext.ProofOfDeliveryPhotos.Add(photo);
}
