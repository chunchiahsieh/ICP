(function (global, $) {
    'use strict';

    var app = global.ShipInfoApp;
    if (!app || !$) return;
    var editScrollTop = 0;

    function setError(message) {
        $('#shipInfoDeliveryDelayError').text(message || '').toggleClass('d-none', !message);
    }

    function taipeiToday() {
        return new Intl.DateTimeFormat('sv-SE', {
            timeZone: 'Asia/Taipei', year: 'numeric', month: '2-digit', day: '2-digit'
        }).format(new Date());
    }

    function openPanel() {
        var $modalBody = $('#shipInfoViewModal .modal-body');
        editScrollTop = $modalBody.scrollTop();
        $modalBody.addClass('arrival-schedule-active').scrollTop(0);
        $('#shipInfoArrivalScheduleToolbar, #shipInfoViewForm').addClass('d-none');
        $('#shipInfoDeliveryDelayPanel').removeClass('d-none');
        $('#btnShipInfoViewSave, #btnShipInfoViewCancelEdit').prop('disabled', true);
        $('#shipInfoDeliveryDelayDate').val(taipeiToday());
        loadPreview();
    }

    function closePanel() {
        $('#shipInfoDeliveryDelayPanel').addClass('d-none');
        $('#shipInfoViewForm').removeClass('d-none');
        if (app.state.viewModalEditing && app.state.viewModalKind === 'header')
            $('#shipInfoArrivalScheduleToolbar').removeClass('d-none');
        $('#btnShipInfoViewSave, #btnShipInfoViewCancelEdit').prop('disabled', false);
        $('#shipInfoViewModal .modal-body').removeClass('arrival-schedule-active').scrollTop(editScrollTop);
        setError('');
    }

    function loadPreview() {
        setError('');
        var $rows = $('#shipInfoDeliveryDelayRows').empty();
        var $content = $('#shipInfoDeliveryDelayContent').addClass('d-none');
        var $empty = $('#shipInfoDeliveryDelayEmpty').removeClass('d-none');
        $('#shipInfoDeliveryDelaySendTimes').text('');
        var date = $('#shipInfoDeliveryDelayDate').val();
        if (!date) return;
        $.getJSON(app.urls.deliveryDelayPreview, { delayNotificationDate: date }).done(function (response) {
            if (!response || !response.success || !response.data) {
                setError((response && response.message) || app.messages.saveFailed);
                return;
            }
            var preview = response.data;
            var sendTimes = preview.sendTimes || preview.SendTimes || [];
            $('#shipInfoDeliveryDelaySendTimes').text(sendTimes.join('; ') || preview.frequency || preview.Frequency || '');
            var rows = preview.rows || preview.Rows || [];
            if (!rows.length) {
                $rows.append($('<tr>').append($('<td colspan="8" class="text-center text-muted">')
                    .text(app.messages.deliveryDelayNoRows)));
                return;
            }
            rows.forEach(function (row) {
                var isCurrent = String(row.headerId || row.HeaderId || '').toLowerCase()
                    === String(app.state.viewModalKey || '').toLowerCase();
                var status = row.status || row.Status || 'Unknown';
                var statusLabels = {
                    Pending: app.messages.deliveryDelayStatusNotSent,
                    Sending: app.messages.arrivalScheduleSending,
                    Sent: app.messages.arrivalScheduleSent,
                    Failed: app.messages.arrivalScheduleFailed,
                    Unknown: app.messages.deliveryDelayStatusUnknown
                };
                var statusColors = { Pending: 'bg-secondary', Sending: 'bg-info', Sent: 'bg-success', Failed: 'bg-danger', Unknown: 'bg-secondary' };
                $rows.append($('<tr>').toggleClass('table-primary', isCurrent)
                    .append($('<td>').text(row.invoiceNo || row.InvoiceNo || ''))
                    .append($('<td>').text(row.hawb || row.Hawb || ''))
                    .append($('<td>').text(row.warehouse || row.Warehouse || ''))
                    .append($('<td>').text(row.flt || row.Flt || ''))
                    .append($('<td>').text(row.eta || row.Eta || ''))
                    .append($('<td>').text(row.reason || row.Reason || ''))
                    .append($('<td>').text(row.notificationDate || row.NotificationDate || ''))
                    .append($('<td>').append($('<span class="badge">')
                        .addClass(statusColors[status] || 'bg-secondary')
                        .text(statusLabels[status] || status))));
            });
            $('#shipInfoDeliveryDelayTo').text(preview.mailTo || preview.MailTo || '');
            $('#shipInfoDeliveryDelayCc').text(preview.ccTo || preview.CcTo || '');
            $('#shipInfoDeliveryDelaySubject').text(preview.subject || preview.Subject || '');
            $('#shipInfoDeliveryDelayBody').html(preview.bodyHtml || preview.BodyHtml || '');
            $empty.addClass('d-none');
            $content.removeClass('d-none');
        }).fail(function (xhr) {
            setError((xhr.responseJSON && xhr.responseJSON.message) || app.messages.saveFailed);
        });
    }

    $(function () {
        $('#btnShipInfoDeliveryDelayPreview').on('click', openPanel);
        $('#btnShipInfoDeliveryDelayClose').on('click', closePanel);
        $('#shipInfoDeliveryDelayDate').on('change', loadPreview);
        $('#shipInfoViewModal').on('hidden.bs.modal', function () {
            $('#shipInfoDeliveryDelayPanel').addClass('d-none');
            $('#shipInfoViewForm').removeClass('d-none');
            $('#shipInfoViewModal .modal-body').removeClass('arrival-schedule-active');
            $('#btnShipInfoViewSave, #btnShipInfoViewCancelEdit').prop('disabled', false);
        });
    });
})(window, window.jQuery);
