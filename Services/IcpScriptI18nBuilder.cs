using Microsoft.Extensions.Localization;

namespace ICP.Services;

public static class IcpScriptI18nBuilder
{
    public static Dictionary<string, string> Build(IStringLocalizer<SharedResource> localizer) =>
        new(StringComparer.Ordinal)
        {
            ["selectedCount"] = localizer["Js.SelectedCount"],
            ["none"] = localizer["Common.None"],
            ["selectAtLeastOneRole"] = localizer["Validation.SelectAtLeastOneRole"],
            ["selectAtLeastOneResource"] = localizer["Validation.SelectAtLeastOneResource"],
            ["selectAtLeastOneUser"] = localizer["Validation.SelectAtLeastOneUser"],
            ["selectAtLeastOneDepartment"] = localizer["Validation.SelectAtLeastOneDepartment"],
            ["selectAtLeastOneMailGroup"] = localizer["Validation.SelectAtLeastOneMailGroup"],
            ["selectAtLeastOneRoleAndResource"] = localizer["Validation.SelectAtLeastOneRoleAndResource"],
            ["selectAtLeastOneRoleAndUser"] = localizer["Validation.SelectAtLeastOneRoleAndUser"],
            ["selectAtLeastOneRecord"] = localizer["Validation.SelectAtLeastOneRecord"],
            ["batchCreateSuccess"] = localizer["Message.BatchCreateSuccess"],
            ["createFailed"] = localizer["Message.CreateFailed"],
            ["createFailedRetry"] = localizer["Message.CreateFailedRetry"],
            ["deleteFailed"] = localizer["Message.DeleteFailed"],
            ["deleteFailedRetry"] = localizer["Message.DeleteFailedRetry"],
            ["deleteSuccess"] = localizer["Message.DeleteSuccess"],
            ["operationFailed"] = localizer["Message.OperationFailed"],
            ["operationFailedRetry"] = localizer["Message.OperationFailedRetry"],
            ["saveFailedRetry"] = localizer["Message.SaveFailedRetry"],
            ["loadFailed"] = localizer["Message.LoadFailed"],
            ["pleaseSelect"] = localizer["Common.PleaseSelect"],
            ["create"] = localizer["Common.Create"],
            ["edit"] = localizer["Common.Edit"],
            ["disableConfirm"] = localizer["Message.DisableConfirm"],
            ["deleteConfirm"] = localizer["Message.DeleteConfirm"],
            ["permissionAccessDenied"] = localizer["Permission.AccessDenied"]
        };

    public static object BuildDataTablesLengthMenu(IStringLocalizer<SharedResource> localizer)
    {
        var format = localizer["DataTables.LengthMenuItem"].Value;
        int[] values = [10, 25, 50, 100];
        var labels = values.Select(v => string.Format(format, v)).ToArray();
        return new object[] { values, labels };
    }

    public static object BuildDataTablesLanguage(IStringLocalizer<SharedResource> localizer) =>
        new
        {
            emptyTable = localizer["DataTables.EmptyTable"].Value,
            info = localizer["DataTables.Info"].Value,
            infoEmpty = localizer["DataTables.InfoEmpty"].Value,
            infoFiltered = localizer["DataTables.InfoFiltered"].Value,
            lengthMenu = localizer["DataTables.LengthMenu"].Value,
            loadingRecords = localizer["DataTables.LoadingRecords"].Value,
            processing = localizer["DataTables.Processing"].Value,
            search = localizer["DataTables.Search"].Value,
            zeroRecords = localizer["DataTables.ZeroRecords"].Value,
            paginate = new
            {
                first = localizer["DataTables.Paginate.First"].Value,
                last = localizer["DataTables.Paginate.Last"].Value,
                next = localizer["DataTables.Paginate.Next"].Value,
                previous = localizer["DataTables.Paginate.Previous"].Value
            }
        };
}
