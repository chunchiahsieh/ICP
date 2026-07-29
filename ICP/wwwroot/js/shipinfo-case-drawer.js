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

    function formatSummaryValue(value) {
        if (value === undefined || value === null || value === '') {
            return '-';
        }

        return value;
    }

    function resolveCaseHeaderRowKey() {
        return state.selectedHeaderRowKey;
    }

    function getPairsPerRow() {
        return window.matchMedia('(max-width: 767.98px)').matches ? 1 : 3;
    }

    function getFieldEntries(fields, item) {
        var entries = [];

        renderApi.getAllFields(fields).forEach(function (field) {
            if (!renderApi.canUseField(field, app.hasPermission)) {
                return;
            }

            var fieldName = field.FieldName || field.fieldName;
            var rawValue = app.getRowValue(item, [
                fieldName,
                fieldName.charAt(0).toLowerCase() + fieldName.slice(1)
            ]);
            var displayValue = fieldName === 'Status' || fieldName === 'status'
                ? app.formatStatusLabel(rawValue)
                : rawValue;
            entries.push({
                label: renderApi.resolveLabel(field, app.getCulture()),
                value: formatSummaryValue(displayValue)
            });
        });

        return entries;
    }

    function renderHeaderReportTable(fields, item) {
        var entries = getFieldEntries(fields, item);
        var pairsPerRow = getPairsPerRow();
        var $table = $('<table class="table table-sm table-bordered mb-0 shipinfo-drawer-report-table shipinfo-drawer-header-report"></table>');
        var $tbody = $('<tbody></tbody>');

        for (var i = 0; i < entries.length; i += pairsPerRow) {
            var $tr = $('<tr></tr>');

            for (var j = 0; j < pairsPerRow; j++) {
                var entry = entries[i + j];
                if (entry) {
                    $tr.append(
                        '<th scope="row" class="shipinfo-drawer-report-label">'
                        + $('<span>').text(entry.label).html()
                        + '</th>'
                    );
                    $tr.append(
                        '<td class="shipinfo-drawer-report-value">'
                        + $('<span>').text(entry.value).html()
                        + '</td>'
                    );
                } else {
                    $tr.append('<th class="shipinfo-drawer-report-label shipinfo-drawer-report-empty"></th>');
                    $tr.append('<td class="shipinfo-drawer-report-value shipinfo-drawer-report-empty"></td>');
                }
            }

            $tbody.append($tr);
        }

        $table.append($tbody);
        return $table;
    }

    function renderDetailReportTable(fields, details) {
        var visibleFields = renderApi.getAllFields(fields).filter(function (field) {
            return renderApi.canUseField(field, app.hasPermission);
        });
        var $wrap = $('<div class="table-responsive shipinfo-drawer-detail-table-wrap"></div>');
        var $table = $('<table class="table table-sm table-bordered table-striped mb-0 shipinfo-drawer-report-table shipinfo-drawer-detail-report"></table>');
        var $thead = $('<thead class="table-light"></thead>');
        var $headerRow = $('<tr></tr>');

        visibleFields.forEach(function (field) {
            var label = renderApi.resolveLabel(field, app.getCulture());
            $headerRow.append('<th scope="col">' + $('<span>').text(label).html() + '</th>');
        });

        $thead.append($headerRow);

        var $tbody = $('<tbody></tbody>');
        details.forEach(function (item) {
            var $tr = $('<tr></tr>');

            visibleFields.forEach(function (field) {
                var fieldName = field.FieldName || field.fieldName;
                var value = app.getRowValue(item, [
                    fieldName,
                    fieldName.charAt(0).toLowerCase() + fieldName.slice(1)
                ]);
                $tr.append('<td>' + $('<span>').text(formatSummaryValue(value)).html() + '</td>');
            });

            $tbody.append($tr);
        });

        $table.append($thead).append($tbody);
        $wrap.append($table);
        return $wrap;
    }

    function renderSectionAccordion($container, title, $content, accordionId, defaultExpanded) {
        $container.empty();
        var collapseId = accordionId + 'Collapse';
        var headingId = accordionId + 'Heading';
        var expanded = defaultExpanded ? 'true' : 'false';
        var showClass = defaultExpanded ? ' show' : '';
        var collapsedClass = defaultExpanded ? '' : ' collapsed';
        var $accordion = $('<div class="accordion" id="' + accordionId + '"></div>');
        var $accordionItem = $('<div class="accordion-item"></div>');
        var $header = $('<h2 class="accordion-header" id="' + headingId + '"></h2>');

        $header.append(
            $('<button class="accordion-button' + collapsedClass + '" type="button" data-bs-toggle="collapse" data-bs-target="#' + collapseId + '" aria-expanded="' + expanded + '" aria-controls="' + collapseId + '"></button>')
                .text(title)
        );

        var $collapse = $('<div id="' + collapseId + '" class="accordion-collapse collapse' + showClass + '" aria-labelledby="' + headingId + '"></div>');
        var $body = $('<div class="accordion-body p-0"></div>');

        $body.append($content);
        $collapse.append($body);
        $accordionItem.append($header).append($collapse);
        $accordion.append($accordionItem);
        $container.append($accordion);
    }

    app.renderCaseDrawer = function (data) {
        state.caseDrawerData = data || null;
        var header = (data && data.header) || {};
        var details = (data && data.details) || [];

        renderSectionAccordion(
            $('#shipInfoCaseHeaderAccordionWrap'),
            messages.headerInformation || messages.drawerHeader || 'Header Information',
            $('<div class="p-3"></div>').append(renderHeaderReportTable(app.getAllHeaderFields(), header)),
            'shipInfoCaseHeaderAccordion',
            true
        );

        var $detailContent;
        if (!details.length) {
            $detailContent = $('<p class="text-muted mb-0 p-3"></p>').text(messages.noDetailData || '');
        } else {
            $detailContent = renderDetailReportTable(app.getDetailFields(), details);
        }

        renderSectionAccordion(
            $('#shipInfoCaseDetailAccordionWrap'),
            messages.detailInformation || messages.drawerDetail || 'Detail Information',
            $detailContent,
            'shipInfoCaseDetailAccordion',
            true
        );

        var canSubmit = !!(data && (data.canSubmit || data.CanSubmit));
        var validationMessages = (data && (data.validationMessages || data.ValidationMessages)) || [];
        $('#btnShipInfoCaseSubmit').prop('disabled', !canSubmit || state.caseSubmitting);
        if (!canSubmit && validationMessages.length) {
            app.showToast(validationMessages[0], 'warning');
        }
    };

    app.setCaseDrawerLoading = function (isLoading) {
        $('#shipInfoCaseDrawerMask').toggleClass('d-none', !isLoading);
        $('#btnShipInfoCaseSubmit, #btnShipInfoCaseCancel, #btnShipInfoCaseDrawerClose').prop('disabled', isLoading || state.caseSubmitting);
    };

    app.openCaseDrawer = function (caseType) {
        var headerRowKey = resolveCaseHeaderRowKey();
        if (!headerRowKey) {
            app.showToast(messages.selectHeaderFirst, 'warning');
            return;
        }

        var normalizedType = (caseType || '').toUpperCase() === 'ARUR' ? 'ARUR' : 'Deposit';
        if (normalizedType === 'Deposit' && !app.hasPermission('Views.Function.ShipInfo.Deposit')) {
            return;
        }

        if (normalizedType === 'ARUR' && !app.hasPermission('Views.Function.ShipInfo.ARUR')) {
            return;
        }

        state.caseType = normalizedType;
        state.caseHeaderRowKey = headerRowKey;
        $('#shipInfoCaseDrawerLabel').text(
            normalizedType === 'Deposit'
                ? (messages.caseDrawerDepositTitle || messages.deposit || 'Deposit')
                : (messages.caseDrawerArurTitle || messages.arur || 'ARUR')
        );

        $('#shipInfoCaseHeaderAccordionWrap, #shipInfoCaseDetailAccordionWrap').empty();
        var drawerElement = document.getElementById('shipInfoCaseDrawer');
        if (global.bootstrap && global.bootstrap.Offcanvas) {
            global.bootstrap.Offcanvas.getOrCreateInstance(drawerElement).show();
        }

        app.setCaseDrawerLoading(true);
        $.getJSON(urls.getCaseDrawerData, {
            headerKey: headerRowKey,
            caseType: normalizedType
        }).done(function (response) {
            if (!response || !response.success) {
                app.showToast((response && response.message) || messages.caseCreateFailed, 'danger');
                return;
            }

            app.renderCaseDrawer(response.data || {});
        }).fail(function (xhr) {
            app.showToast((xhr.responseJSON && xhr.responseJSON.message) || messages.caseCreateFailed, 'danger');
        }).always(function () {
            app.setCaseDrawerLoading(false);
        });
    };

    app.showCaseSubmitConfirm = function () {
        if (!state.caseDrawerData || !state.caseHeaderRowKey) {
            return;
        }

        var isDeposit = state.caseType === 'Deposit';
        $('#shipInfoCaseSubmitConfirmModalLabel').text(isDeposit ? messages.depositConfirmTitle : messages.arurConfirmTitle);
        $('#shipInfoCaseSubmitConfirmMessage').text(isDeposit ? messages.depositConfirmMessage : messages.arurConfirmMessage);

        if (global.bootstrap && global.bootstrap.Modal) {
            global.bootstrap.Modal.getOrCreateInstance(document.getElementById('shipInfoCaseSubmitConfirmModal')).show();
        }
    };

    app.submitCase = function () {
        if (!state.caseHeaderRowKey || !state.caseType || state.caseSubmitting) {
            return;
        }

        var submitUrl = state.caseType === 'Deposit' ? urls.createDeposit : urls.createArur;
        state.caseSubmitting = true;
        app.updateHeaderActionState();
        app.setButtonLoading($('#btnShipInfoCaseSubmit'), true);
        app.setButtonLoading($('#btnShipInfoConfirmCaseSubmit'), true);
        $('#btnShipInfoCaseCancel, #btnShipInfoCaseDrawerClose').prop('disabled', true);

        $.ajax({
            url: submitUrl,
            type: 'POST',
            data: { headerKey: state.caseHeaderRowKey },
            dataType: 'json'
        }).done(function (response) {
            if (response && response.success) {
                app.showToast(messages.caseCreateSuccess, 'success');
                if (global.bootstrap && global.bootstrap.Modal) {
                    var confirmModal = global.bootstrap.Modal.getInstance(document.getElementById('shipInfoCaseSubmitConfirmModal'));
                    if (confirmModal) {
                        confirmModal.hide();
                    }
                }

                if (global.bootstrap && global.bootstrap.Offcanvas) {
                    var drawer = global.bootstrap.Offcanvas.getInstance(document.getElementById('shipInfoCaseDrawer'));
                    if (drawer) {
                        drawer.hide();
                    }
                }

                app.refreshHeaderKeepingSelection();
                return;
            }

            app.showToast((response && response.message) || messages.caseCreateFailed, 'danger');
        }).fail(function (xhr) {
            app.showToast((xhr.responseJSON && xhr.responseJSON.message) || messages.caseCreateFailed, 'danger');
        }).always(function () {
            state.caseSubmitting = false;
            app.updateHeaderActionState();
            app.setButtonLoading($('#btnShipInfoCaseSubmit'), false);
            app.setButtonLoading($('#btnShipInfoConfirmCaseSubmit'), false);
            $('#btnShipInfoCaseCancel, #btnShipInfoCaseDrawerClose').prop('disabled', false);
            if (state.caseDrawerData) {
                $('#btnShipInfoCaseSubmit').prop('disabled', !(state.caseDrawerData.canSubmit || state.caseDrawerData.CanSubmit));
            }
        });
    };
})(window, window.jQuery);
