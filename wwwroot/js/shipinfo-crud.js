(function (global, $) {
    'use strict';

    var app = global.ShipInfoApp;
    if (!app || !app.renderApi) {
        return;
    }

    var renderApi = app.renderApi;
    var state = app.state;
    var urls = app.urls;
    var messages = app.messages;

    function getViewModal() {
        var element = document.getElementById('shipInfoViewModal');
        if (!element || !global.bootstrap || !global.bootstrap.Modal) {
            return null;
        }

        return global.bootstrap.Modal.getOrCreateInstance(element);
    }

    function setViewModalLoading(isLoading) {
        $('#shipInfoViewModalMask').toggleClass('d-none', !isLoading);
    }

    function getModalFields() {
        return state.viewModalKind === 'header'
            ? app.getHeaderEditFormFields()
            : app.getDetailEditFormFields();
    }

    function getEditableFieldNames() {
        var editFields = state.viewModalKind === 'header'
            ? app.getHeaderEditFields()
            : app.getDetailEditFields();
        return editFields.map(function (field) {
            return field.fieldName || field.FieldName;
        });
    }

    function getStatusSource() {
        return state.viewModalKind === 'header'
            ? state.viewModalData
            : app.state.selectedHeaderRow;
    }

    function updateViewModalButtons() {
        var permission = app.getStatusPermission(app.getHeaderStatus(getStatusSource()));
        var canEdit = app.hasPermission('Views.Function.ShipInfo.Edit') && permission.edit;
        var canDelete = app.hasPermission('Views.Function.ShipInfo.Delete') && permission.delete;
        if (state.viewModalKind === 'detail') {
            canDelete = canDelete && app.canDeleteDetail();
        }

        var editing = !!state.viewModalEditing;

        $('#btnShipInfoViewEdit').toggleClass('d-none', editing || !canEdit);
        $('#btnShipInfoViewDelete').toggleClass('d-none', editing || !canDelete);
        $('#btnShipInfoViewSave').toggleClass('d-none', !editing || !canEdit);
        $('#btnShipInfoViewCancelEdit').toggleClass('d-none', !editing || !canEdit);
        $('#shipInfoViewModalLabel').text(editing
            ? (messages.editMode || messages.edit || 'Edit')
            : (messages.view || 'View'));
    }

    function renderViewForm(values) {
        var fields = getModalFields();
        var renderValues = renderApi.enrichDateRangeValues(values);
        renderApi.renderFormFields($('#shipInfoViewForm'), fields, $.extend({}, app.getRenderOptions(renderValues), {
            mode: 'view',
            includeHidden: state.viewModalKind === 'header'
        }));
        app.initTooltips($('#shipInfoViewForm'));
    }

    app.openViewModal = function (kind, key) {
        if (!key) {
            return;
        }

        state.viewModalKind = kind;
        state.viewModalKey = key;
        state.viewModalEditing = false;
        state.viewModalData = null;
        $('#shipInfoViewForm').empty();
        updateViewModalButtons();

        var modal = getViewModal();
        if (modal) {
            modal.show();
            $('#shipInfoViewModal .modal-body').scrollTop(0);
        }

        setViewModalLoading(true);
        var requestUrl = kind === 'header' ? urls.getHeader : urls.getDetail;
        var requestData = kind === 'header' ? { headerKey: key } : { detailKey: key };

        $.getJSON(requestUrl, requestData).done(function (response) {
            if (!response || !response.success) {
                app.showToast((response && response.message) || messages.saveFailed, 'danger');
                return;
            }

            state.viewModalData = response.data || {};
            renderViewForm(state.viewModalData);
            if (state.viewModalKind === 'detail') {
                app.getDetailRowCount();
            }
            updateViewModalButtons();
        }).fail(function (xhr) {
            app.showToast((xhr.responseJSON && xhr.responseJSON.message) || messages.saveFailed, 'danger');
        }).always(function () {
            setViewModalLoading(false);
        });
    };

    app.enterEditMode = function () {
        if (!state.viewModalData || !state.viewModalKey) {
            return;
        }

        if (!app.hasPermission('Views.Function.ShipInfo.Edit')) {
            return;
        }

        var permission = app.getStatusPermission(app.getHeaderStatus(getStatusSource()));
        if (!permission.edit) {
            app.showToast(messages.statusNotAllowed, 'warning');
            return;
        }

        state.viewModalEditing = true;
        renderApi.setFieldsEditable($('#shipInfoViewForm'), getEditableFieldNames(), {
            fields: getModalFields()
        });
        updateViewModalButtons();
    };

    app.cancelViewEdit = function () {
        if (!state.viewModalData) {
            return;
        }

        state.viewModalEditing = false;
        renderViewForm(state.viewModalData);
        updateViewModalButtons();
    };

    app.saveViewModal = function () {
        if (!state.viewModalEditing || !state.viewModalKey) {
            return;
        }

        var isHeader = state.viewModalKind === 'header';
        var fields = isHeader ? app.getHeaderEditFields() : app.getDetailEditFields();
        var saveUrl = isHeader ? urls.saveHeader : urls.saveDetail;
        var refreshMode = isHeader ? 'header' : 'detail';

        app.saveEntity(
            $('#shipInfoViewForm'),
            fields,
            saveUrl,
            state.viewModalKey,
            messages.saveSuccess,
            $('#btnShipInfoViewSave'),
            null,
            refreshMode,
            function (savedData) {
                state.viewModalEditing = false;
                state.viewModalData = savedData || state.viewModalData;
                renderViewForm(state.viewModalData);
                updateViewModalButtons();
            }
        );
    };

    app.deleteFromModal = function () {
        if (!state.viewModalKey) {
            return;
        }

        if (state.viewModalKind === 'header') {
            state.pendingDeleteHeaderKey = state.viewModalKey;
            app.showDeleteConfirm();
            return;
        }

        app.showDetailDeleteConfirm(state.viewModalKey);
    };

    app.showDeleteConfirm = function () {
        var headerKey = state.pendingDeleteHeaderKey || state.viewModalKey || state.selectedHeaderRowKey;
        if (!headerKey || !app.hasPermission('Views.Function.ShipInfo.Delete')) {
            app.showToast(messages.selectHeaderFirst, 'warning');
            return;
        }

        state.pendingDeleteHeaderKey = headerKey;
        if (global.bootstrap && global.bootstrap.Modal) {
            global.bootstrap.Modal.getOrCreateInstance(document.getElementById('shipInfoDeleteConfirmModal')).show();
        }
    };

    app.deleteSelectedHeader = function () {
        var headerKey = state.pendingDeleteHeaderKey || state.viewModalKey || state.selectedHeaderRowKey;
        if (!headerKey || !app.hasPermission('Views.Function.ShipInfo.Delete')) {
            app.showToast(messages.selectHeaderFirst, 'warning');
            return;
        }

        app.setActionBusy(true);
        $.ajax({
            url: urls.deleteHeader,
            type: 'POST',
            data: { headerKey: headerKey },
            dataType: 'json'
        }).done(function (response) {
            if (response && response.success) {
                app.showToast(messages.deleteSuccess, 'success');
                state.selectedHeaderKey = null;
                state.selectedHeaderRowKey = null;
                state.selectedHeaderRow = null;
                state.viewModalKey = null;
                state.viewModalData = null;
                state.pendingDeleteHeaderKey = null;
                state.detailItems = [];

                if (global.bootstrap && global.bootstrap.Modal) {
                    var deleteModal = global.bootstrap.Modal.getInstance(document.getElementById('shipInfoDeleteConfirmModal'));
                    if (deleteModal) {
                        deleteModal.hide();
                    }

                    var viewModal = global.bootstrap.Modal.getInstance(document.getElementById('shipInfoViewModal'));
                    if (viewModal) {
                        viewModal.hide();
                    }
                }

                app.reloadHeaderTable();
                app.reloadDetailTable();
                return;
            }

            app.showToast((response && response.message) || messages.deleteFailed, 'danger');
        }).fail(function (xhr) {
            app.showToast((xhr.responseJSON && xhr.responseJSON.message) || messages.deleteFailed, 'danger');
        }).always(function () {
            app.setActionBusy(false);
        });
    };

    app.showDetailDeleteConfirm = function (detailId) {
        if (!detailId || !app.hasPermission('Views.Function.ShipInfo.Delete')) {
            return;
        }

        if (!app.canDeleteDetail()) {
            app.showToast(messages.deleteLastDetailNotAllowed || messages.deleteFailed, 'warning');
            return;
        }

        state.pendingDeleteDetailId = detailId;
        if (global.bootstrap && global.bootstrap.Modal) {
            global.bootstrap.Modal.getOrCreateInstance(document.getElementById('shipInfoDeleteDetailConfirmModal')).show();
        }
    };

    app.deleteSelectedDetail = function () {
        if (!state.pendingDeleteDetailId || !app.hasPermission('Views.Function.ShipInfo.Delete')) {
            return;
        }

        app.setActionBusy(true);
        $.ajax({
            url: urls.deleteDetail,
            type: 'POST',
            data: { detailKey: state.pendingDeleteDetailId },
            dataType: 'json'
        }).done(function (response) {
            if (response && response.success) {
                app.showToast(messages.deleteSuccess, 'success');
                state.pendingDeleteDetailId = null;

                if (global.bootstrap && global.bootstrap.Modal) {
                    var deleteModal = global.bootstrap.Modal.getInstance(document.getElementById('shipInfoDeleteDetailConfirmModal'));
                    if (deleteModal) {
                        deleteModal.hide();
                    }

                    var viewModal = global.bootstrap.Modal.getInstance(document.getElementById('shipInfoViewModal'));
                    if (viewModal) {
                        viewModal.hide();
                    }
                }

                if (state.selectedHeaderKey) {
                    app.reloadDetailTable();
                }

                return;
            }

            app.showToast((response && response.message) || messages.deleteFailed, 'danger');
        }).fail(function (xhr) {
            app.showToast((xhr.responseJSON && xhr.responseJSON.message) || messages.deleteFailed, 'danger');
        }).always(function () {
            app.setActionBusy(false);
        });
    };

    app.saveEntity = function ($form, fields, saveUrl, entityId, successMessage, $saveButton, closeModalId, refreshMode, onSuccess) {
        var clientErrors = renderApi.validateClientFields($form, fields, app.getCulture(), messages.requiredMark);
        if (clientErrors.length > 0) {
            app.showToast(clientErrors[0].message, 'warning');
            return;
        }

        var values = renderApi.collectControlValues($form);
        var meta = renderApi.collectSaveMeta($form);
        app.setButtonLoading($saveButton, true);
        app.setActionBusy(true);
        $.ajax({
            url: saveUrl,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                id: entityId || meta.id,
                values: values,
                rowVersion: meta.rowVersion,
                updateTime: meta.updateTime
            }),
            dataType: 'json'
        }).done(function (response) {
            if (response && response.success) {
                app.showToast(successMessage, 'success');
                if (closeModalId && global.bootstrap && global.bootstrap.Modal) {
                    var modal = global.bootstrap.Modal.getInstance(document.getElementById(closeModalId));
                    if (modal) {
                        modal.hide();
                    }
                }

                if (typeof onSuccess === 'function') {
                    onSuccess(response.data || null);
                }

                if (refreshMode === 'header') {
                    app.refreshHeaderKeepingSelection();
                } else if (state.selectedHeaderKey) {
                    app.reloadDetailTable();
                }

                return;
            }

            app.showToast((response && response.message) || messages.validationFailed, 'danger');
            renderApi.validateClientFields($form, fields, app.getCulture(), messages.requiredMark);
        }).fail(function (xhr) {
            var message = (xhr.responseJSON && xhr.responseJSON.message) || messages.saveFailed;
            app.showToast(message, 'danger');
        }).always(function () {
            app.setButtonLoading($saveButton, false);
            app.setActionBusy(false);
        });
    };
})(window, window.jQuery);
