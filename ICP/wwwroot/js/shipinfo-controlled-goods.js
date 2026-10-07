(function (global, $) {
    'use strict';
    var app = global.ShipInfoApp;
    if (!app || !$) return;
    var editScrollTop = 0;
    var loadGeneration = 0;

    function setError(message) {
        $('#shipInfoControlledGoodsError').text(message || '').toggleClass('d-none', !message);
    }

    function currentEta() {
        var data = app.state.viewModalData || {};
        var eta = String(data.Eta || data.eta || '').replace(/\//g, '-').slice(0, 10);
        if (/^\d{4}-\d{2}-\d{2}$/.test(eta)) return eta;
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
        $('#shipInfoControlledGoodsEta').val(currentEta());
        loadSchedules();
        checkEligibility();
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
        var eta = $('#shipInfoControlledGoodsEta').val();
        if (!eta) return;
        $.getJSON(app.urls.controlledGoodsSchedules, { eta: eta }).done(function (response) {
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

    function checkEligibility() {
        $('#btnShipInfoControlledGoodsAdd').prop('disabled', true);
        $.getJSON(app.urls.controlledGoodsEligibility, { headerId: app.state.viewModalKey })
            .done(function (response) {
                if (!response || !response.success || !response.data) {
                    setError((response && response.message) || app.messages.saveFailed);
                    return;
                }
                var data = response.data;
                var canSchedule = data.canSchedule === true || data.CanSchedule === true;
                var reason = data.reason || data.Reason;
                $('#btnShipInfoControlledGoodsAdd').prop('disabled', !canSchedule);
                if (!canSchedule) {
                    var labels = {
                        NoElFlag: app.messages.controlledGoodsEligibility,
                        EtaUnavailable: app.messages.controlledGoodsEtaUnavailable,
                        AlreadyScheduled: app.messages.controlledGoodsAlreadyScheduled,
                        StatusUnavailable: app.messages.statusNotAllowed
                    };
                    setError(labels[reason] || reason);
                } else setError('');
            }).fail(function (xhr) {
                setError((xhr.responseJSON && xhr.responseJSON.message) || app.messages.saveFailed);
            });
    }

    $(function () {
        $('#btnShipInfoControlledGoodsNotice').on('click', showPanel);
        $('#btnShipInfoControlledGoodsClose').on('click', closePanel);
        $('#shipInfoControlledGoodsEta').on('change', loadSchedules);
        $('#btnShipInfoControlledGoodsAdd').on('click', function () {
            setError('');
            var $button = $(this).prop('disabled', true);
            $.post(app.urls.controlledGoodsAdd, { headerId: app.state.viewModalKey })
                .done(function (response) {
                    if (!response || !response.success) {
                        setError((response && response.message) || app.messages.saveFailed);
                        return;
                    }
                    var row = response.data || {};
                    $('#shipInfoControlledGoodsEta').val(row.eta || row.Eta || currentEta());
                    loadSchedules();
                    checkEligibility();
                }).fail(function (xhr) {
                    setError((xhr.responseJSON && xhr.responseJSON.message) || app.messages.saveFailed);
                }).always(function () { $button.prop('disabled', false); });
        });
        $('#shipInfoControlledGoodsRows').on('click', '[data-controlled-schedule-id]', function () {
            var $button = $(this).prop('disabled', true);
            $.post(app.urls.controlledGoodsCancel,
                { scheduleId: $button.attr('data-controlled-schedule-id') })
                .done(function (response) {
                    if (!response || !response.success) {
                        setError((response && response.message) || app.messages.saveFailed);
                        return;
                    }
                    loadSchedules();
                    checkEligibility();
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
