using ICP.Models.Icp;
using ICP.Models.ShipInfo;

namespace ICP.Repositories;

public interface IShipInfoRepository
{
    Task<ShipInfoHeaderListResult> SearchHeadersAsync(
        ShipInfoSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Dictionary<string, object?>>> QueryHeadersAsync(
        ShipInfoHeaderQueryModel criteria,
        IReadOnlyList<ShipInfoFieldMetadata> fields,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Dictionary<string, object?>>> QueryDetailsAsync(
        ShipInfoDetailQueryModel criteria,
        IReadOnlyList<ShipInfoFieldMetadata> fields,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetDistinctDetailValuesAsync(
        string column,
        string headerKey,
        string? search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetDistinctHeaderValuesAsync(
        string column,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ShipInfoDetailListResult> GetDetailsByHeaderKeyAsync(
        string headerKey,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsHeaderByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default);

    Task<IcpHeader?> GetHeaderByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IcpHeader?> GetHeaderForUpdateByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IcpHeader?> GetHeaderByInvoiceNoAndTetPoAsync(string invoiceNo, string? tetPo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IcpHeader>> GetHeaderEntitiesByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default);

    Task<IcpDetail?> GetDetailByKeyAsync(string detailKey, CancellationToken cancellationToken = default);

    Task<IcpDetail?> GetDetailForUpdateAsync(string detailKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IcpDetail>> GetDetailEntitiesByHeaderKeyAsync(
        string headerKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IcpDetail>> GetDetailEntitiesForUpdateByInvoiceNoAndTetPoAsync(
        string invoiceNo,
        string tetPo,
        CancellationToken cancellationToken = default);

    Task UpdateHeaderAsync(IcpHeader header, CancellationToken cancellationToken = default);

    Task UpdateDetailAsync(IcpDetail detail, CancellationToken cancellationToken = default);

    Task UpdateHeaderAndDetailsAsync(
        IcpHeader header,
        IReadOnlyList<IcpDetail> details,
        CancellationToken cancellationToken = default);

    Task AddAuditLogsAsync(IEnumerable<ShipInfoAuditLog> logs, CancellationToken cancellationToken = default);

    Task DeleteHeadersByInvoiceAsync(IReadOnlyList<IcpHeader> headers, IReadOnlyList<IcpDetail> details, IReadOnlyList<DeleteLog> logs, string invoiceKey, CancellationToken cancellationToken = default);

    Task DeleteDetailAsync(IcpDetail detail, DeleteLog log, CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
}
