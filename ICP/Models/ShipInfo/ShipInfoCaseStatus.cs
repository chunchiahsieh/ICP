namespace ICP.Models.ShipInfo;

/// <summary>押金／ARUR 起案狀態。</summary>
public enum ShipInfoCaseStatus
{
    /// <summary>未起案</summary>
    NotInitiated,

    /// <summary>起案失敗</summary>
    Failed,

    /// <summary>起案中</summary>
    Processing,

    /// <summary>已起案</summary>
    Initiated
}
