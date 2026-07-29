using ICP.Models.Icp;
using ICP.Models.Integration;

namespace ICP.Services.Integration;

public interface IShipInfoCaseEventFactory
{
    ShipInfoCaseInitiatedEvent Create(
        string headerRowKey,
        string invoiceKey,
        string caseType,
        string caseNo,
        string? oldStatus,
        string? newStatus,
        IcpHeader header,
        IReadOnlyList<IcpDetail> details,
        string? userName);
}
