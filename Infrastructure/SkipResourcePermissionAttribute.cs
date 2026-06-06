namespace ICP.Infrastructure;

/// <summary>略過 RequireResourcePermissionFilter 的 Route 權限檢查。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipResourcePermissionAttribute : Attribute;
