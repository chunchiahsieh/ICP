namespace ICP.Models.ShipInfo;

/// <summary>DTO 欄位對應的實體屬性名稱（名稱不同時使用）。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ShipInfoMapsToEntityAttribute(string entityPropertyName) : Attribute
{
    public string EntityPropertyName { get; } = entityPropertyName;
}

/// <summary>僅由 DTO 計算、不直接對應單一實體欄位。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ShipInfoComputedAttribute : Attribute;
