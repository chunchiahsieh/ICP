namespace ICP.Models.ShipInfo;

public static class ShipInfoSystemFieldNames
{
    public static readonly string[] ExcludedFromEdit =
    [
        nameof(ShipInfoHeaderRowDto.Id),
        nameof(ShipInfoHeaderRowDto.CreateTime),
        nameof(ShipInfoHeaderRowDto.CreateUser),
        nameof(ShipInfoHeaderRowDto.UpdateTime),
        nameof(ShipInfoHeaderRowDto.UpdateUser)
    ];
}
