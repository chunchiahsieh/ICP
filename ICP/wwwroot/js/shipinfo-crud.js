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
        if (state.viewModalKind === 'header') {
            return (state.headerFormEffectiveFields || []).filter(function (field) {
                return field.editable !== false && field.Editable !== false;
            }).map(function (field) { return field.fieldName || field.FieldName; });
        }
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
        var canDiscard = state.viewModalKind === 'header'
            && app.hasPermission('Views.Function.ShipInfo.Delete')
            && permission.delete;
        var editing = !!state.viewModalEditing;

        $('#btnShipInfoViewEdit').toggleClass('d-none', editing || !canEdit);
        $('#btnShipInfoViewSave').toggleClass('d-none', !editing || !canEdit);
        $('#btnShipInfoViewCancelEdit').toggleClass('d-none', !editing || !canEdit);
        $('#btnShipInfoViewDiscard').toggleClass('d-none', editing || !canDiscard)
            .prop('disabled', !!state.actionBusy);
        $('#shipInfoViewModalLabel').text(editing
            ? (messages.editMode || messages.edit || 'Edit')
            : (messages.view || 'View'));
    }

    function renderViewForm(values) {
        if (state.viewModalKind === 'header') {
            try {
                var rendered = renderApi.renderMetadataForm($('#shipInfoViewForm'), app.getHeaderFormMetadata(), $.extend({}, app.getRenderOptions(values), {
                    mode: state.viewModalEditing ? 'edit' : 'view'
                }));
                state.headerFormEffectiveFields = rendered.fields;
                if (typeof app.renderHeaderAttachments === 'function') {
                    app.renderHeaderAttachments(
                        state.viewModalKey,
                        state.viewModalEditing,
                        $('#shipInfoViewForm').find('[data-form-adapter="shipInfoHeaderAttachments"]'));
                }
            } catch (error) {
                state.headerFormEffectiveFields = [];
                if (typeof app.disposeHeaderAttachments === 'function') app.disposeHeaderAttachments();
                $('#shipInfoViewForm').empty().append('<div class="alert alert-danger mb-0">表單設定載入失敗，請聯絡系統管理員。</div>');
                app.showToast((error && error.message) || '表單設定載入失敗', 'danger');
            }
            return;
        }
        try {
            var rendered = renderApi.renderMetadataForm($('#shipInfoViewForm'), app.getDetailFormMetadata(), $.extend({}, app.getRenderOptions(values), {
                mode: state.viewModalEditing ? 'edit' : 'view'
            }));
            state.detailFormEffectiveFields = rendered.fields;
        } catch (error) {
            state.detailFormEffectiveFields = [];
            $('#shipInfoViewForm').empty().append('<div class="alert alert-danger mb-0">明細表單設定載入失敗，請聯絡系統管理員。</div>');
            app.showToast((error && error.message) || '明細表單設定載入失敗', 'danger');
        }
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
        state.headerFormEffectiveFields = [];
        state.detailFormEffectiveFields = [];
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
        renderViewForm(state.viewModalData);
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
        var fields = isHeader ? getEditableFieldNames().map(function (name) {
            return (state.headerFormEffectiveFields || []).filter(function (field) {
                return String(field.fieldName || field.FieldName).toLowerCase() === String(name).toLowerCase();
            })[0];
        }).filter(Boolean) : app.getDetailEditFields();
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

    app.saveEntity = function ($form, fields, saveUrl, entityId, successMessage, $saveButton, closeModalId, refreshMode, onSuccess) {
        var clientErrors = renderApi.validateClientFields($form, fields, app.getCulture(), messages.requiredMark);
        if (clientErrors.length > 0) {
            app.showToast(clientErrors[0].message, 'warning');
            return;
        }

        var values = renderApi.collectControlValues($form);
        if (Object.prototype.hasOwnProperty.call(values, 'TotalCartons')
            && !/^\d+$/.test(String(values.TotalCartons || '').trim())) {
            app.showToast(messages.totalCartonsInteger || 'Total Cartons must be a non-negative integer.', 'warning');
            return;
        }
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
