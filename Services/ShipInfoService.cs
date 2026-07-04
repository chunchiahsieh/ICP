using System.Globalization;

using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;
using ICP.Repositories;

namespace ICP.Services;

public class ShipInfoService : IShipInfoService
{
    private readonly IShipInfoRepository _repository;
    private readonly ShipInfoMetadataProvider _metadataProvider;
    private readonly ShipInfoLookupService _lookupService;
    private readonly UserResourcePermissionService _permissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ShipInfoService> _logger;

    public ShipInfoService(
        IShipInfoRepository repository,
        ShipInfoMetadataProvider metadataProvider,
        ShipInfoLookupService lookupService,
        UserResourcePermissionService permissionService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ShipInfoService> logger)
    {
        _repository = repository;
        _metadataProvider = metadataProvider;
        _lookupService = lookupService;
        _permissionService = permissionService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public ShipInfoPageConfig GetPageConfig() =>
        _metadataProvider.GetPageConfig(CultureInfo.CurrentUICulture.Name);

    public Task<IReadOnlyList<ShipInfoLookupOption>> GetLookupOptionsAsync(
        string category,
        CancellationToken cancellationToken = default) =>
        _lookupService.GetOptionsAsync(category, cancellationToken);

    public Task<ShipInfoHeaderListResult> SearchHeadersAsync(
        ShipInfoSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.View);
        LogOperation("QueryHeader", extra: $"Page={criteria.Page},PageSize={criteria.PageSize}");
        return _repository.SearchHeadersAsync(criteria, cancellationToken);
    }

