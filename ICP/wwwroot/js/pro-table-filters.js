(function (global, $) {
    'use strict';

    var FILTER_TYPES = {
        Checkbox: 'Checkbox',
        Text: 'Text',
        DateRange: 'DateRange',
        Date: 'Date'
    };

    function normalizeFilterType(value) {
        var type = String(value || FILTER_TYPES.Checkbox);
        if (FILTER_TYPES[type]) {
            return type;
        }

        return FILTER_TYPES.Checkbox;
    }

    function resolveFieldFilterType(field) {
        return normalizeFilterType(field.filterType || field.FilterType);
    }

    function resolveFilterTypeFromMeta(meta) {
        return typeof meta === 'string'
            ? FILTER_TYPES.Checkbox
            : normalizeFilterType(meta.filterType || meta.FilterType);
    }

    function closeProTableFilterDropdown($filter) {
        var toggle = $filter.find('[data-bs-toggle="dropdown"]')[0];
        if (toggle && global.bootstrap && global.bootstrap.Dropdown) {
            global.bootstrap.Dropdown.getOrCreateInstance(toggle).hide();
        }
    }

    function updateProTableFilterCount($filter, filterType) {
        if (filterType === FILTER_TYPES.Checkbox) {
            var checkboxCount = $filter.find('.column-filter-cb:checked').length;
            $filter.find('.filter-count').text('(' + checkboxCount + ')');
            return;
        }

        var active = 0;
        if (filterType === FILTER_TYPES.Text) {
            active = ($filter.find('.pro-table-filter-text-input').val() || '').trim() ? 1 : 0;
        } else if (filterType === FILTER_TYPES.DateRange) {
            var from = ($filter.find('.pro-table-filter-date-from').val() || '').trim();
            var to = ($filter.find('.pro-table-filter-date-to').val() || '').trim();
            active = (from || to) ? 1 : 0;
        } else if (filterType === FILTER_TYPES.Date) {
            active = ($filter.find('.pro-table-filter-date-input').val() || '').trim() ? 1 : 0;
        }

        $filter.find('.filter-count').text('(' + active + ')');
    }

    function updateAllProTableFilterCounts($scope, filterFieldMap) {
        $.each(filterFieldMap || {}, function (filterId, meta) {
            var filterType = resolveFilterTypeFromMeta(meta);
            if (filterType === FILTER_TYPES.Checkbox) {
                return;
            }

            var $filter = $scope.find('#' + filterId);
            if ($filter.length) {
                updateProTableFilterCount($filter, filterType);
            }
        });
    }

    function getProTableFilterValues($scope, filterFieldMap) {
        var values = {};
        $.each(filterFieldMap || {}, function (filterId, meta) {
            var filterType = resolveFilterTypeFromMeta(meta);
            var $filter = $scope.find('#' + filterId);
            if ($filter.length === 0) {
                return;
            }

            if (filterType === FILTER_TYPES.Checkbox) {
                values[filterId] = $filter.find('.column-filter-cb:checked').map(function () {
                    return $(this).val();
                }).get();
                return;
            }

            if (filterType === FILTER_TYPES.Text) {
                values[filterId] = {
                    text: ($filter.find('.pro-table-filter-text-input').val() || '').trim()
                };
                return;
            }

            if (filterType === FILTER_TYPES.DateRange) {
                values[filterId] = {
                    from: ($filter.find('.pro-table-filter-date-from').val() || '').trim(),
                    to: ($filter.find('.pro-table-filter-date-to').val() || '').trim()
                };
                return;
            }

            if (filterType === FILTER_TYPES.Date) {
                values[filterId] = {
                    date: ($filter.find('.pro-table-filter-date-input').val() || '').trim()
                };
            }
        });

        return values;
    }

    function buildProTableQueryPayload(saved, filterFieldMap) {
        var payload = {};
        $.each(filterFieldMap || {}, function (filterId, meta) {
            var fieldName = meta.fieldName || meta.FieldName || meta;
            var filterType = resolveFilterTypeFromMeta(meta);
            var entry = saved ? saved[filterId] : null;
            if (!entry) {
                return;
            }

            if (filterType === FILTER_TYPES.Checkbox) {
                if (!entry.length) {
                    return;
                }

                $.each(entry, function (index, value) {
                    payload['Checkbox[' + fieldName + '][' + index + ']'] = value;
                });
                return;
            }

            if (filterType === FILTER_TYPES.Text) {
                if (!entry.text) {
                    return;
                }

                payload['Text[' + fieldName + ']'] = entry.text;
                return;
            }

            if (filterType === FILTER_TYPES.DateRange) {
                if (entry.from) {
                    payload['DateFrom[' + fieldName + ']'] = entry.from;
                }
                if (entry.to) {
                    payload['DateTo[' + fieldName + ']'] = entry.to;
                }
                return;
            }

            if (filterType === FILTER_TYPES.Date && entry.date) {
                payload['Date[' + fieldName + ']'] = entry.date;
            }
        });

        return payload;
    }

    function restoreProTableFilterValues(saved, $scope, filterFieldMap) {
        if (!saved) {
            return;
        }

        $.each(filterFieldMap || {}, function (filterId, meta) {
            var filterType = resolveFilterTypeFromMeta(meta);
            var $filter = $scope.find('#' + filterId);
            if ($filter.length === 0 || !saved[filterId]) {
                return;
            }

            var entry = saved[filterId];
            if (filterType === FILTER_TYPES.Checkbox) {
                $filter.find('.column-filter-cb').each(function () {
                    $(this).prop('checked', entry.indexOf($(this).val()) >= 0);
                });
                updateProTableFilterCount($filter, filterType);
                return;
            }

            if (filterType === FILTER_TYPES.Text) {
                $filter.find('.pro-table-filter-text-input').val(entry.text || '');
                updateProTableFilterCount($filter, filterType);
                return;
            }

            if (filterType === FILTER_TYPES.DateRange) {
                $filter.find('.pro-table-filter-date-from').val(entry.from || '');
                $filter.find('.pro-table-filter-date-to').val(entry.to || '');
                updateProTableFilterCount($filter, filterType);
                return;
            }

            if (filterType === FILTER_TYPES.Date) {
                $filter.find('.pro-table-filter-date-input').val(entry.date || '');
                updateProTableFilterCount($filter, filterType);
            }
        });
    }

    function bindProTableFilterActions(config) {
        config = config || {};
        var pageSelector = config.pageSelector || '.pro-datatables-page, .shipinfo-page';
        var resolveReload = config.resolveReload || function () { return null; };

        $(document)
            .off('click.proTableFilterConfirm', pageSelector + ' .pro-table-filter-confirm')
            .on('click.proTableFilterConfirm', pageSelector + ' .pro-table-filter-confirm', function (e) {
                e.preventDefault();
                e.stopPropagation();
                var $filter = $(this).closest('.pro-table-filter');
                var filterType = normalizeFilterType($filter.data('filter-type'));
                updateProTableFilterCount($filter, filterType);
                closeProTableFilterDropdown($filter);
                var reload = resolveReload($(this).closest(config.dataDivSelector || '[id$="DataDiv"]'));
                if (typeof reload === 'function') {
                    reload();
                }
            });

        $(document)
            .off('click.proTableFilterReset', pageSelector + ' .pro-table-filter-reset')
            .on('click.proTableFilterReset', pageSelector + ' .pro-table-filter-reset', function (e) {
                e.preventDefault();
                e.stopPropagation();
                var $filter = $(this).closest('.pro-table-filter');
                $filter.find('input').val('');
                var filterType = normalizeFilterType($filter.data('filter-type'));
                updateProTableFilterCount($filter, filterType);
                closeProTableFilterDropdown($filter);
                var reload = resolveReload($filter.closest(config.dataDivSelector || '[id$="DataDiv"]'));
                if (typeof reload === 'function') {
                    reload();
                }
            });
    }

    var api = {
        FILTER_TYPES: FILTER_TYPES,
        resolveFieldFilterType: resolveFieldFilterType,
        getProTableFilterValues: getProTableFilterValues,
        buildProTableQueryPayload: buildProTableQueryPayload,
        restoreProTableFilterValues: restoreProTableFilterValues,
        bindProTableFilterActions: bindProTableFilterActions,
        updateAllProTableFilterCounts: updateAllProTableFilterCounts
    };

    global.ProTableFilters = api;

    global.ShipInfoTableFilters = {
        FILTER_TYPES: api.FILTER_TYPES,
        resolveFieldFilterType: api.resolveFieldFilterType,
        getShipInfoFilterValues: api.getProTableFilterValues,
        buildShipInfoQueryPayload: api.buildProTableQueryPayload,
        restoreShipInfoFilterValues: api.restoreProTableFilterValues,
        bindShipInfoFilterActions: function (app) {
            api.bindProTableFilterActions({
                pageSelector: '.shipinfo-page',
                resolveReload: function ($container) {
                    if ($container.is('#shipInfoHeaderDataDiv') && app.headerTableInstance) {
                        return function () { app.headerTableInstance.reload(); };
                    }

                    if ($container.is('#shipInfoDetailDataDiv') && app.detailTableInstance) {
                        return function () { app.detailTableInstance.reload(); };
                    }

                    return null;
                }
            });
        },
        updateAllShipInfoFilterCounts: api.updateAllProTableFilterCounts
    };
})(window, window.jQuery);
