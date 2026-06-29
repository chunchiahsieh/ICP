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
        pendingDeleteDetailId: null,
        pendingDeleteHeaderKey: null,
        viewModalKind: null,
        viewModalKey: null,
        viewModalData: null,
        viewModalEditing: false,
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

    app.isDepositCompleted = function (row) {
        return !!(app.getRowValue(row, ['Deposit', 'deposit'])
            || app.getRowValue(row, ['DepositNo', 'depositNo']));
    };

    app.isArurCompleted = function (row) {
        return !!(app.getRowValue(row, ['RtNo', 'rtNo'])
            || app.getRowValue(row, ['ArurNo', 'arurNo']));
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

    app.updateHeaderActionState = function () {
        var state = app.state;
        var hasSelection = !!state.selectedHeaderRowKey;
        var busy = state.headerLoading || state.detailLoading || state.actionBusy || state.caseSubmitting;
        var row = state.selectedHeaderRow;
        var statusPermission = app.getStatusPermission(app.getHeaderStatus(row));
        var depositDone = app.isDepositCompleted(row);
        var arurDone = app.isArurCompleted(row);
        var depositDisabled = !hasSelection
            || busy
            || depositDone
            || !statusPermission.deposit
            || !app.hasPermission('Views.Function.ShipInfo.Deposit');
        var arurDisabled = !hasSelection
            || busy
            || arurDone
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

    app.getHeaderEditFields = function () {
        return app.getAllHeaderFields().filter(function (field) {
            return field.editable !== false && field.Editable !== false;
        });
    };

    app.getDetailFields = function () {
        var state = app.state;
        return state.pageConfig ? (state.pageConfig.detailFields || state.pageConfig.DetailFields || []) : [];
    };

    app.getDetailEditFields = function () {
        return app.getDetailFields().filter(function (field) {
            return field.editable !== false && field.Editable !== false;
        });
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
            WarehouseReceived: messages.statusWarehouseReceived,
            Cancelled: messages.statusCancelled
        };

        return map[normalized] || value;
    };
})(window, window.jQuery);
