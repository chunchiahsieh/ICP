(function (global, $) {
    'use strict';

    var app = global.ShipInfoApp;
    if (!app) return;

    function getModal() {
        var element = document.getElementById('shipInfoDiscardModal');
        return element && global.bootstrap && global.bootstrap.Modal
            ? global.bootstrap.Modal.getOrCreateInstance(element)
            : null;
    }

    app.openDiscardModal = function () {
        var state = app.state;
        var headerKey = state.viewModalKind === 'header' ? state.viewModalKey : state.selectedHeaderRowKey;
        if (!headerKey) return;
        state.discardHeaderKey = headerKey;
        var $reason = $('#shipInfoDiscardReason');
        $reason.val('').removeClass('is-invalid');
        var modal = getModal();
        if (modal) modal.show();
    };

    app.submitDiscard = function () {
        var headerKey = app.state.discardHeaderKey;
        var $reason = $('#shipInfoDiscardReason');
        var reason = String($reason.val() || '').trim();
        if (!reason) {
            $reason.addClass('is-invalid').trigger('focus');
            return;
        }

        var $button = $('#btnShipInfoConfirmDiscard');
        app.setButtonLoading($button, true);
        app.setActionBusy(true);
        $.ajax({
            url: app.urls.discardHeader,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ headerKey: headerKey, reason: reason }),
            dataType: 'json'
        }).done(function (response) {
            if (!response || !response.success) {
                app.showToast((response && response.message) || app.messages.discardFailed, 'danger');
                return;
            }
            var modal = getModal();
            if (modal) modal.hide();
            var viewModalElement = document.getElementById('shipInfoViewModal');
            var viewModal = viewModalElement && global.bootstrap && global.bootstrap.Modal
                ? global.bootstrap.Modal.getInstance(viewModalElement)
                : null;
            if (viewModal) viewModal.hide();
            app.showToast(app.messages.discardSuccess, 'success');
            app.refreshHeaderKeepingSelection();
        }).fail(function (xhr) {
            app.showToast((xhr.responseJSON && xhr.responseJSON.message) || app.messages.discardFailed, 'danger');
        }).always(function () {
            app.setButtonLoading($button, false);
            app.setActionBusy(false);
        });
    };
})(window, window.jQuery);
