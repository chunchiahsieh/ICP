(function (global, $) {
    'use strict';
    var app = global.ShipInfoApp;
    if (!app || !$) return;
    var editScrollTop = 0;
    var loadGeneration = 0;

    function setError(message) {
        $('#shipInfoControlledGoodsError').text(message || '').toggleClass('d-none', !message);
    }

    function currentScheduleDate() {
        return new Intl.DateTimeFormat('sv-SE', {
            timeZone: 'Asia/Taipei', year: 'numeric', month: '2-digit', day: '2-digit'
        }).format(new Date());
    }

    function formatTaipei(value) {
        var date = new Date(value);
        return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('sv-SE', {
            timeZone: 'Asia/Taipei', year: 'numeric', month: '2-digit', day: '2-digit',
            hour: '2-digit', minute: '2-digit', hour12: false
        }).format(date);
    }

    function showPanel() {
        setError('');
        var $body = $('#shipInfoViewModal .modal-body');
        editScrollTop = $body.scrollTop();
        $body.addClass('arrival-schedule-active').scrollTop(0);
        $('#shipInfoArrivalScheduleToolbar, #shipInfoViewForm').addClass('d-none');
        $('#shipInfoControlledGoodsPanel').removeClass('d-none');
        $('#btnShipInfoViewSave, #btnShipInfoViewCancelEdit').prop('disabled', true);
        $('#shipInfoControlledGoodsDate').val(currentScheduleDate());
        loadSchedules();
    }

    function closePanel() {
        ++loadGeneration;
        $('#shipInfoControlledGoodsPanel').addClass('d-none');
        $('#shipInfoViewForm').removeClass('d-none');
        if (app.state.viewModalEditing && app.state.viewModalKind === 'header')
            $('#shipInfoArrivalScheduleToolbar').removeClass('d-none');
        $('#shipInfoViewModal .modal-body').removeClass('arrival-schedule-active').scrollTop(editScrollTop);
        $('#btnShipInfoViewSave, #btnShipInfoViewCancelEdit').prop('disabled', false);
        setError('');
    }

    function loadSchedules() {
        var generation = ++loadGeneration;
        var $rows = $('#shipInfoControlledGoodsRows').empty();
        var $previews = $('#shipInfoControlledGoodsPreviews').empty();
        var eta = $('#shipInfoControlledGoodsDate').val();
        if (!eta) return;
        $.getJSON(app.urls.controlledGoodsSchedules, { scheduleDate: eta }).done(function (response) {
            if (generation !== loadGeneration) return;
            if (!response || !response.success) {
                setError((response && response.message) || app.messages.saveFailed);
                return;
            }
            var rows = response.data || [];
            var current = String(app.state.viewModalKey || '').toLowerCase();
            rows.sort(function (a, b) {
                var aCurrent = String(a.headerId || a.HeaderId || '').toLowerCase() === current;
                var bCurrent = String(b.headerId || b.HeaderId || '').toLowerCase() === current;
                return aCurrent === bCurrent ? 0 : (aCurrent ? -1 : 1);
            });
            if (!rows.length) {
                $rows.append($('<tr>').append($('<td colspan="5" class="text-center text-muted">')
                    .text(app.messages.controlledGoodsEmpty)));
                return;
            }
            var labels = {
                Pending: app.messages.arrivalSchedulePending,
                Sending: app.messages.arrivalScheduleSending,
                Sent: app.messages.arrivalScheduleSent,
                Failed: app.messages.arrivalScheduleFailed
            };
            var colors = { Pending: 'bg-secondary', Sending: 'bg-info', Sent: 'bg-success', Failed: 'bg-danger' };
            rows.forEach(function (row) {
                var id = row.id || row.Id;
                var isCurrent = String(row.headerId || row.HeaderId || '').toLowerCase() === current;
                var state = row.state || row.State || 'Pending';
                var $invoice = $('<td>').text(row.invoiceNo || row.InvoiceNo || '');
                if (isCurrent) $invoice.append($('<span class="badge bg-primary ms-2">')
                    .text(app.messages.arrivalScheduleCurrent));
                var $action = state === 'Pending'
                    ? $('<button type="button" class="btn btn-sm btn-outline-danger">')
                        .text(app.messages.arrivalScheduleCancel).attr('data-controlled-schedule-id', id)
                    : '—';
                $rows.append($('<tr>').toggleClass('table-primary', isCurrent)
                    .append($('<td>').text(formatTaipei(row.scheduledAtUtc || row.ScheduledAtUtc)))
                    .append($invoice)
                    .append($('<td>').text(row.eta || row.Eta || ''))
                    .append($('<td>').append($('<span class="badge">').addClass(colors[state] || 'bg-secondary')
                        .text(labels[state] || state)))
                    .append($('<td>').append($action)));

                var $card = $('<div class="border rounded p-3 mb-3">');
                var $heading = $('<h6>').text(app.messages.controlledGoodsPreview + ' — '
                    + (row.invoiceNo || row.InvoiceNo || ''));
                if (isCurrent) $heading.append($('<span class="badge bg-primary ms-2">')
                    .text(app.messages.arrivalScheduleCurrent));
                var $content = $('<div>').text(app.messages.loading || '...');
                $card.append($heading, $content);
                $previews.append($card);
                $.getJSON(app.urls.controlledGoodsPreview, { scheduleId: id }).done(function (previewResponse) {
                    if (generation !== loadGeneration) return;
                    if (!previewResponse || !previewResponse.success || !previewResponse.data) return;
                    var preview = previewResponse.data;
                    $content.empty()
                        .append($('<p class="mb-1">').append($('<strong>').text('MailTo: '),
                            $('<span>').text(preview.mailTo || preview.MailTo || '')))
                        .append($('<p class="mb-1">').append($('<strong>').text('CcTo: '),
                            $('<span>').text(preview.ccTo || preview.CcTo || '')))
                        .append($('<p class="mb-2">').append($('<strong>').text(app.messages.arrivalSchedulePreviewSubjectLabel + ': '),
                            $('<span>').text(preview.subject || preview.Subject || '')))
                        .append($('<div class="table-responsive">').html(preview.bodyHtml || preview.BodyHtml || ''));
                }).fail(function (xhr) {
                    if (generation === loadGeneration)
                        $content.text((xhr.responseJSON && xhr.responseJSON.message) || app.messages.saveFailed);
                });
            });
        }).fail(function (xhr) {
            if (generation === loadGeneration)
                setError((xhr.responseJSON && xhr.responseJSON.message) || app.messages.saveFailed);
        });
    }

    $(function () {
        $('#btnShipInfoControlledGoodsNotice').on('click', showPanel);
        $('#btnShipInfoControlledGoodsClose').on('click', closePanel);
        $('#shipInfoControlledGoodsDate').on('change', loadSchedules);
        $('#shipInfoControlledGoodsRows').on('click', '[data-controlled-schedule-id]', function () {
            var $button = $(this).prop('disabled', true);
            $.post(app.urls.controlledGoodsCancel,
                { scheduleId: $button.attr('data-controlled-schedule-id') })
                .done(function (response) {
                    if (!response || !response.success) {
                        setError((response && response.message) || app.messages.saveFailed);
                        return;
                    }
                    var data = response.data || {};
                    if (String(data.headerId || data.HeaderId || '').toLowerCase() === String(app.state.viewModalKey || '').toLowerCase()) {
                        $.getJSON(app.urls.getHeader, { headerKey: app.state.viewModalKey }).done(function (result) {
                            if (!result || !result.success || !result.data) return;
                            app.state.viewModalData = result.data;
                            $('#shipInfoViewForm .shipinfo-control[data-field="ControlledGoodsNoticeFlag"]').val(result.data.ControlledGoodsNoticeFlag || result.data.controlledGoodsNoticeFlag || 'Y');
                            $('#shipInfoViewForm .shipinfo-control[data-field="ControlledGoodsNoticeReason"]').val(result.data.ControlledGoodsNoticeReason || result.data.controlledGoodsNoticeReason || '管制品到貨');
                            $('#shipInfoViewForm [data-meta-field="updateTime"]').val(result.data.UpdateTime || result.data.updateTime || '');
                        });
                    }
                    loadSchedules();
                }).fail(function (xhr) {
                    setError((xhr.responseJSON && xhr.responseJSON.message) || app.messages.saveFailed);
                }).always(function () { $button.prop('disabled', false); });
        });
        $('#shipInfoViewModal').on('hidden.bs.modal', function () {
            ++loadGeneration;
            $('#shipInfoControlledGoodsPanel').addClass('d-none');
            $('#shipInfoViewForm').removeClass('d-none');
            $('#shipInfoViewModal .modal-body').removeClass('arrival-schedule-active');
            $('#btnShipInfoViewSave, #btnShipInfoViewCancelEdit').prop('disabled', false);
        });
    });
})(window, window.jQuery);
