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

    function validateDetailNumericRules(values) {
        var decimalFields = [
            { name: 'Qty', places: 3, required: true },
            { name: 'Price', places: 2, required: true },
            { name: 'Amount', places: 2, required: true },
            { name: 'Rate', places: 4, required: false }
        ];
        var integerFields = ['CartonNo', 'Length', 'Width', 'Hight', 'GrossWeight'];

        if (Object.prototype.hasOwnProperty.call(values, 'InvoiceSeq')
            && String(values.InvoiceSeq || '').trim() !== ''
            && !/^[1-9]\d*$/.test(String(values.InvoiceSeq).trim())) {
            return 'Invoice Seq must be an integer greater than 0.';
        }

        for (var i = 0; i < decimalFields.length; i++) {
            var rule = decimalFields[i];
            if (!Object.prototype.hasOwnProperty.call(values, rule.name)) {
                continue;
            }
            var decimalValue = String(values[rule.name] || '').trim();
            if (!decimalValue && !rule.required) {
                continue;
            }
            var pattern = new RegExp('^\\d+(?:\\.\\d{1,' + rule.places + '})?$');
            if (!pattern.test(decimalValue)) {
                return rule.name + ' must be a non-negative number with up to ' + rule.places + ' decimal places.';
            }
        }

        for (var j = 0; j < integerFields.length; j++) {
            var integerField = integerFields[j];
            if (!Object.prototype.hasOwnProperty.call(values, integerField)) {
                continue;
            }

            if (!/^\d+$/.test(String(values[integerField] || '').trim())) {
                return integerField + ' must be a non-negative integer.';
            }
        }

        return null;
    }

    function initializeDetailNumericControls($form) {
        function getControl(name) {
            return $form.find('.shipinfo-control[data-field="' + name + '"]');
        }

        function formatControl(name, places) {
            var $control = getControl(name);
            var value = String($control.val() || '').trim();
            if (value !== '' && Number.isFinite(Number(value))) {
                $control.val(Number(value).toFixed(places));
            }
        }

        function calculateAmount() {
            var qty = Number(getControl('Qty').val());
            var price = Number(getControl('Price').val());
            if (Number.isFinite(qty) && Number.isFinite(price)) {
                getControl('Amount').val((qty * price).toFixed(2));
            }
        }

        getControl('InvoiceSeq').attr({ step: '1', min: '1' });
        getControl('Qty').attr({ step: '0.001', min: '0' });
        getControl('Price').attr({ step: '0.01', min: '0' });
        getControl('Amount').attr({ step: '0.01', min: '0' });
        getControl('Rate').attr({ step: '0.0001', min: '0' });

        getControl('Qty').add(getControl('Price')).add(getControl('Amount'))
            .off('.shipinfoDetailCalculation')
            .on('input.shipinfoDetailCalculation change.shipinfoDetailCalculation', calculateAmount);

        [['Qty', 3], ['Price', 2], ['Amount', 2], ['Rate', 4]].forEach(function (setting) {
            getControl(setting[0]).off('blur.shipinfoDetailFormat').on('blur.shipinfoDetailFormat', function () {
                formatControl(setting[0], setting[1]);
            });
        });
    }

    function validateDelayPair($form, values) {
        var $reason = $form.find('.shipinfo-control[data-field="ReasonForDeliveryDelay"]');
        var $date = $form.find('.shipinfo-control[data-field="DelayNotificationDate"]');
        var $group = $reason.closest('.shipinfo-form-group');
        $group.find('.shipinfo-delay-pair-error').remove();
        $reason.add($date).removeClass('is-invalid').removeAttr('aria-invalid');
        var hasReason = String(values.ReasonForDeliveryDelay || '').trim() !== '';
        var hasDate = String(values.DelayNotificationDate || '').trim() !== '';
        if (hasReason === hasDate) return true;

        var message = messages.delayPairRequired || 'Reason for Delivery Delay and Delay Notification Date must both be filled in, or both be left blank.';
        $reason.add($date).addClass('is-invalid').attr('aria-invalid', 'true');
        $group.find('.row').first().before($('<div class="alert alert-danger shipinfo-delay-pair-error" role="alert"></div>').text(message));
        ($reason.length ? $reason : $date).trigger('focus');
        return false;
    }

    function initializeForkliftMovingLabor($form) {
        var $forklift = $form.find('.shipinfo-control[data-field="Forklift"]');
        var $movingLabor = $form.find('.shipinfo-control[data-field="MovingLabor"]');
        var previousValue = String($forklift.val() || '').toUpperCase();
        $forklift.off('change.shipinfoMovingLabor').on('change.shipinfoMovingLabor', function () {
            var currentValue = String($forklift.val() || '').toUpperCase();
            if (previousValue === 'N' && (currentValue === '' || currentValue === 'Y')) {
                $movingLabor.val('').trigger('input');
            }
            previousValue = currentValue;
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
            && app.hasPermission('Views.Function.ShipInfo.Discard')
            && permission.delete;
        var isLastDetail = state.viewModalKind === 'detail' && app.getDetailRowCount() === 1;
        var editing = !!state.viewModalEditing;
        var notificationPanelOpen = !$('#shipInfoArrivalSchedulePanel').hasClass('d-none')
            || !$('#shipInfoDeliveryDelayPanel').hasClass('d-none')
            || !$('#shipInfoControlledGoodsPanel').hasClass('d-none');

        $('#btnShipInfoViewEdit').toggleClass('d-none', editing || !canEdit);
        $('#btnShipInfoViewSave').toggleClass('d-none', !editing || !canEdit)
            .prop('disabled', !!state.actionBusy || notificationPanelOpen);
        $('#shipInfoArrivalScheduleToolbar').toggleClass('d-none', !editing || !canEdit || state.viewModalKind !== 'header');
        $('#btnShipInfoViewCancelEdit').toggleClass('d-none', !editing || !canEdit)
            .prop('disabled', !!state.actionBusy || notificationPanelOpen);
        $('#btnShipInfoViewDiscard').toggleClass('d-none', editing || !canDiscard)
            .prop('disabled', !!state.actionBusy);
        $('#btnShipInfoViewDelete').toggleClass('d-none', editing || !app.hasPermission('Views.Function.ShipInfo.Delete') || !permission.delete || isLastDetail)
            .prop('disabled', !!state.actionBusy);
        $('#shipInfoViewModalLabel').text(editing
            ? (messages.editMode || messages.edit || 'Edit')
            : (messages.view || 'View'));
    }

    function renderViewForm(values) {
        if (state.viewModalKind === 'header') {
            try {
                var rendered = renderApi.renderMetadataForm($('#shipInfoViewForm'), app.getHeaderFormMetadata(), $.extend({}, app.getRenderOptions(values), {
                    mode: state.viewModalEditing ? 'edit' : 'view', autoFocus: false
                }));
                state.headerFormEffectiveFields = rendered.fields;
                if (state.viewModalEditing) {
                    initializeForkliftMovingLabor($('#shipInfoViewForm'));
                }
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
                mode: state.viewModalEditing ? 'edit' : 'view', autoFocus: false
            }));
            state.detailFormEffectiveFields = rendered.fields;
            if (state.viewModalEditing) {
                initializeDetailNumericControls($('#shipInfoViewForm'));
            }
        } catch (error) {
            state.detailFormEffectiveFields = [];
            $('#shipInfoViewForm').empty().append('<div class="alert alert-danger mb-0">明細表單設定載入失敗，請聯絡系統管理員。</div>');
            app.showToast((error && error.message) || '明細表單設定載入失敗', 'danger');
        }
        app.initTooltips($('#shipInfoViewForm'));
    }

    function restoreViewScroll(scrollTop) {
        var $body = $('#shipInfoViewModal .modal-body');
        $body.scrollTop(scrollTop);
        global.requestAnimationFrame(function () { $body.scrollTop(scrollTop); });
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

        var scrollTop = $('#shipInfoViewModal .modal-body').scrollTop();
        state.viewModalEditing = true;
        renderViewForm(state.viewModalData);
        updateViewModalButtons();
        restoreViewScroll(scrollTop);
    };

    app.cancelViewEdit = function () {
        if (!state.viewModalData) {
            return;
        }

        var scrollTop = $('#shipInfoViewModal .modal-body').scrollTop();
        state.viewModalEditing = false;
        renderViewForm(state.viewModalData);
        updateViewModalButtons();
        restoreViewScroll(scrollTop);
    };

    app.saveViewModal = function () {
        if (!state.viewModalEditing || !state.viewModalKey) {
            return;
        }

        var isHeader = state.viewModalKind === 'header';
        var modalScrollTop = $('#shipInfoViewModal .modal-body').scrollTop();
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
                restoreViewScroll(modalScrollTop);
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
        if (state.viewModalKind === 'header' && !validateDelayPair($form, values)) {
            return;
        }
        if (Object.prototype.hasOwnProperty.call(values, 'TotalCartons')
            && !/^\d+$/.test(String(values.TotalCartons || '').trim())) {
            app.showToast(messages.totalCartonsInteger || 'Total Cartons must be a non-negative integer.', 'warning');
            return;
        }
        var detailNumericError = validateDetailNumericRules(values);
        if (detailNumericError) {
            app.showToast(detailNumericError, 'warning');
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
