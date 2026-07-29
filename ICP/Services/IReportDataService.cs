using ICP.Models.ShipInfo;

namespace ICP.Services;

public interface IReportDataService
{
    ShipInfoPageConfig GetPageConfig(string reportKey);

    Task<ShipInfoTableListViewModel> QueryHeaderTableAsync(
        string reportKey,
        ShipInfoHeaderQueryModel criteria,
        CancellationToken cancellationToken = default);

    Task<ShipInfoTableListViewModel> QueryDetailTableAsync(
        string reportKey,
        ShipInfoDetailQueryModel criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetHeaderFilterOptionsAsync(
        string reportKey,
        string column,
        string? search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetDetailFilterOptionsAsync(
        string reportKey,
        string column,
        string headerKey,
        string? search,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string FileName)> ExportExcelAsync(
        string reportKey,
        ShipInfoHeaderQueryModel criteria,
        string reportDisplayName,
        CancellationToken cancellationToken = default);
}
