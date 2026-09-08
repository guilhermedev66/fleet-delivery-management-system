namespace FleetDelivery.Modules.Shipments.Application.Abstractions;

/// <summary>
/// Thrown by Infrastructure when two concurrent Proof of Delivery uploads
/// for the same shipment race each other. <see cref="Domain.Shipment.AttachProofOfDelivery"/>'s
/// in-memory <c>HasProofOfDelivery</c> check (and the matching
/// <see cref="Domain.ProofOfDeliveryAlreadyAttachedException"/>) is the fast
/// path for the common case; it isn't itself protected by the optimistic
/// concurrency token (attaching a photo doesn't bump <c>Version</c>), so two
/// requests loading the shipment at the same instant can both pass that
/// check. The <c>proof_of_delivery_photos</c> table's primary key
/// (<c>shipment_id</c> — a photo is 1:1 with its shipment) is the real,
/// race-safe backstop: the losing insert hits a Postgres unique-violation on
/// that key, translated here into this exception so
/// <c>AttachProofOfDeliveryCommandHandler</c> can map it to the same 409 the
/// fast path returns, instead of letting it surface as an unhandled 500.
/// </summary>
public sealed class ProofOfDeliveryPhotoAlreadyExistsException(Exception innerException)
    : Exception("A Proof of Delivery photo was already attached to this shipment.", innerException)
{
}
