using ICP.Models.ShipInfo;

namespace ICP.Services;

public interface IShipInfoService
{
    ShipInfoPageConfig GetPageConfig();

    Task<IReadOnlyList<ShipInfoLookupOption>> GetLookupOptionsAsync(
        string category,
        CancellationToken cancellationToken = default);

    Task<ShipInfoHeaderListResult> SearchHeadersAsync(
        ShipInfoSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<ShipInfoTableListViewModel> QueryHeaderTableAsync(
        ShipInfoHeaderQueryModel criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetHeaderFilterOptionsAsync(
        string column,
        string? search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetDetailFilterOptionsAsync(
        string column,
        string headerKey,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ShipInfoTableListViewModel> QueryDetailTableAsync(
        ShipInfoDetailQueryModel criteria,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> GetHeaderDataAsync(
        string headerKey,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> GetDetailDataAsync(
        string detailKey,
        CancellationToken cancellationToken = default);

    Task<ShipInfoDetailListResult> GetDetailsByHeaderKeyAsync(
        string headerKey,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> ValidateHeaderValues(IReadOnlyDictionary<string, string?> values);

    IReadOnlyList<string> ValidateDetailValues(IReadOnlyDictionary<string, string?> values);

    Task<Dictionary<string, object?>> SaveHeaderAsync(
        ShipInfoSaveRequest request,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> SaveDetailAsync(
        ShipInfoSaveRequest request,
        string? userName,
        CancellationToken cancellationToken = default);

    Task DiscardHeaderAsync(string headerKey, string? reason, string? userName, CancellationToken cancellationToken = default);

    Task<ShipInfoCaseDrawerData> GetCaseDrawerDataAsync(
        string headerKey,
        string caseType,
        CancellationToken cancellationToken = default);

    Task<ShipInfoCaseCreateResult> CreateDepositCaseAsync(
        string headerKey,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<ShipInfoCaseCreateResult> CreateArurCaseAsync(
        string headerKey,
        string? userName,
        CancellationToken cancellationToken = default);
}
