(function (global, $) {
    'use strict';

    var app = global.ShipInfoApp;
    if (!app || !$) return;
    var editScrollTop = 0;
    var previewGeneration = 0;
    var scheduleLoadGeneration = 0;

    function showScheduleError(message) {
        $('#shipInfoArrivalScheduleError').text(message || '').toggleClass('d-none', !message);
    }

    function showScheduleModal() {
        showScheduleError('');
        var $body = $('#shipInfoViewModal .modal-body');
        editScrollTop = $body.scrollTop();
        $body.addClass('arrival-schedule-active');
        $('#shipInfoArrivalScheduleToolbar').addClass('d-none');
        $('#shipInfoViewForm').addClass('d-none');
        $('#shipInfoArrivalSchedulePanel').removeClass('d-none');
        $('#btnShipInfoViewSave, #btnShipInfoViewCancelEdit, #btnShipInfoArrivalNoticeSchedule').prop('disabled', true);
        $body.scrollTop(0);
        loadToday();
    }

    function hideScheduleModal() {
        $('#shipInfoArrivalSchedulePanel').addClass('d-none');
        $('#shipInfoViewForm').removeClass('d-none');
        if (app.state.viewModalEditing && app.state.viewModalKind === 'header')
            $('#shipInfoArrivalScheduleToolbar').removeClass('d-none');
        $('#shipInfoViewModal .modal-body').removeClass('arrival-schedule-active');
        $('#btnShipInfoViewSave, #btnShipInfoViewCancelEdit, #btnShipInfoArrivalNoticeSchedule').prop('disabled', false);
        $('#shipInfoViewModal .modal-body').scrollTop(editScrollTop);
        showScheduleError('');
    }

    function fieldValue(data) {
        return String(data && (data.ArrivalNotice || data.arrivalNotice) || '').trim();
    }

    function currentSavedAddress() {
        var saved = fieldValue(app.state.viewModalData);
        var $input = $('#shipInfoViewForm .shipinfo-control[data-field="ArrivalNotice"]');
        if ($input.length && String($input.val() || '').trim() !== saved) {
            showScheduleError(app.messages.arrivalScheduleSaveFirst);
            return null;
        }
        return saved;
    }

    function validateMailTo(value) {
        if (!value) return app.messages.arrivalScheduleMailRequired;
        if (value.charAt(0) === '*') return app.messages.arrivalScheduleMailLegacyMarker;
        if (value.length >= 300) return app.messages.arrivalScheduleMailTooLong;
        var addresses = value.replace(/^mail:/i, '');
        if (/[,\r\n]/.test(addresses)) return app.messages.arrivalScheduleMailSeparator;
        var parts = addresses.split(';').map(function (address) { return address.trim(); })
            .filter(function (address) { return address.length > 0; });
        if (!parts.length) return app.messages.arrivalScheduleMailRequired;
        for (var i = 0; i < parts.length; i++) {
            if (!/^[^\s@<>;,]+@[^\s@<>;,]+\.[^\s@<>;,]+$/.test(parts[i]))
                return app.messages.arrivalScheduleMailInvalid.replace('{0}', String(i + 1));
        }
        return null;
    }

    function formatTaipei(value) {
        var date = new Date(value);
        return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('sv-SE', {
            timeZone: 'Asia/Taipei', year: 'numeric', month: '2-digit', day: '2-digit',
            hour: '2-digit', minute: '2-digit', hour12: false
        }).format(date);
    }

    function loadPreviews(rows) {
        var generation = ++previewGeneration;
        var current = String(app.state.viewModalKey || '').toLowerCase();
        var $container = $('#shipInfoArrivalSchedulePreviews').empty();
        rows.forEach(function (row) {
            var scheduleId = row.id || row.Id;
            var isCurrent = String(row.headerId || row.HeaderId || '').toLowerCase() === current;
            var $card = $('<div class="border rounded p-3 mb-3">');
            var $title = $('<h6>').text((app.messages.arrivalSchedulePreviewTitle || 'Arrival Notice preview')
                + ' — ' + (row.invoiceNo || row.InvoiceNo || ''));
            if (isCurrent) $title.append($('<span class="badge bg-primary ms-2">')
                .text(app.messages.arrivalScheduleCurrent));
            var $empty = $('<p class="text-muted mb-0">').text(app.messages.arrivalSchedulePreviewEmpty);
            var $content = $('<div class="d-none">');
            var $to = $('<span>');
            var $subject = $('<span>');
            var $body = $('<div class="table-responsive">');
            $content.append($('<p class="mb-1">').append($('<strong>').text(app.messages.arrivalSchedulePreviewToLabel + ': '), $to));
            $content.append($('<p class="mb-2">').append($('<strong>').text(app.messages.arrivalSchedulePreviewSubjectLabel + ': '), $subject));
            $content.append($body);
            $card.append($title, $empty, $content);
            $container.append($card);
            if (!(row.headerId || row.HeaderId)) return;
            $.getJSON(app.urls.arrivalScheduleQueuedPreview, { scheduleId: scheduleId }).done(function (response) {
                if (generation !== previewGeneration || !response || !response.success || !response.data) return;
                var preview = response.data;
                $to.text(preview.mailTo || preview.MailTo || '');
                $subject.text(preview.subject || preview.Subject || '');
                $body.html(preview.bodyHtml || preview.BodyHtml || '');
                $empty.addClass('d-none');
                $content.removeClass('d-none');
            }).fail(function (xhr) {
                if (generation === previewGeneration && xhr.responseJSON && xhr.responseJSON.message)
                    $empty.text(xhr.responseJSON.message);
            });
        });
    }

    function loadToday() {
        var loadGeneration = ++scheduleLoadGeneration;
        var $body = $('#shipInfoArrivalScheduleRows').empty();
        $.getJSON(app.urls.arrivalScheduleToday, { currentHeaderId: app.state.viewModalKey }).done(function (response) {
            if (loadGeneration !== scheduleLoadGeneration) return;
            if (!response || !response.success) {
                app.showToast((response && response.message) || app.messages.saveFailed, 'danger');
                loadPreviews([]);
                return;
            }
            var rows = response.data || [];
            var current = String(app.state.viewModalKey || '').toLowerCase();
            var alreadyScheduled = rows.some(function (row) {
                return String(row.headerId || row.HeaderId || '').toLowerCase() === current
                    && (row.state || row.State) === 'Pending';
            });
            $('#btnShipInfoArrivalScheduleAdd').prop('disabled', alreadyScheduled);
            if (!rows.length) {
                $body.append($('<tr>').append($('<td colspan="5" class="text-muted text-center">')
                    .text(app.messages.arrivalScheduleEmpty)));
                loadPreviews([]);
                return;
            }
            rows.sort(function (left, right) {
                var leftCurrent = String(left.headerId || left.HeaderId || '').toLowerCase() === current;
                var rightCurrent = String(right.headerId || right.HeaderId || '').toLowerCase() === current;
                return leftCurrent === rightCurrent ? 0 : (leftCurrent ? -1 : 1);
            });
            rows.forEach(function (row) {
                var id = row.id || row.Id;
                var isCurrent = String(row.headerId || row.HeaderId || '').toLowerCase()
                    === String(app.state.viewModalKey || '').toLowerCase();
                var status = row.state || row.State || 'Pending';
                var statusLabels = {
                    Pending: app.messages.arrivalSchedulePending,
                    Sending: app.messages.arrivalScheduleSending,
                    Sent: app.messages.arrivalScheduleSent,
                    Failed: app.messages.arrivalScheduleFailed
                };
                var statusColors = { Pending: 'bg-secondary', Sending: 'bg-info', Sent: 'bg-success', Failed: 'bg-danger' };
                var $cancel = $('<button type="button" class="btn btn-sm btn-outline-danger">')
                    .text(app.messages.arrivalScheduleCancel).attr('data-schedule-id', id);
                var $invoice = $('<td>').text(row.invoiceNo || row.InvoiceNo || '');
                if (isCurrent) $invoice.append($('<span class="badge bg-primary ms-2">')
                    .text(app.messages.arrivalScheduleCurrent));
                $body.append($('<tr>').toggleClass('table-primary', isCurrent)
                    .append($('<td>').text(formatTaipei(row.scheduledAtUtc || row.ScheduledAtUtc)))
                    .append($invoice)
                    .append($('<td>').text(row.mailTo || row.MailTo || ''))
                    .append($('<td>').append($('<span class="badge">').addClass(statusColors[status] || 'bg-secondary')
                        .text(statusLabels[status] || status)))
                    .append($('<td>').append(status === 'Pending' ? $cancel : '—')));
            });
            loadPreviews(rows);
        }).fail(function (xhr) {
            if (loadGeneration !== scheduleLoadGeneration) return;
            app.showToast((xhr.responseJSON && xhr.responseJSON.message) || app.messages.saveFailed, 'danger');
            loadPreviews([]);
        });
    }

    $(function () {
        $('#btnShipInfoArrivalNoticeSchedule').on('click', showScheduleModal);
        $('#btnShipInfoArrivalScheduleClose').on('click', hideScheduleModal);
        $('#shipInfoViewModal').on('hidden.bs.modal', function () {
            $('#shipInfoArrivalSchedulePanel').addClass('d-none');
            $('#shipInfoViewForm').removeClass('d-none');
            $('#shipInfoViewModal .modal-body').removeClass('arrival-schedule-active');
            $('#btnShipInfoViewSave, #btnShipInfoViewCancelEdit, #btnShipInfoArrivalNoticeSchedule')
                .prop('disabled', false);
            showScheduleError('');
        });
        $('#btnShipInfoArrivalScheduleAdd').on('click', function () {
            showScheduleError('');
            if (!app.state.viewModalEditing || app.state.viewModalKind !== 'header'
                || !app.state.viewModalKey) return;
            var savedAddress = currentSavedAddress();
            if (savedAddress === null) return;
            var validationError = validateMailTo(savedAddress);
            if (validationError) {
                showScheduleError(validationError);
                return;
            }
            var $button = $(this).prop('disabled', true);
            $.post(app.urls.arrivalScheduleAdd, { headerId: app.state.viewModalKey })
                .done(function (response) {
                    if (!response || !response.success) {
                        showScheduleError((response && response.message) || app.messages.saveFailed);
                        return;
                    }
                    var row = response.data || {};
                    app.showToast(formatTaipei(row.scheduledAtUtc || row.ScheduledAtUtc), 'success');
                    loadToday();
                }).fail(function (xhr) {
                    showScheduleError((xhr.responseJSON && xhr.responseJSON.message) || app.messages.saveFailed);
                }).always(function () { $button.prop('disabled', false); });
        });
        $('#shipInfoArrivalScheduleRows').on('click', '[data-schedule-id]', function () {
            var $button = $(this).prop('disabled', true);
            $.post(app.urls.arrivalScheduleCancel, { scheduleId: $button.attr('data-schedule-id') })
                .done(function (response) {
                    if (!response || !response.success) {
                        app.showToast((response && response.message) || app.messages.saveFailed, 'danger');
                        return;
                    }
                    loadToday();
                }).fail(function (xhr) {
                    app.showToast((xhr.responseJSON && xhr.responseJSON.message) || app.messages.saveFailed, 'danger');
                }).always(function () { $button.prop('disabled', false); });
        });
    });
})(window, window.jQuery);
