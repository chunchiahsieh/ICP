(function (global, $) {
    'use strict';

    $(function () {
        if (!global.ProDataTables || !global.bootstrap) return;
        var page = global.NotificationMailPage;
        var tables = {};
        var fields = {
            delay: { ScheduledAt: 'Date', InvoiceNo: 'Text', Hawb: 'Text', Warehouse: 'Text', Flt: 'Text',
                Eta: 'Date', Reason: 'Text', NotificationDate: 'Date', Status: 'Checkbox' },
            arrival: { ScheduledAt: 'Date', InvoiceNo: 'Text', MailTo: 'Text', Status: 'Checkbox' },
            controlled: { ScheduledAt: 'Date', InvoiceNo: 'Text', Eta: 'Date', Status: 'Checkbox' }
        };
        ['delay', 'arrival', 'controlled'].forEach(function (type) {
            var fieldMap = {};
            Object.keys(fields[type]).forEach(function (name) {
                fieldMap['filter-' + type + '-' + name] = {
                    fieldName: name, filterType: fields[type][name]
                };
            });
            tables[type] = global.ProDataTables.initUsers({
                dataDivSelector: '#notificationMailList-' + type,
                tableSelector: '#notificationMailTable-' + type,
                queryUrl: page.queryUrl,
                filterOptionsUrl: page.filterOptionsUrl,
                filterFieldMap: fieldMap,
                initialSort: [[1, 'desc']],
                preserveSort: true,
                customGetFilterValues: global.ProTableFilters.getProTableFilterValues,
                customBuildQueryPayload: global.ProTableFilters.buildProTableQueryPayload,
                customRestoreFilterValues: global.ProTableFilters.restoreProTableFilterValues,
                extraQueryParams: { type: type },
                onAfterRender: function ($root) {
                    global.ProTableFilters.updateAllProTableFilterCounts($root, fieldMap);
                }
            });
        });
        global.ProTableFilters.bindProTableFilterActions({
            pageSelector: '.notification-mail-page',
            dataDivSelector: '[id^="notificationMailList-"]',
            resolveReload: function ($container) {
                var type = $container.attr('id').replace('notificationMailList-', '');
                return tables[type] ? function () { tables[type].reload(); } : null;
            }
        });
        $('button[data-bs-toggle="tab"]').on('shown.bs.tab', function () {
            $.fn.dataTable.tables({ visible: true, api: true }).columns.adjust();
        });

        var drawer = global.bootstrap.Offcanvas.getOrCreateInstance(
            document.getElementById('notificationMailPreviewDrawer'));
        var failureModal = global.bootstrap.Modal.getOrCreateInstance(
            document.getElementById('notificationMailFailureModal'));
        $(document).on('click', '.notification-mail-failure', function () {
            $('#notificationMailFailureBody').text($(this).attr('data-message') || page.noFailureDetails);
            failureModal.show();
        });
        $(document).on('click', '.notification-mail-preview', function () {
            var $button = $(this);
            var type = $button.data('type');
            $('#notificationMailPreviewTitle').text($button.data('invoice'));
            $('#notificationMailPreviewError').addClass('d-none').text('');
            $('#notificationMailPreviewTo, #notificationMailPreviewCc, #notificationMailPreviewSubject').text('');
            $('#notificationMailPreviewBody').empty();
            drawer.show();
            $.getJSON(page.previewUrl, {
                type: type,
                id: $button.data('id'),
                date: String($button.data('date') || '').replaceAll('/', '-')
            }).done(function (preview) {
                $('#notificationMailPreviewTo').text(preview.mailTo || '');
                $('#notificationMailPreviewCc').text(preview.ccTo || '');
                $('#notificationMailPreviewSubject').text(preview.subject || '');
                $('#notificationMailPreviewBody').html(preview.bodyHtml || '');
            }).fail(function () {
                $('#notificationMailPreviewError').removeClass('d-none').text(page.previewLoadFailed);
            });
        });
    });
})(window, window.jQuery);
