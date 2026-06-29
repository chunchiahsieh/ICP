(function (global, $) {
    'use strict';

    var app = global.ShipInfoApp;
    if (!app || !$) {
        return;
    }

    var headerTableInstance = null;
    var detailTableInstance = null;

    function buildFilterFieldMap(fields, tableId) {
        var map = {};
        (fields || []).forEach(function (field) {
            var fieldName = field.fieldName || field.FieldName;
            if (!fieldName) {
                return;
            }

            var visible = field.visible !== false && field.Visible !== false;
            if (!visible) {
                return;
            }

            map['filter-' + tableId + '-' + fieldName] = fieldName;
        });
        return map;
    }

    function getPageConfigFields(kind) {
        var pageConfig = app.state.pageConfig || {};
        if (kind === 'detail') {
            return pageConfig.detailFields || pageConfig.DetailFields || [];
        }

        return pageConfig.headerFields || pageConfig.HeaderFields || [];
    }

    function syncStickyHeaderOffset($scope) {
        ($scope || $('#shipInfoHeaderDataDiv, #shipInfoDetailDataDiv')).find('.shipinfo-pro-table').each(function () {
            var table = this;
            var $table = $(table);
            var $titleRow = $table.find('thead.shipinfo-sticky-header > tr').first();
            if (!$titleRow.length) {
                return;
            }

            var titleHeight = Math.ceil($titleRow[0].getBoundingClientRect().height);
            table.style.setProperty('--shipinfo-header-row-height', titleHeight + 'px');
        });
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

    function findHeaderRow($scope, headerRowKey) {
        if (!headerRowKey) {
            return $();
        }

        var $match = $();
        $scope.find('#shipInfoHeaderTable tbody tr[data-header-id]').each(function () {
            if ($(this).attr('data-header-id') === headerRowKey) {
                $match = $(this);
                return false;
            }
        });
        return $match;
    }

    function bindHeaderTableEvents($scope) {
        syncStickyHeaderOffset($scope);
        app.restoreHeaderSelection($scope);
    }

    $(document)
        .off('change.shipinfoHeaderRadio', '#shipInfoHeaderDataDiv .shipinfo-header-radio')
        .on('change.shipinfoHeaderRadio', '#shipInfoHeaderDataDiv .shipinfo-header-radio', function (e) {
            e.stopPropagation();
            var $row = $(this).closest('tr');
            app.selectHeader($row.attr('data-header-id'), parseRowData($row));
        });

    $(document)
        .off('click.shipinfoHeaderEdit', '#shipInfoHeaderDataDiv .shipinfo-header-edit-btn')
        .on('click.shipinfoHeaderEdit', '#shipInfoHeaderDataDiv .shipinfo-header-edit-btn', function (e) {
            e.preventDefault();
            e.stopPropagation();
            var headerId = $(this).attr('data-header-id') || $(this).closest('tr').attr('data-header-id');
            app.openViewModal('header', headerId);
        });

    $(document)
        .off('click.shipinfoHeaderRow', '#shipInfoHeaderDataDiv #shipInfoHeaderTable tbody tr[data-header-id]')
        .on('click.shipinfoHeaderRow', '#shipInfoHeaderDataDiv #shipInfoHeaderTable tbody tr[data-header-id]', function (e) {
            if ($(e.target).closest('.shipinfo-header-radio, .shipinfo-header-edit-btn, .dropdown-menu, .column-filter-dropdown').length) {
                return;
            }

            var $current = $(this);
            $current.find('.shipinfo-header-radio').prop('checked', true);
            app.selectHeader($current.attr('data-header-id'), parseRowData($current));
        });

    function bindDetailTableEvents($scope) {
        syncStickyHeaderOffset($scope);

        $scope.find('#shipInfoDetailTable .shipinfo-detail-edit-btn').off('click.shipinfo').on('click.shipinfo', function (e) {
            e.preventDefault();
            e.stopPropagation();
            var detailId = $(this).attr('data-detail-id') || $(this).closest('tr').attr('data-detail-id');
            app.openViewModal('detail', detailId);
        });
    }

    app.restoreHeaderSelection = function ($scope) {
        var state = app.state;
        var $root = $scope || $('#shipInfoHeaderDataDiv');
        $root.find('#shipInfoHeaderTable tbody tr').removeClass('table-primary');
        $root.find('#shipInfoHeaderTable tbody tr .shipinfo-header-radio').prop('checked', false);

        if (!state.selectedHeaderRowKey) {
            return;
        }

        var $selected = findHeaderRow($root, state.selectedHeaderRowKey);
        if (!$selected.length) {
            return;
        }

        $selected.addClass('table-primary');
        $selected.find('.shipinfo-header-radio').prop('checked', true);
        state.selectedHeaderRow = parseRowData($selected);
    };

    app.initProTables = function () {
        var urls = app.urls;
        if (!global.ProDataTables || !ProDataTables.initUsers) {
            return;
        }

        headerTableInstance = ProDataTables.initUsers(ProDataTables.buildConfig({
            tableSelector: '#shipInfoHeaderTable',
            dataDivSelector: '#shipInfoHeaderDataDiv',
            filterFieldMap: buildFilterFieldMap(getPageConfigFields('header'), 'shipInfoHeaderTable'),
            filterOptionsUrl: urls.headerFilterOptions,
            queryUrl: urls.queryHeader,
            pageLength: 50,
            initialSort: [[3, 'desc']],
            onAfterRender: function ($div) {
                bindHeaderTableEvents($div);
                app.updateHeaderActionState();
            }
        }));

        detailTableInstance = ProDataTables.initUsers(ProDataTables.buildConfig({
            tableSelector: '#shipInfoDetailTable',
            dataDivSelector: '#shipInfoDetailDataDiv',
            filterFieldMap: buildFilterFieldMap(getPageConfigFields('detail'), 'shipInfoDetailTable'),
            filterOptionsUrl: urls.detailFilterOptions,
            filterOptionsExtraParams: function () {
                return { headerKey: app.state.selectedHeaderKey || '' };
            },
            queryUrl: urls.queryDetail,
            pageLength: 50,
            autoLoad: false,
            preserveSort: false,
            initialSort: [[1, 'asc']],
            dataTableOptions: {
                columnDefs: [
                    { orderable: false, targets: 0 },
                    { type: 'num', targets: 1 }
                ]
            },
            extraQueryParams: function () {
                return { headerKey: app.state.selectedHeaderKey || '' };
            },
            onAfterRender: function ($div) {
                bindDetailTableEvents($div);
            }
        }));

        app.headerTableInstance = headerTableInstance;
        app.detailTableInstance = detailTableInstance;

        $(window).off('resize.shipinfoStickyHeader').on('resize.shipinfoStickyHeader', function () {
            syncStickyHeaderOffset();
        });
    };

    app.reloadHeaderTable = function () {
        if (headerTableInstance && headerTableInstance.reload) {
            headerTableInstance.reload();
        }
    };

    app.reloadDetailTable = function () {
        if (!app.state.selectedHeaderKey) {
            $('#shipInfoDetailDataDiv').empty();
            return;
        }

        if (detailTableInstance && detailTableInstance.reload) {
            detailTableInstance.reload();
        }
    };
})(window, window.jQuery);
