(function (global, $) {
    'use strict';

    var app = global.ShipInfoApp;
    if (!app) return;

    function getModal() {
        var element = document.getElementById('shipInfoDeleteConfirmModal');
        return element && global.bootstrap && global.bootstrap.Modal
            ? global.bootstrap.Modal.getOrCreateInstance(element)
            : null;
    }

    app.openDeleteConfirmModal = function () {
        var state = app.state;
        if (!state.viewModalKey) return;
        var isHeader = state.viewModalKind === 'header';
        if (!isHeader && app.getDetailRowCount() === 1) {
            app.showToast(app.messages.deleteLastDetailNotAllowed || 'At least one detail row must remain and cannot be deleted.', 'warning');
            return;
        }
        state.deleteTarget = { kind: isHeader ? 'header' : 'detail', key: state.viewModalKey };
        $('#shipInfoDeleteConfirmModalLabel').text(isHeader ? app.messages.deleteConfirmTitle : app.messages.deleteDetailConfirmTitle);
        $('#shipInfoDeleteConfirmMessage').text(isHeader ? app.messages.deleteConfirmMessage : app.messages.deleteDetailConfirmMessage);
        var modal = getModal();
        if (modal) modal.show();
    };

    app.submitDelete = function () {
        var target = app.state.deleteTarget;
        if (!target) return;
        var isHeader = target.kind === 'header';
        var $button = $('#btnShipInfoConfirmDelete');
        app.setButtonLoading($button, true);
        app.setActionBusy(true);
        $.post(isHeader ? app.urls.deleteHeader : app.urls.deleteDetail,
            isHeader ? { headerKey: target.key } : { detailKey: target.key })
            .done(function (response) {
                if (!response || !response.success) {
                    app.showToast((response && response.message) || app.messages.deleteFailed, 'danger');
                    return;
                }
                var confirmModal = getModal();
                if (confirmModal) confirmModal.hide();
                var viewModal = global.bootstrap && global.bootstrap.Modal
                    ? global.bootstrap.Modal.getInstance(document.getElementById('shipInfoViewModal')) : null;
                if (viewModal) viewModal.hide();
                app.showToast(app.messages.deleteSuccess, 'success');
                if (isHeader) {
                    app.state.selectedHeaderKey = null;
                    app.state.selectedHeaderRowKey = null;
                    app.state.selectedHeaderRow = null;
                    $('#shipInfoDetailDataDiv').empty();
                    app.reloadHeaderTable();
                } else {
                    app.reloadDetailTable();
                }
            }).fail(function (xhr) {
                app.showToast((xhr.responseJSON && xhr.responseJSON.message) || app.messages.deleteFailed, 'danger');
            }).always(function () {
                app.setButtonLoading($button, false);
                app.setActionBusy(false);
            });
    };
})(window, window.jQuery);
