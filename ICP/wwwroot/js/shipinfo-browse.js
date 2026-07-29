(function (global, $) {
    'use strict';

    var app = global.ShipInfoApp;
    if (!app) {
        return;
    }

    var state = app.state;
    var urls = app.urls;
    var messages = app.messages;

    app.loadPageConfig = function () {
        return $.getJSON(urls.pageConfig).then(function (response) {
            if (!response || !response.success) {
                throw new Error((response && response.message) || messages.saveFailed);
            }

            state.pageConfig = response.data || {};
            app.renderApi.clearLookupCache();
            app.initProTables();
        });
    };

    function resolveInvoiceKey(rowData) {
        if (!rowData) {
            return null;
        }

        return rowData.HeaderKey || rowData.headerKey || null;
    }

    app.selectHeader = function (headerRowKey, rowData) {
        state.selectedHeaderRowKey = headerRowKey;
        state.selectedHeaderKey = resolveInvoiceKey(rowData) || headerRowKey;
        state.selectedHeaderRow = rowData || null;
        app.restoreHeaderSelection();
        app.reloadDetailTable();
        app.updateHeaderActionState();
    };

    app.refreshHeaderKeepingSelection = function () {
        var keepRowKey = state.selectedHeaderRowKey;
        app.reloadHeaderTable();
        return $.Deferred().resolve().promise();
    };

    app.loadDetails = function (headerId) {
        if (!headerId) {
            state.detailItems = [];
            state.detailRowCount = 0;
            $('#shipInfoDetailDataDiv').empty();
            return $.Deferred().resolve().promise();
        }

        app.reloadDetailTable();
        return $.Deferred().resolve().promise();
    };
})(window, window.jQuery);
