(function (global, $) {
    'use strict';

    var app = global.ShipInfoApp;
    if (!app) {
        return;
    }

    function bindEvents() {
        $('#btnShipInfoViewEdit').on('click', app.enterEditMode);
        $('#btnShipInfoViewSave').on('click', app.saveViewModal);
        $('#btnShipInfoViewCancelEdit').on('click', app.cancelViewEdit);
        $('#btnShipInfoDeposit').on('click', function () {
            app.openCaseDrawer('Deposit');
        });
        $('#btnShipInfoArur').on('click', function () {
            app.openCaseDrawer('ARUR');
        });
        $('#btnShipInfoViewDiscard').on('click', app.openDiscardModal);
        $('#btnShipInfoConfirmDiscard').on('click', app.submitDiscard);
        $('#btnShipInfoCaseSubmit').on('click', app.showCaseSubmitConfirm);
        $('#btnShipInfoConfirmCaseSubmit').on('click', app.submitCase);

        $('#shipInfoViewModal').on('hidden.bs.modal', function () {
            app.state.viewModalEditing = false;
            app.state.viewModalKey = null;
            app.state.viewModalData = null;
            app.state.headerFormEffectiveFields = [];
            app.state.detailFormEffectiveFields = [];
            app.renderApi.destroyForm($('#shipInfoViewForm'));
            if (typeof app.disposeHeaderAttachments === 'function') app.disposeHeaderAttachments();
            $('#shipInfoHeaderAttachments').addClass('d-none');
            $('#shipInfoViewForm').empty();
        });

        $('#shipInfoCaseDrawer').on('hidden.bs.offcanvas', function () {
            app.state.caseType = null;
            app.state.caseDrawerData = null;
            app.state.caseHeaderRowKey = null;
        });
    }

    function init() {
        if (!app.urls.pageConfig) {
            return;
        }

        bindEvents();
        app.disposeTooltips();
        app.initTooltips($('.shipinfo-page'));
        app.updateHeaderActionState();
        app.setHeaderLoading(true);
        app.loadPageConfig()
            .fail(function (error) {
                app.showToast(error.message || app.messages.saveFailed, 'danger');
            })
            .always(function () {
                app.setHeaderLoading(false);
            });
    }

    $(init);
})(window, window.jQuery);