    public async Task<ShipInfoTableListViewModel> QueryHeaderTableAsync(
        ShipInfoHeaderQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.View);
        LogOperation("QueryHeader");
        var config = _metadataProvider.GetPageConfig(CultureInfo.CurrentUICulture.Name);
        var items = await _repository.QueryHeadersAsync(criteria, config.HeaderFields, cancellationToken);
        return new ShipInfoTableListViewModel
        {
            TableId = "shipInfoHeaderTable",
            TableKind = "Header",
            Culture = config.Culture,
            Fields = config.HeaderFields,
            TableUi = config.HeaderTableUi,
            Items = items
        };
    }

    public Task<IReadOnlyList<string>> GetHeaderFilterOptionsAsync(
        string column,
        string? search,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.View);
        var config = _metadataProvider.GetPageConfig(CultureInfo.CurrentUICulture.Name);
        var field = config.HeaderFields.FirstOrDefault(x =>
            string.Equals(x.FieldName, column, StringComparison.OrdinalIgnoreCase));
        if (field is null || !field.Searchable || !ShipInfoMetadataHelper.IsCheckboxFilter(field))
        {
            throw new ShipInfoBusinessException("Filter column is invalid.");
        }

        return _repository.GetDistinctHeaderValuesAsync(column, search, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetDetailFilterOptionsAsync(
        string column,
        string headerKey,
        string? search,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.View);
        if (string.IsNullOrWhiteSpace(headerKey))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var config = _metadataProvider.GetPageConfig(CultureInfo.CurrentUICulture.Name);
        var field = config.DetailFields.FirstOrDefault(x =>
            string.Equals(x.FieldName, column, StringComparison.OrdinalIgnoreCase));
        if (field is null || !field.Searchable || !ShipInfoMetadataHelper.IsCheckboxFilter(field))
        {
            throw new ShipInfoBusinessException("Filter column is invalid.");
        }

        return _repository.GetDistinctDetailValuesAsync(column, headerKey, search, cancellationToken);
    }

    public async Task<ShipInfoTableListViewModel> QueryDetailTableAsync(
        ShipInfoDetailQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.View);
        if (string.IsNullOrWhiteSpace(criteria.HeaderKey))
        {
            return new ShipInfoTableListViewModel
            {
                TableId = "shipInfoDetailTable",
                TableKind = "Detail",
                Culture = CultureInfo.CurrentUICulture.Name,
                Fields = _metadataProvider.GetPageConfig().DetailFields,
                TableUi = _metadataProvider.GetPageConfig().DetailTableUi,
                Items = []
            };
        }

        await RequireHeaderByInvoiceAsync(criteria.HeaderKey, cancellationToken);
        LogOperation("QueryDetail", headerKey: criteria.HeaderKey);
        var config = _metadataProvider.GetPageConfig(CultureInfo.CurrentUICulture.Name);
        var result = await _repository.QueryDetailsAsync(criteria, config.DetailFields, cancellationToken);
        return new ShipInfoTableListViewModel
        {
            TableId = "shipInfoDetailTable",
            TableKind = "Detail",
            Culture = config.Culture,
            Fields = config.DetailFields,
            TableUi = config.DetailTableUi,
            Items = result,
            SelectedHeaderKey = criteria.HeaderKey,
        };
    }

    public async Task<Dictionary<string, object?>> GetHeaderDataAsync(
        string headerRowKey,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.View);
        var header = await RequireHeaderByRowKeyAsync(headerRowKey, cancellationToken);
        LogOperation("QueryHeader", headerKey: ShipInfoKeyHelper.BuildHeaderKey(header));
        return ShipInfoEntityMapper.MapEntity(header);
    }

    public async Task<Dictionary<string, object?>> GetDetailDataAsync(
        string detailKey,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.View);
        var detail = await RequireDetailAsync(detailKey, cancellationToken);
        var headerKey = ShipInfoKeyHelper.BuildHeaderKey(detail.InvoiceNo);
        LogOperation("QueryDetail", headerKey: headerKey, detailKey: detailKey);
        return ShipInfoEntityMapper.MapEntity(detail);
    }

    public async Task<ShipInfoDetailListResult> GetDetailsByHeaderKeyAsync(
        string headerKey,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.View);
        await RequireHeaderByInvoiceAsync(headerKey, cancellationToken);
        LogOperation("QueryDetail", headerKey: headerKey);
        return await _repository.GetDetailsByHeaderKeyAsync(headerKey, cancellationToken);
    }

    public IReadOnlyList<string> ValidateHeaderValues(IReadOnlyDictionary<string, string?> values) =>
        ValidateEditableValues(_metadataProvider.GetPageConfig().HeaderFields, values);

    public IReadOnlyList<string> ValidateDetailValues(IReadOnlyDictionary<string, string?> values) =>
        ValidateEditableValues(_metadataProvider.GetPageConfig().DetailFields, values);

    public async Task<Dictionary<string, object?>> SaveHeaderAsync(
        ShipInfoSaveRequest request,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.Edit);
        var headerRowKey = request.Id;
        if (string.IsNullOrWhiteSpace(headerRowKey))
        {
            throw new ShipInfoBusinessException("Header key is required.");
        }

        var fields = _metadataProvider.GetHeaderEditFields();
        var values = NormalizeHeaderSaveValues(request.Values);
        var header = await _repository.GetHeaderForUpdateByRowKeyAsync(headerRowKey, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Header not found.");

        EnsureStatusAllows(header, permission => permission.Edit, "Header cannot be edited in current status.");
        EnsureConcurrency(header, request.UpdateTime);

        var currentValues = ShipInfoEntityMapper.MapEntity(header)
            .ToDictionary(x => x.Key, x => x.Value?.ToString(), StringComparer.OrdinalIgnoreCase);
        var validationErrors = CollectValidationErrors(fields, values, currentValues);
        if (validationErrors.Count > 0)
        {
            throw new ShipInfoBusinessException(string.Join(' ', validationErrors));
        }

        var changes = ShipInfoEntityMapper.DetectChanges(header, values, fields);
        ShipInfoEntityMapper.ApplyEditableValues(header, values, fields);
        CrudAuditHelper.ApplyUpdateAudit(header, userName);

        await _repository.UpdateHeaderAsync(header, cancellationToken);

        var invoiceKey = ShipInfoKeyHelper.BuildHeaderKey(header);
        await WriteFieldAuditAsync("Header", headerRowKey, invoiceKey, changes, userName, cancellationToken);
        LogOperation("EditHeader", headerKey: invoiceKey);
        return ShipInfoEntityMapper.MapEntity(header);
    }

    public async Task<Dictionary<string, object?>> SaveDetailAsync(
        ShipInfoSaveRequest request,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.Edit);
        var detailKey = request.Id;
        if (string.IsNullOrWhiteSpace(detailKey))
        {
            throw new ShipInfoBusinessException("Detail key is required.");
        }

        var fields = _metadataProvider.GetDetailEditFields();
        var values = NormalizeValues(request.Values);
        var detail = await _repository.GetDetailForUpdateAsync(detailKey, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Detail not found.");

        var headerRowKey = ShipInfoKeyHelper.BuildHeaderRowKey(detail.InvoiceNo, detail.TetPo);
        var headerKey = ShipInfoKeyHelper.BuildHeaderKey(detail.InvoiceNo);
        var header = await _repository.GetHeaderByRowKeyAsync(headerRowKey, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Header not found.");

        EnsureStatusAllows(header, permission => permission.Edit, "Detail cannot be edited in current status.");
        EnsureConcurrency(detail, request.UpdateTime);

        var currentValues = ShipInfoEntityMapper.MapEntity(detail)
            .ToDictionary(x => x.Key, x => x.Value?.ToString(), StringComparer.OrdinalIgnoreCase);
        var validationErrors = CollectValidationErrors(fields, values, currentValues);
        if (validationErrors.Count > 0)
        {
            throw new ShipInfoBusinessException(string.Join(' ', validationErrors));
        }

        var changes = ShipInfoEntityMapper.DetectChanges(detail, values, fields);
        ShipInfoEntityMapper.ApplyEditableValues(detail, values, fields);
        CrudAuditHelper.ApplyUpdateAudit(detail, userName);

        await _repository.UpdateDetailAsync(detail, cancellationToken);

        await WriteFieldAuditAsync("Detail", detailKey, headerKey, changes, userName, cancellationToken);
        LogOperation("EditDetail", headerKey: headerKey, detailKey: detailKey);
        return ShipInfoEntityMapper.MapEntity(detail);
    }

    public async Task DeleteHeaderAsync(string headerRowKey, string? userName, CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.Delete);
        var header = await RequireHeaderByRowKeyAsync(headerRowKey, cancellationToken);
        EnsureStatusAllows(header, permission => permission.Delete, "Header cannot be deleted in current status.");

        var invoiceKey = ShipInfoKeyHelper.BuildHeaderKey(header);
        var auditLog = CreateAuditLog("Header", headerRowKey, invoiceKey, "Delete", userName);

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            await _repository.AddAuditLogsAsync([auditLog], cancellationToken);
            await _repository.DeleteHeaderWithDetailsAsync(headerRowKey, cancellationToken);
        }, cancellationToken);

        LogOperation("DeleteHeader", headerKey: invoiceKey);
    }

    public async Task DeleteDetailAsync(string detailKey, string? userName, CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.Delete);
        var detail = await RequireDetailAsync(detailKey, cancellationToken);
        var headerRowKey = ShipInfoKeyHelper.BuildHeaderRowKey(detail.InvoiceNo, detail.TetPo);
        var headerKey = ShipInfoKeyHelper.BuildHeaderKey(detail.InvoiceNo);
        var header = await RequireHeaderByRowKeyAsync(headerRowKey, cancellationToken);
        EnsureStatusAllows(header, permission => permission.Delete, "Detail cannot be deleted in current status.");

        var detailCount = await _repository.CountDetailsByInvoiceNoAsync(detail.InvoiceNo, cancellationToken);
        if (detailCount <= 1)
        {
            throw new ShipInfoBusinessException("At least one detail row must remain.");
        }

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            await _repository.AddAuditLogsAsync(
            [
                CreateAuditLog("Detail", detailKey, headerKey, "Delete", userName)
            ], cancellationToken);

            await _repository.DeleteDetailAsync(detailKey, cancellationToken);
        }, cancellationToken);

        LogOperation("DeleteDetail", headerKey: headerKey, detailKey: detailKey);
    }

    public async Task<ShipInfoCaseDrawerData> GetCaseDrawerDataAsync(
        string headerRowKey,
        string caseType,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(ShipInfoPermissionCodes.View);
        var normalizedCaseType = NormalizeCaseType(caseType);
        EnsureCasePermission(normalizedCaseType);
        var header = await RequireHeaderByRowKeyAsync(headerRowKey, cancellationToken);
        var invoiceKey = ShipInfoKeyHelper.BuildHeaderKey(header);
        var details = await _repository.GetDetailEntitiesByHeaderKeyAsync(invoiceKey, cancellationToken);
        var validationMessages = ValidateCaseCreation(header, details, normalizedCaseType, previewOnly: true);

        LogOperation("QueryCaseDrawer", headerKey: invoiceKey, extra: normalizedCaseType);

        return new ShipInfoCaseDrawerData
        {
            HeaderKey = headerRowKey,
            CaseType = normalizedCaseType,
            HeaderSummary = ShipInfoDetailSummaryCalculator.BuildHeaderSummary(header),
            Header = ShipInfoEntityMapper.MapEntity(header),
            DetailSummary = ShipInfoDetailSummaryCalculator.Calculate(details),
            Details = details.Select(ShipInfoEntityMapper.MapEntity).ToList(),
            CanSubmit = validationMessages.Count == 0,
            ValidationMessages = validationMessages
        };
    }

    public Task<ShipInfoCaseCreateResult> CreateDepositCaseAsync(
        string headerRowKey,
        string? userName,
        CancellationToken cancellationToken = default) =>
        CreateCaseAsync(headerRowKey, ShipInfoCaseTypes.Deposit, userName, cancellationToken);

    public Task<ShipInfoCaseCreateResult> CreateArurCaseAsync(
        string headerRowKey,
        string? userName,
        CancellationToken cancellationToken = default) =>
        CreateCaseAsync(headerRowKey, ShipInfoCaseTypes.Arur, userName, cancellationToken);

    private async Task<ShipInfoCaseCreateResult> CreateCaseAsync(
        string headerRowKey,
        string caseType,
        string? userName,
        CancellationToken cancellationToken)
    {
        EnsureCasePermission(caseType);
        var header = await _repository.GetHeaderForUpdateByRowKeyAsync(headerRowKey, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Header not found.");

        var invoiceKey = ShipInfoKeyHelper.BuildHeaderKey(header);
        var details = (await _repository.GetDetailEntitiesByHeaderKeyAsync(invoiceKey, cancellationToken)).ToList();
        var validationMessages = ValidateCaseCreation(header, details, caseType, previewOnly: false);
        if (validationMessages.Count > 0)
        {
            throw new ShipInfoBusinessException(string.Join(' ', validationMessages));
        }

        var oldStatus = ShipInfoStatusResolver.Resolve(header);
        ApplyCaseStatus(header, details, caseType, ShipInfoCaseStatuses.Processing, userName);

        try
        {
            await _repository.ExecuteInTransactionAsync(async () =>
            {
                await _repository.UpdateHeaderAndDetailsAsync(header, details, cancellationToken);
            }, cancellationToken);

            var caseNo = GenerateCaseNo(caseType, header.InvoiceNo, header.TetPo);
            if (caseType == ShipInfoCaseTypes.Deposit)
            {
                header.Deposit = caseNo;
            }
            else
            {
                header.RtNo = caseNo;
            }

            ApplyCaseStatus(header, details, caseType, ShipInfoCaseStatuses.Initiated, userName);
            CrudAuditHelper.ApplyUpdateAudit(header, userName);
            foreach (var detail in details)
            {
                CrudAuditHelper.ApplyUpdateAudit(detail, userName);
            }

            var newStatus = ShipInfoStatusResolver.Resolve(header);
            var auditLog = CreateAuditLog(
                "Header",
                headerRowKey,
                invoiceKey,
                "CreateCase",
                userName,
                caseType: caseType,
                caseNo: caseNo,
                oldStatus: oldStatus,
                newStatus: newStatus);

            await _repository.ExecuteInTransactionAsync(async () =>
            {
                await _repository.UpdateHeaderAndDetailsAsync(header, details, cancellationToken);
                await _repository.AddAuditLogsAsync([auditLog], cancellationToken);
            }, cancellationToken);

            LogOperation(caseType == ShipInfoCaseTypes.Deposit ? "Deposit" : "ARUR", headerKey: invoiceKey, extra: caseNo);

            return new ShipInfoCaseCreateResult
            {
                HeaderKey = headerRowKey,
                CaseType = caseType,
                DepositNo = caseType == ShipInfoCaseTypes.Deposit ? caseNo : header.Deposit,
                ArurNo = caseType == ShipInfoCaseTypes.Arur ? caseNo : header.RtNo,
                NewStatus = newStatus
            };
        }
        catch (Exception ex) when (ex is not ShipInfoBusinessException and not ShipInfoNotFoundException)
        {
            ApplyCaseStatus(header, details, caseType, ShipInfoCaseStatuses.Failed, userName);
            foreach (var detail in details)
            {
                CrudAuditHelper.ApplyUpdateAudit(detail, userName);
            }

            CrudAuditHelper.ApplyUpdateAudit(header, userName);
            await _repository.ExecuteInTransactionAsync(async () =>
            {
                await _repository.UpdateHeaderAndDetailsAsync(header, details, cancellationToken);
            }, cancellationToken);

            throw;
        }
    }

    private static void ApplyCaseStatus(
        IcpHeader header,
        IReadOnlyList<IcpDetail> details,
        string caseType,
        string caseStatus,
        string? userName)
    {
        var normalized = ShipInfoCaseStatusResolver.Normalize(caseStatus);
        if (caseType == ShipInfoCaseTypes.Deposit)
        {
            header.DepositCaseStatus = normalized;
            foreach (var detail in details)
            {
                detail.DepositCaseStatus = normalized;
            }
        }
        else
        {
            header.ArurCaseStatus = normalized;
            foreach (var detail in details)
            {
                detail.ArurCaseStatus = normalized;
            }
        }
    }

    private static string NormalizeCaseType(string caseType)
    {
        if (caseType.Equals(ShipInfoCaseTypes.Deposit, StringComparison.OrdinalIgnoreCase))
        {
            return ShipInfoCaseTypes.Deposit;
        }

        if (caseType.Equals(ShipInfoCaseTypes.Arur, StringComparison.OrdinalIgnoreCase)
            || caseType.Equals("Arur", StringComparison.OrdinalIgnoreCase))
        {
            return ShipInfoCaseTypes.Arur;
        }

        throw new ShipInfoBusinessException("Case type is invalid.");
    }

    private static string GenerateCaseNo(string caseType, string invoiceNo, string? _)
    {
        var prefix = caseType == ShipInfoCaseTypes.Deposit ? "DEP" : "ARUR";
        var maxLength = caseType == ShipInfoCaseTypes.Deposit
            ? IcpHeader.DepositMaxLength
            : IcpHeader.RtNoMaxLength;
        var normalizedInvoice = (invoiceNo ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalizedInvoice))
        {
            throw new ShipInfoBusinessException("Invoice number is required to generate a case number.");
        }

        var caseNo = $"{prefix}-{normalizedInvoice}";
        return caseNo.Length <= maxLength ? caseNo : caseNo[..maxLength];
    }

    private static IReadOnlyList<string> ValidateCaseCreation(
        IcpHeader header,
        IReadOnlyList<IcpDetail> details,
        string caseType,
        bool previewOnly)
    {
        var errors = new List<string>();
        var status = ShipInfoStatusResolver.Resolve(header);
        var permission = ShipInfoStatusRules.Resolve(status);

        if (caseType == ShipInfoCaseTypes.Deposit)
        {
            if (!permission.Deposit)
            {
                errors.Add("Deposit case cannot be created in current status.");
            }

            if (!ShipInfoCaseStatusResolver.CanCreateCase(header.DepositCaseStatus))
            {
                errors.Add("Deposit case cannot be created in current case status.");
            }
        }
        else
        {
            if (!permission.Arur)
            {
                errors.Add("ARUR case cannot be created in current status.");
            }

            if (!ShipInfoCaseStatusResolver.CanCreateCase(header.ArurCaseStatus))
            {
                errors.Add("ARUR case cannot be created in current case status.");
            }
        }

        if (string.IsNullOrWhiteSpace(header.InvoiceNo))
        {
            errors.Add("Invoice No is required.");
        }

        if (!previewOnly && errors.Count > 0)
        {
            return errors;
        }

        return errors;
    }

    private IReadOnlyList<string> ValidateEditableValues(
        IReadOnlyList<ShipInfoFieldMetadata> fields,
        IReadOnlyDictionary<string, string?> values) =>
        ShipInfoMetadataHelper.ValidateFieldValues(fields, values, validateEditableOnly: true);

    private static Dictionary<string, string?> NormalizeValues(IReadOnlyDictionary<string, string?> values) =>
        values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string?> NormalizeHeaderSaveValues(IReadOnlyDictionary<string, string?> values)
    {
        var normalized = NormalizeValues(values);
        if (!normalized.ContainsKey("SaDate") && normalized.TryGetValue("SaDateFrom", out var saDateFrom))
        {
            normalized["SaDate"] = saDateFrom;
        }

        if (!normalized.ContainsKey("Eta") && normalized.TryGetValue("EtaFrom", out var etaFrom))
        {
            normalized["Eta"] = etaFrom;
        }

        return normalized;
    }

    private static IReadOnlyList<string> CollectValidationErrors(
        IReadOnlyList<ShipInfoFieldMetadata> fields,
        IReadOnlyDictionary<string, string?> submittedValues,
        IReadOnlyDictionary<string, string?> currentValues)
    {
        var errors = new List<string>();
        errors.AddRange(ShipInfoMetadataHelper.ValidateFieldValues(fields, submittedValues, validateEditableOnly: true));
        errors.AddRange(ShipInfoMetadataHelper.DetectNonEditableChanges(fields, submittedValues, currentValues));
        return errors;
    }

    private static void EnsureConcurrency(IcpAuditableEntity entity, string? submittedUpdateTime)
    {
        if (string.IsNullOrWhiteSpace(submittedUpdateTime) || !entity.UpdateTime.HasValue)
        {
            return;
        }

        if (!DateTime.TryParse(submittedUpdateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var submitted)
            && !DateTime.TryParse(submittedUpdateTime, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out submitted))
        {
            return;
        }

        var current = entity.UpdateTime.Value;
        if (Math.Abs((current.ToUniversalTime() - submitted.ToUniversalTime()).TotalSeconds) > 1)
        {
            throw new ShipInfoBusinessException("Data has been updated by another user. Please refresh and try again.");
        }
    }

    private static void EnsureStatusAllows(
        IcpHeader header,
        Func<ShipInfoActionPermission, bool> selector,
        string message)
    {
        var permission = ShipInfoStatusRules.Resolve(ShipInfoStatusResolver.Resolve(header));
        if (!selector(permission))
        {
            throw new ShipInfoBusinessException(message);
        }
    }

    private async Task WriteFieldAuditAsync(
        string entityType,
        string entityKey,
        string headerKey,
        IReadOnlyList<FieldChange> changes,
        string? userName,
        CancellationToken cancellationToken)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var actor = CrudAuditHelper.ResolveUserName(userName);
        var now = DateTime.Now;
        var logs = changes.Select(change =>
        {
            var log = new ShipInfoAuditLog
            {
                EntityType = entityType,
                EntityKey = entityKey,
                HeaderKey = headerKey,
                Action = "Update",
                FieldName = change.FieldName,
                OldValue = change.OldValue,
                NewValue = change.NewValue,
                UserName = actor,
                ActionTime = now
            };
            CrudAuditHelper.ApplyCreateAudit(log, userName);
            return log;
        });

        await _repository.AddAuditLogsAsync(logs, cancellationToken);
    }

    private static ShipInfoAuditLog CreateAuditLog(
        string entityType,
        string entityKey,
        string? headerKey,
        string action,
        string? userName,
        string? caseType = null,
        string? caseNo = null,
        string? oldStatus = null,
        string? newStatus = null)
    {
        var log = new ShipInfoAuditLog
        {
            EntityType = entityType,
            EntityKey = entityKey,
            HeaderKey = headerKey,
            Action = action,
            CaseType = caseType,
            CaseNo = caseNo,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            UserName = CrudAuditHelper.ResolveUserName(userName),
            ActionTime = DateTime.Now
        };
        CrudAuditHelper.ApplyCreateAudit(log, userName);
        return log;
    }

    private async Task<IcpHeader> RequireHeaderByRowKeyAsync(string headerRowKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(headerRowKey))
        {
            throw new ShipInfoBusinessException("Header row key is required.");
        }

        var header = await _repository.GetHeaderByRowKeyAsync(headerRowKey, cancellationToken);
        if (header is null)
        {
            throw new ShipInfoNotFoundException("Header not found.");
        }

        return header;
    }

    private async Task RequireHeaderByInvoiceAsync(string headerKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(headerKey))
        {
            throw new ShipInfoBusinessException("Header key is required.");
        }

        var invoiceNo = ShipInfoKeyHelper.ParseInvoiceNo(headerKey);
        if (!await _repository.ExistsHeaderByInvoiceNoAsync(invoiceNo, cancellationToken))
        {
            throw new ShipInfoNotFoundException("Header not found.");
        }
    }

    private async Task<IcpDetail> RequireDetailAsync(string detailKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(detailKey))
        {
            throw new ShipInfoBusinessException("Detail key is required.");
        }

        var detail = await _repository.GetDetailByKeyAsync(detailKey, cancellationToken);
        if (detail is null)
        {
            throw new ShipInfoNotFoundException("Detail not found.");
        }

        return detail;
    }

    private void EnsurePermission(string resourceCode)
    {
        if (!_permissionService.HasPermission(resourceCode))
        {
            throw new ShipInfoForbiddenException("Permission denied.");
        }
    }

    private void EnsureCasePermission(string caseType)
    {
        if (caseType == ShipInfoCaseTypes.Deposit)
        {
            EnsurePermission(ShipInfoPermissionCodes.Deposit);
            return;
        }

        EnsurePermission(ShipInfoPermissionCodes.Arur);
    }

    private void LogOperation(
        string operation,
        string? headerKey = null,
        string? detailKey = null,
        string? extra = null)
    {
        var context = _httpContextAccessor.HttpContext;
        var user = context?.User?.Identity?.Name;
        var ip = context?.Connection?.RemoteIpAddress?.ToString();
        var browser = context?.Request.Headers.UserAgent.ToString();

        _logger.LogInformation(
            "ShipInfo {Operation} User={User} IP={IP} Browser={Browser} HeaderKey={HeaderKey} DetailKey={DetailKey} Extra={Extra}",
            operation,
            user,
            ip,
            browser,
            headerKey,
            detailKey,
            extra);
    }
}
