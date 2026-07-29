using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.Integration;

namespace ICP.Services.Integration;

public class ShipInfoCaseEventFactory : IShipInfoCaseEventFactory
{
    public ShipInfoCaseInitiatedEvent Create(
        string headerRowKey,
        string invoiceKey,
        string caseType,
        string caseNo,
        string? oldStatus,
        string? newStatus,
        IcpHeader header,
        IReadOnlyList<IcpDetail> details,
        string? userName)
    {
        return new ShipInfoCaseInitiatedEvent
        {
            MessageId = Guid.NewGuid(),
            EventType = IntegrationEventTypes.ShipInfoCaseInitiated,
            SourceSystem = IntegrationEventTypes.Source,
            TargetSystems = IntegrationEventTypes.ShipInfoCaseDefaultTargets,
            OccurredAt = DateTime.UtcNow,
            CorrelationId = invoiceKey,
            Version = IntegrationEventTypes.EventVersion,
            Payload = new ShipInfoCaseInitiatedPayload
            {
                CaseType = caseType,
                CaseNo = caseNo,
                HeaderKey = headerRowKey,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Actor = new ShipInfoCaseEventActor
                {
                    UserName = CrudAuditHelper.ResolveUserName(userName)
                },
                Snapshot = ShipInfoCaseSnapshotBuilder.Build(header, details)
            }
        };
    }
}
