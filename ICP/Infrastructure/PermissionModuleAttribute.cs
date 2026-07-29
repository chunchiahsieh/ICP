namespace ICP.Infrastructure;

/// <summary>標記 View 位於 Views/Permission/{ControllerName}/。</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PermissionModuleAttribute : Attribute;
