(function (global, $) {
    'use strict';

    if (!$ || !global.ShipInfoRender) {
        return;
    }

    var app = global.ShipInfoApp = global.ShipInfoApp || {};
    var config = global.ShipInfoPage || {};

    app.renderApi = global.ShipInfoRender;
    app.config = config;
    app.urls = config.urls || {};
    app.messages = config.messages || {};
    app.state = {
        pageConfig: null,
        selectedHeaderKey: null,
        selectedHeaderRowKey: null,
        selectedHeaderRow: null,
        selectedDetailId: null,
        selectedDetailRow: null,
        editingHeaderId: null,
        editingDetailId: null,
        headerLoading: false,
        detailLoading: false,
        actionBusy: false,
        page: 1,
        pageSize: 50,
        totalCount: 0,
        detailItems: [],
        detailRowCount: 0,
        viewModalKind: null,
        viewModalKey: null,
        viewModalData: null,
        viewModalEditing: false,
        headerFormEffectiveFields: [],
        detailFormEffectiveFields: [],
        caseType: null,
        caseDrawerData: null,
        caseSubmitting: false,
        caseHeaderRowKey: null
    };

    var tooltipInstances = [];

    app.hasPermission = function (code) {
        var permissionConfig = global.IcpPermissions || { superUser: false, allowedCodes: [] };
        if (permissionConfig.superUser) {
            return true;
        }

        var normalized = (code || '').toLowerCase();
        return (permissionConfig.allowedCodes || []).some(function (allowed) {
            return (allowed || '').toLowerCase() === normalized;
        });
    };

    app.disposeTooltips = function () {
        tooltipInstances.forEach(function (instance) {
            if (instance && typeof instance.dispose === 'function') {
                instance.dispose();
            }
        });
        tooltipInstances = [];
    };

    app.initTooltips = function ($scope) {
        if (!global.bootstrap || !global.bootstrap.Tooltip) {
            return;
        }

        ($scope || $(document)).find('[data-bs-toggle="tooltip"]').each(function () {
            tooltipInstances.push(new global.bootstrap.Tooltip(this));
        });
    };

    app.showToast = function (message, type) {
        var messages = app.messages;
        var toastType = type || 'info';
        var bgClassMap = {
            success: 'text-bg-success',
            danger: 'text-bg-danger',
            warning: 'text-bg-warning',
            info: 'text-bg-info'
        };
        var bgClass = bgClassMap[toastType] || bgClassMap.info;
        var $container = $('#shipInfoToastContainer');
        var toastId = 'shipinfo-toast-' + Date.now();
        var html = ''
            + '<div id="' + toastId + '" class="toast align-items-center ' + bgClass + ' border-0" role="alert" aria-live="assertive" aria-atomic="true">'
            + '  <div class="d-flex">'
            + '    <div class="toast-body">' + $('<div>').text(message || '').html() + '</div>'
            + '    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>'
            + '  </div>'
            + '</div>';

        $container.append(html);
        var toastElement = document.getElementById(toastId);
        if (!toastElement || !global.bootstrap || !global.bootstrap.Toast) {
            return;
        }

        var toast = global.bootstrap.Toast.getOrCreateInstance(toastElement, { delay: 3500 });
        toast.show();
        toastElement.addEventListener('hidden.bs.toast', function () {
            toastElement.remove();
        });
    };

    app.getCulture = function () {
        var state = app.state;
        return (state.pageConfig && (state.pageConfig.culture || state.pageConfig.Culture)) || 'zh-TW';
    };

    app.getRenderOptions = function (values) {
        return {
            culture: app.getCulture(),
            lookupUrl: app.urls.lookupOptions,
            loadingText: app.messages.loading,
            requiredMark: app.messages.requiredMark,
            hasPermission: app.hasPermission,
            values: values || {}
        };
    };

    app.getRowValue = function (row, names) {
        if (!row) {
            return undefined;
        }

        for (var i = 0; i < names.length; i++) {
            var name = names[i];
            if (row[name] !== undefined && row[name] !== null) {
                return row[name];
            }
        }

        return undefined;
    };

    app.isTruthyFlag = function (value) {
        return value === true || value === 1 || value === '1' || String(value).toLowerCase() === 'true' || String(value).toLowerCase() === 'y';
    };

    app.normalizeCaseStatus = function (value) {
        var normalized = String(value || '').trim();
        if (!normalized) {
            return 'NotInitiated';
        }

        var lower = normalized.toLowerCase();
        if (lower === 'notinitiated' || normalized === '未起案') {
            return 'NotInitiated';
        }

        if (lower === 'initiated' || normalized === '已起案') {
            return 'Initiated';
        }

        if (lower === 'failed' || normalized === '起案失敗') {
            return 'Failed';
        }

        if (lower === 'processing' || normalized === '起案中' || normalized === '處理中') {
            return 'Processing';
        }

        return normalized;
    };

    app.canCreateCase = function (status) {
        var code = app.normalizeCaseStatus(status);
        return code === 'NotInitiated' || code === 'Failed';
    };

    app.canResendOutbox = function (caseStatus, outboxFailed) {
        return app.normalizeCaseStatus(caseStatus) === 'Initiated' && app.isTruthyFlag(outboxFailed);
    };

    app.canEnableCaseAction = function (caseStatus, outboxFailed) {
        return app.canCreateCase(caseStatus) || app.canResendOutbox(caseStatus, outboxFailed);
    };

    app.getDepositCaseStatus = function (row) {
        return app.normalizeCaseStatus(app.getRowValue(row, ['DepositCaseStatus', 'depositCaseStatus']));
    };

    app.getArurCaseStatus = function (row) {
        return app.normalizeCaseStatus(app.getRowValue(row, ['ArurCaseStatus', 'arurCaseStatus']));
    };

    app.getDepositOutboxFailed = function (row) {
        return app.isTruthyFlag(app.getRowValue(row, ['DepositOutboxFailed', 'depositOutboxFailed']));
    };

    app.getArurOutboxFailed = function (row) {
        return app.isTruthyFlag(app.getRowValue(row, ['ArurOutboxFailed', 'arurOutboxFailed']));
    };

    app.formatCaseStatusLabel = function (value) {
        var messages = app.messages;
        var code = app.normalizeCaseStatus(value);
        var map = {
            NotInitiated: messages.caseStatusNotInitiated,
            Initiated: messages.caseStatusInitiated,
            Failed: messages.caseStatusFailed,
            Processing: messages.caseStatusProcessing
        };

        return map[code] || value || messages.caseStatusNotInitiated;
    };

    app.getHeaderStatus = function (row) {
        return app.getRowValue(row, ['Status', 'status']) || '';
    };

    app.getStatusPermission = function (status) {
        var state = app.state;
        var rules = (state.pageConfig && (state.pageConfig.statusRules || state.pageConfig.StatusRules)) || {};
        var key = status || '';
        var permission = rules[key];
        if (permission) {
            return {
                edit: permission.edit !== false && permission.Edit !== false,
                delete: permission.delete !== false && permission.Delete !== false,
                deposit: permission.deposit !== false && permission.Deposit !== false,
                arur: permission.arur !== false && permission.Arur !== false
            };
        }

        return { edit: true, delete: true, deposit: true, arur: true };
    };

    app.setHeaderLoading = function (isLoading) {
        app.state.headerLoading = isLoading;
        app.updateBusyState();
    };

    app.setDetailLoading = function (isLoading) {
        app.state.detailLoading = isLoading;
        app.updateBusyState();
    };

    app.setActionBusy = function (isBusy) {
        app.state.actionBusy = isBusy;
        app.updateBusyState();
    };

    app.updateBusyState = function () {
        var state = app.state;
        var busy = state.headerLoading || state.detailLoading || state.actionBusy;
        $('.shipinfo-page').toggleClass('shipinfo-busy', busy);
        app.updateHeaderActionState();
    };

    app.setButtonLoading = function ($btn, isLoading) {
        var messages = app.messages;
        if (!$btn || !$btn.length) {
            return;
        }

        if (isLoading) {
            if (!$btn.data('orig-html')) {
                $btn.data('orig-html', $btn.html());
            }

            $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>' + (messages.loading || ''));
            return;
        }

        $btn.prop('disabled', false).html($btn.data('orig-html') || $btn.html());
        $btn.removeData('orig-html');
    };

    app.getDetailRowCount = function ($scope) {
        var $root = $scope || $('#shipInfoDetailDataDiv');
        var count = $root.find('#shipInfoDetailTable tbody tr[data-detail-id]').length;
        app.state.detailRowCount = count;
        return count;
    };

    app.updateHeaderActionState = function () {
        var state = app.state;
        var messages = app.messages;
        var hasSelection = !!state.selectedHeaderRowKey;
        var busy = state.headerLoading || state.detailLoading || state.actionBusy || state.caseSubmitting;
        var row = state.selectedHeaderRow;
        var statusPermission = app.getStatusPermission(app.getHeaderStatus(row));
        var depositCaseStatus = hasSelection ? app.getDepositCaseStatus(row) : 'NotInitiated';
        var arurCaseStatus = hasSelection ? app.getArurCaseStatus(row) : 'NotInitiated';
        var depositDisabled = !hasSelection
            || busy
            || !app.canEnableCaseAction(depositCaseStatus, hasSelection ? app.getDepositOutboxFailed(row) : false)
            || !statusPermission.deposit
            || !app.hasPermission('Views.Function.ShipInfo.Deposit');
        var arurDisabled = !hasSelection
            || busy
            || !app.canEnableCaseAction(arurCaseStatus, hasSelection ? app.getArurOutboxFailed(row) : false)
            || !statusPermission.arur
            || !app.hasPermission('Views.Function.ShipInfo.ARUR');
        $('#btnShipInfoDeposit').prop('disabled', depositDisabled);
        $('#btnShipInfoArur').prop('disabled', arurDisabled);
    };

    app.getHeaderFields = function () {
        var state = app.state;
        return state.pageConfig ? (state.pageConfig.headerFields || state.pageConfig.HeaderFields || []) : [];
    };

    app.getAllHeaderFields = function () {
        return app.renderApi.getAllFields(app.getHeaderFields());
    };

    app.getHeaderEditFormFields = function () {
        if (app.state.headerFormEffectiveFields && app.state.headerFormEffectiveFields.length) {
            return app.state.headerFormEffectiveFields;
        }
        var state = app.state;
        var fields = state.pageConfig
            ? (state.pageConfig.headerEditFields || state.pageConfig.HeaderEditFields || [])
            : [];
        return app.renderApi.getAllFields(fields);
    };

    app.getHeaderFormMetadata = function () {
        var state = app.state;
        return state.pageConfig
            ? (state.pageConfig.headerFormMetadata || state.pageConfig.HeaderFormMetadata || null)
            : null;
    };

    app.getHeaderEditFields = function () {
        return app.getHeaderEditFormFields().filter(function (field) {
            return field.editable !== false && field.Editable !== false;
        });
    };

    app.getDetailEditFormFields = function () {
        if (app.state.detailFormEffectiveFields && app.state.detailFormEffectiveFields.length) {
            return app.state.detailFormEffectiveFields;
        }
        var state = app.state;
        var fields = state.pageConfig
            ? (state.pageConfig.detailEditFields || state.pageConfig.DetailEditFields || [])
            : [];
        return app.renderApi.getAllFields(fields);
    };

    app.getDetailFormMetadata = function () {
        var state = app.state;
        return state.pageConfig
            ? (state.pageConfig.detailFormMetadata || state.pageConfig.DetailFormMetadata || null)
            : null;
    };

    app.getDetailEditFields = function () {
        return app.getDetailEditFormFields().filter(function (field) {
            return field.editable !== false && field.Editable !== false;
        });
    };

    app.getDetailFields = function () {
        var state = app.state;
        return state.pageConfig ? (state.pageConfig.detailFields || state.pageConfig.DetailFields || []) : [];
    };

    app.getSearchFields = function () {
        return [];
    };

    app.formatStatusLabel = function (value) {
        var messages = app.messages;
        var normalized = String(value || '').trim();
        if (!normalized) {
            return '';
        }

        var map = {
            Processing: messages.statusProcessing,
            Cancelled: messages.statusCancelled
        };

        return map[normalized] || value;
    };
})(window, window.jQuery);
