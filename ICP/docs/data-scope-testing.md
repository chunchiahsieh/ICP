# DataScope: four-page verification

Implemented pages and View resources:

| Page | Resource | Table |
| --- | --- | --- |
| ShipInfo | Views.Function.ShipInfo.View | ICP_HEADER |
| Shipping Report | Views.Report.ShippingReport.View | ICP_HEADER |
| Mass Data Report | Views.Report.MassDataReport.View | ICP_HEADER |
| Tariff Data | Views.Broker.TariffData.View | TariffData |

Each page reads its own View DataScopes from Session. Export permissions still control whether export is allowed; the page's View scope controls the exported rows. Compare ICP vs ARUR and Forwarder Data Upload are not registered with the new scope service.

1. Stop the existing IIS Express/debug session, rebuild and restart the application so the running process loads the modified DLL.
2. If not already applied, run sql/002_ExpandRolePermissionDataScope.sql against the intended ICP database before saving JSON scopes. The application does not run this script automatically.
3. Set a role permission for ShipInfo View: ICP_HEADER / MDP_FLAG / Equal / Y. Assign that role to the test user, then reload permissions as that user.
4. Open ShipInfo with UI filters cleared: only MDP Flag Y should remain; N and blank must be absent. Header filter choices must also come from the restricted rows.
5. Using a previously recorded out-of-scope header/detail key, call GetHeader, GetDetail, QueryDetail and attachment endpoints: they must not return the excluded data/files. A missing-row response or empty list is expected depending on the endpoint.
6. Set separate View scopes for Shipping Report (ICP_HEADER / BU / Equal / CT) and Mass Data Report (ICP_HEADER / BROKER / In / actual broker values). Reload permissions; check lists, detail rows, filter choices and DownloadExcel. Each report must follow its own scope, not ShipInfo's.
7. Set Tariff Data View to TariffData / Broker / In / actual broker values. Reload permissions; check list, filters, export and a direct attachment download for an excluded HAWB.
8. Remove the scope and reload permissions: normal unrestricted rows return. Per the selected policy, any granted role with null/ALL or an invalid/unapplicable scope also makes the page unrestricted. Missing data scope does not grant the page's access permission.

Database column names (MDP_FLAG, BROKER, BU) are resolved using EF metadata. Equal and In use the mapped field type; Contains is supported for strings. Multiple conditions use AND, values within a condition use OR, and role scopes use OR.

Automated checks (no live DB connection):

```powershell
dotnet run --project tests/DataScopeChecks/DataScopeChecks.csproj -p:OutputPath='C:/Disk F/Aga/EY/TEL/ICP/Projects/.buildcheck/data-scope-tests/'
```

Checks cover MDP_FLAG Y/N/null, operators, condition and role merging, invalid/unrestricted fallback, SQL Server translation, independent page scopes, detail/header association and excluded pages.
