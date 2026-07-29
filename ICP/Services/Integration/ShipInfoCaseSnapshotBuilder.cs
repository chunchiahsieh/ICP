using ICP.Models.Icp;
using ICP.Models.Integration;
using ICP.Models.ShipInfo;
using ICP.Repositories;
using ICP.Services;

namespace ICP.Services.Integration;

public static class ShipInfoCaseSnapshotBuilder
{
    public static ShipInfoCaseEventSnapshot Build(IcpHeader header, IReadOnlyList<IcpDetail> details) =>
        new()
        {
            Header = ShipInfoEntityMapper.MapEntity(header),
            Details = details.Select(ShipInfoEntityMapper.MapEntity).ToList(),
            HeaderSummary = ShipInfoDetailSummaryCalculator.BuildHeaderSummary(header),
            DetailSummary = ShipInfoDetailSummaryCalculator.Calculate(details)
        };
}
