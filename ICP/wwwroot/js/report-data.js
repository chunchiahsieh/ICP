(function (global, $) {
    'use strict';

    var app = global.ReportDataApp = global.ReportDataApp || {};
    var headerTableInstance = null;
    var detailTableInstance = null;

    app.state = {
        pageConfig: null,
        selectedHeaderKey: null,
        selectedHeaderRowKey: null,
        selectedHeaderRow: null
    };

    function getFiltersApi() {
        return global.ProTableFilters || null;
    }

    function isFieldVisible(field) {
        return field.visible !== false && field.Visible !== false;
    }

    function isFieldSearchable(field) {
        return field.searchable !== false && field.Searchable !== false;
    }

    function getPageConfigFields(kind) {
        var pageConfig = app.state.pageConfig || {};
        if (kind === 'detail') {
            return pageConfig.detailFields || pageConfig.DetailFields || [];
        }

        return pageConfig.headerFields || pageConfig.HeaderFields || [];
    }

    function buildFilterFieldMap(fields, tableId) {
        var map = {};
        var filtersApi = getFiltersApi();
        (fields || []).forEach(function (field) {
            var fieldName = field.fieldName || field.FieldName;
            if (!fieldName || !isFieldVisible(field) || !isFieldSearchable(field)) {
                return;
            }

            map['filter-' + tableId + '-' + fieldName] = {
                fieldName: fieldName,
                filterType: filtersApi
                    ? filtersApi.resolveFieldFilterType(field)
                    : (field.filterType || field.FilterType || 'Checkbox')
            };
        });
        return map;
    }

    function buildFilterHooks() {
        var filtersApi = getFiltersApi();
        if (!filtersApi) {
            return {};
        }

        return {
            customGetFilterValues: filtersApi.getProTableFilterValues,
            customBuildQueryPayload: filtersApi.buildProTableQueryPayload,
            customRestoreFilterValues: filtersApi.restoreProTableFilterValues
        };
    }

    function resolveInvoiceKey(rowData) {
        if (!rowData) {
            return null;
        }

        return rowData.HeaderKey || rowData.headerKey || null;
    }

    function parseRowData($row) {
        var raw = $row.attr('data-row');
        if (!raw) {
            return null;
        }

        try {
            return JSON.parse(raw);
        } catch (error) {
            return null;
        }
    }

    function formatCaseStatusLabel(value) {
        var key = String(value || '');
        var map = (app.messages && app.messages.caseStatus) || {};
        return map[key] || key;
    }

    function syncStickyHeaderOffset($scope) {
        ($scope || $('#reportHeaderDataDiv, #reportDetailDataDiv')).find('.shipinfo-pro-table').each(function () {
            var table = this;
            var $titleRow = $(table).find('thead.shipinfo-sticky-header > tr').first();
            if (!$titleRow.length) {
                return;
            }

            var titleHeight = Math.ceil($titleRow[0].getBoundingClientRect().height);
            table.style.setProperty('--shipinfo-header-row-height', titleHeight + 'px');
        });
    }

    function resolveInitialSort(kind, pageConfig) {
        var fields = getPageConfigFields(kind)
            .filter(isFieldVisible)
            .sort(function (a, b) {
                var orderA = a.displayOrder != null ? a.displayOrder : (a.DisplayOrder != null ? a.DisplayOrder : 0);
                var orderB = b.displayOrder != null ? b.displayOrder : (b.DisplayOrder != null ? b.DisplayOrder : 0);
                if (orderA !== orderB) {
                    return orderA - orderB;
                }

                var nameA = (a.fieldName || a.FieldName || '').toLowerCase();
                var nameB = (b.fieldName || b.FieldName || '').toLowerCase();
                return nameA.localeCompare(nameB);
            });

        var sortConfig = kind === 'detail'
            ? (pageConfig.detailInitialSort || pageConfig.DetailInitialSort)
            : (pageConfig.headerInitialSort || pageConfig.HeaderInitialSort);
        var leadingOffset = kind === 'header' ? 1 : 0;
        var fallback = kind === 'header' ? [[1, 'desc']] : [[0, 'asc']];

        if (!sortConfig) {
            return fallback;
        }

        var targetFieldName = sortConfig.fieldName || sortConfig.FieldName;
        if (!targetFieldName) {
            return fallback;
        }

        var fieldIndex = -1;
        fields.forEach(function (field, index) {
            var fieldName = field.fieldName || field.FieldName;
            if (fieldName && fieldName.toLowerCase() === String(targetFieldName).toLowerCase()) {
                fieldIndex = index;
            }
        });

        if (fieldIndex < 0) {
            return fallback;
        }

        var direction = String(sortConfig.direction || sortConfig.Direction || 'asc').toLowerCase() === 'desc'
            ? 'desc'
            : 'asc';

        return [[leadingOffset + fieldIndex, direction]];
    }

    function buildTableConfig(kind) {
        var urls = app.urls;
        var pageConfig = app.state.pageConfig || {};
        var tableId = kind === 'header' ? 'reportHeaderTable' : 'reportDetailTable';
        var fields = getPageConfigFields(kind);
        var filtersApi = getFiltersApi();

        var base = $.extend(true, {
            tableSelector: '#' + tableId,
            dataDivSelector: kind === 'header' ? '#reportHeaderDataDiv' : '#reportDetailDataDiv',
            filterFieldMap: buildFilterFieldMap(fields, tableId),
            filterOptionsUrl: kind === 'header' ? urls.headerFilterOptions : urls.detailFilterOptions,
            queryUrl: kind === 'header' ? urls.queryHeader : urls.queryDetail,
            pageLength: 10,
            preserveSort: false,
            initialSort: resolveInitialSort(kind, pageConfig),
            dataTableOptions: {
                columnDefs: kind === 'header'
                    ? [{ orderable: false, targets: [0] }]
                    : []
            },
            onDraw: function ($div) {
                syncStickyHeaderOffset($div);
            },
            formatFilterOptionLabel: function (column, value) {
                if (column === 'DepositCaseStatus' || column === 'ArurCaseStatus') {
                    return formatCaseStatusLabel(value);
                }

                return value;
            }
        }, buildFilterHooks());

        if (kind === 'detail') {
            base.pageLength = -1;
            base.dataTableOptions = $.extend(true, {}, base.dataTableOptions, global.ProDataTables.linkedDetailTableOptions);
            base.autoLoad = false;
            base.filterOptionsExtraParams = function () {
                return { headerKey: app.state.selectedHeaderKey || '' };
            };
            base.extraQueryParams = function () {
                return { headerKey: app.state.selectedHeaderKey || '' };
            };
            base.onAfterRender = function ($div) {
                syncStickyHeaderOffset($div);
                if (filtersApi && filtersApi.updateAllProTableFilterCounts) {
                    filtersApi.updateAllProTableFilterCounts($div, base.filterFieldMap);
                }
            };
        } else {
            base.onAfterRender = function ($div) {
                restoreHeaderSelection($div);
                syncStickyHeaderOffset($div);
                if (filtersApi && filtersApi.updateAllProTableFilterCounts) {
                    filtersApi.updateAllProTableFilterCounts($div, base.filterFieldMap);
                }
            };
        }

        return ProDataTables.buildConfig(base);
    }

    function findHeaderRow($scope, headerRowKey) {
        if (!headerRowKey) {
            return $();
        }

        var $match = $();
        $scope.find('#reportHeaderTable tbody tr[data-header-id]').each(function () {
            if ($(this).attr('data-header-id') === headerRowKey) {
                $match = $(this);
                return false;
            }
        });
        return $match;
    }

    function restoreHeaderSelection($scope) {
        var state = app.state;
        var $root = $scope || $('#reportHeaderDataDiv');
        $root.find('#reportHeaderTable tbody tr').removeClass('table-primary');
        $root.find('#reportHeaderTable tbody tr .report-header-radio').prop('checked', false);

        if (!state.selectedHeaderRowKey) {
            return;
        }

        var $selected = findHeaderRow($root, state.selectedHeaderRowKey);
        if (!$selected.length) {
            return;
        }

        $selected.addClass('table-primary');
        $selected.find('.report-header-radio').prop('checked', true);
        state.selectedHeaderRow = parseRowData($selected);
    }

    app.selectHeader = function (headerRowKey, rowData) {
        var state = app.state;
        state.selectedHeaderRowKey = headerRowKey || null;
        state.selectedHeaderKey = resolveInvoiceKey(rowData) || headerRowKey || null;
        state.selectedHeaderRow = rowData || null;
        restoreHeaderSelection();
        app.reloadDetailTable();
    };

    app.reloadDetailTable = function () {
        if (!app.state.selectedHeaderKey) {
            $('#reportDetailDataDiv').empty();
            return;
        }

        if (detailTableInstance && detailTableInstance.reload) {
            detailTableInstance.reload();
        }
    };

    function collectHeaderFilterPayload() {
        var filtersApi = getFiltersApi();
        if (!filtersApi || !filtersApi.getProTableFilterValues || !filtersApi.buildProTableQueryPayload) {
            return {};
        }

        var $div = $('#reportHeaderDataDiv');
        var tableId = 'reportHeaderTable';
        var fields = getPageConfigFields('header');
        var filterFieldMap = buildFilterFieldMap(fields, tableId);
        var saved = filtersApi.getProTableFilterValues($div, filterFieldMap);
        return filtersApi.buildProTableQueryPayload(saved, filterFieldMap) || {};
    }

    function downloadExcel() {
        var payload = collectHeaderFilterPayload();
        var form = document.createElement('form');
        form.method = 'POST';
        form.action = app.urls.downloadExcel;
        form.style.display = 'none';

        Object.keys(payload).forEach(function (key) {
            var input = document.createElement('input');
            input.type = 'hidden';
            input.name = key;
            input.value = payload[key] == null ? '' : String(payload[key]);
            form.appendChild(input);
        });

        document.body.appendChild(form);
        form.submit();
        document.body.removeChild(form);
    }

    app.init = function (config) {
        app.urls = config.urls || {};
        app.messages = config.messages || {};

        $.get(app.urls.pageConfig)
            .done(function (response) {
                app.state.pageConfig = (response && (response.data || response.Data)) || response || {};
                if (!global.ProDataTables || !ProDataTables.initUsers) {
                    return;
                }

                headerTableInstance = ProDataTables.initUsers(buildTableConfig('header'));
                detailTableInstance = ProDataTables.initUsers(buildTableConfig('detail'));

                if (global.ProTableFilters) {
                    global.ProTableFilters.bindProTableFilterActions({
                        pageSelector: '.report-data-page',
                        resolveReload: function ($container) {
                            if ($container.is('#reportHeaderDataDiv') && headerTableInstance) {
                                return function () { headerTableInstance.reload(); };
                            }

                            if ($container.is('#reportDetailDataDiv') && detailTableInstance) {
                                return function () { detailTableInstance.reload(); };
                            }

                            return null;
                        }
                    });
                }

                $(window).off('resize.reportStickyHeader').on('resize.reportStickyHeader', function () {
                    syncStickyHeaderOffset();
                });
            });

        $(document)
            .off('change.reportHeaderRadio', '#reportHeaderDataDiv .report-header-radio')
            .on('change.reportHeaderRadio', '#reportHeaderDataDiv .report-header-radio', function (e) {
                e.stopPropagation();
                var $row = $(this).closest('tr');
                app.selectHeader($row.attr('data-header-id'), parseRowData($row));
            });

        $(document)
            .off('click.reportHeaderRow', '#reportHeaderDataDiv #reportHeaderTable tbody tr[data-header-id]')
            .on('click.reportHeaderRow', '#reportHeaderDataDiv #reportHeaderTable tbody tr[data-header-id]', function (e) {
                if ($(e.target).closest('.report-header-radio, .dropdown-menu, .column-filter-dropdown, .pro-table-filter').length) {
                    return;
                }

                var $current = $(this);
                $current.find('.report-header-radio').prop('checked', true);
                app.selectHeader($current.attr('data-header-id'), parseRowData($current));
            });

        $('#btnReportDownloadExcel').off('click.reportExcel').on('click.reportExcel', function () {
            try {
                downloadExcel();
            } catch (error) {
                alert((app.messages && app.messages.downloadFailed) || 'Download failed');
            }
        });
    };
})(window, window.jQuery);
