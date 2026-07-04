(function (global, $) {
    'use strict';

    var app = global.ShipInfoApp;
    if (!app || !$) {
        return;
    }

    var headerTableInstance = null;
    var detailTableInstance = null;

    function isFieldVisible(field) {
        return field.visible !== false && field.Visible !== false;
    }

    function isFieldSearchable(field) {
        return field.searchable === true || field.Searchable === true;
    }

    function buildFilterFieldMap(fields, tableId) {
        var map = {};
        (fields || []).forEach(function (field) {
            var fieldName = field.fieldName || field.FieldName;
            if (!fieldName || !isFieldVisible(field) || !isFieldSearchable(field)) {
                return;
            }

            map['filter-' + tableId + '-' + fieldName] = fieldName;
        });
        return map;
    }

    function buildColumnDefs(kind) {
        if (kind === 'header') {
            return [{ orderable: false, targets: [0, 1] }];
        }

        return [
            { orderable: false, targets: 0 },
            { type: 'num', targets: 1 }
        ];
    }

    function buildShipInfoTableConfig(kind, overrides) {
        var urls = app.urls;
        var pageConfig = app.state.pageConfig || {};
        var tableId = kind === 'header' ? 'shipInfoHeaderTable' : 'shipInfoDetailTable';
        var fields = getPageConfigFields(kind);

        var base = {
            tableSelector: '#' + tableId,
            dataDivSelector: kind === 'header' ? '#shipInfoHeaderDataDiv' : '#shipInfoDetailDataDiv',
            filterFieldMap: buildFilterFieldMap(fields, tableId),
            filterOptionsUrl: kind === 'header' ? urls.headerFilterOptions : urls.detailFilterOptions,
            queryUrl: kind === 'header' ? urls.queryHeader : urls.queryDetail,
            pageLength: 50,
            preserveSort: false,
            initialSort: resolveInitialSort(kind, pageConfig),
            dataTableOptions: {
                columnDefs: buildColumnDefs(kind)
            },
            onDraw: function ($div) {
                syncStickyHeaderOffset($div);
            }
        };

        if (kind === 'detail') {
            base.autoLoad = false;
            base.filterOptionsExtraParams = function () {
                return { headerKey: app.state.selectedHeaderKey || '' };
            };
            base.extraQueryParams = function () {
                return { headerKey: app.state.selectedHeaderKey || '' };
            };
            base.onAfterRender = function ($div) {
                bindDetailTableEvents($div);
            };
        } else {
            base.onAfterRender = function ($div) {
                bindHeaderTableEvents($div);
                app.updateHeaderActionState();
            };
        }

        return ProDataTables.buildConfig($.extend(true, {}, base, overrides || {}));
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

    function resolveInitialSort(kind, pageConfig) {
        var fields = getPageConfigFields(kind)
            .filter(function (field) {
                return field.visible !== false && field.Visible !== false;
            })
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
        var leadingOffset = kind === 'header' ? 2 : 1;
        var fallback = kind === 'header' ? [[3, 'desc']] : [[1, 'asc']];

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

    app.initProTables = function () {
        if (!global.ProDataTables || !ProDataTables.initUsers) {
            return;
        }

        headerTableInstance = ProDataTables.initUsers(buildShipInfoTableConfig('header'));
        detailTableInstance = ProDataTables.initUsers(buildShipInfoTableConfig('detail'));

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
