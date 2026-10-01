using System.ComponentModel.DataAnnotations;
using ICP.Models.Icp;

namespace ICP.Models.Tariff;

public sealed class TariffLogRemarksEditModel
{
    [Range(1, long.MaxValue)]
    public long Id { get; set; }

    [MaxLength(TariffData.LogRemarksMaxLength)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? LOGRemarks { get; set; }

    [MaxLength(TariffData.LogRemarksMaxLength)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? OriginalLOGRemarks { get; set; }
}
