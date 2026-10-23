(function ($) {
    'use strict';
    $(function () {
        var $root = $('#forwarderHistory');
        if (!$root.length) return;
        var instance = null;
        var loadingConfig = false;
        function loadHistory() {
            if (loadingConfig) return;
            loadingConfig = true;
            $('#forwarderHistoryMessage').empty();
            $.getJSON($root.data('config-url')).done(function (config) {
                var fields = (config.fields || []).filter(function (field) { return field.visible !== false; });
                var map = {};
                fields.forEach(function (field) {
                    if (field.searchable) map['filter-forwarderHistoryTable-' + field.fieldName] = {
                        fieldName: field.fieldName, filterType: field.filterType
                    };
                });
                var sort = config.initialSort || {};
                var sortIndex = fields.findIndex(function (field) { return field.fieldName === sort.fieldName; });
                var filters = window.ProTableFilters;
                instance = window.ProDataTables.initUsers({
                    tableSelector: '#forwarderHistoryTable', dataDivSelector: '#forwarderHistoryDataDiv',
                    queryUrl: $root.data('list-url'), filterOptionsUrl: $root.data('filter-url'), filterFieldMap: map,
                    initialSort: sortIndex < 0 ? [] : [[sortIndex, sort.direction === 'asc' ? 'asc' : 'desc']],
                    preserveSort: false, pageLength: 10,
                    customGetFilterValues: filters.getProTableFilterValues,
                    customBuildQueryPayload: filters.buildProTableQueryPayload,
                    customRestoreFilterValues: filters.restoreProTableFilterValues,
                    formatFilterOptionLabel: function (column, value) {
                        if (value === '__BLANK__') return $root.data('blank-label');
                        if (column === 'Status') return $root.data('success-label');
                        if (column === 'DownloadOriginal') return value === 'Has' ? $root.data('download-label') : $root.data('missing-label');
                        return value;
                    },
                    onAfterRender: function ($div) {
                        filters.updateAllProTableFilterCounts($div, map);
                        if (window.bootstrap && window.bootstrap.Dropdown) {
                            $div.find('[data-bs-toggle="dropdown"]').each(function () {
                                window.bootstrap.Dropdown.getOrCreateInstance(this, { popperConfig: { strategy: 'fixed' } });
                            });
                        }
                        var table = $div.find('#forwarderHistoryTable')[0];
                        var firstRow = $div.find('thead tr').first()[0];
                        if (table && firstRow) table.style.setProperty('--forwarder-header-row-height', Math.ceil(firstRow.getBoundingClientRect().height) + 'px');
                    }
                });
                filters.bindProTableFilterActions({pageSelector: '#forwarderHistory', resolveReload: function () {
                    return function () { if (instance) instance.reload(); };
                }});
            }).fail(function (xhr) {
                $('#forwarderHistoryMessage').text('HTTP ' + xhr.status + ': ' + xhr.statusText);
            }).always(function () { loadingConfig = false; });
        }
        $('#forwarder-history-tab').on('shown.bs.tab', loadHistory);
        $('#forwarderHistoryRefresh').on('click', loadHistory);
    });
})(window.jQuery);
