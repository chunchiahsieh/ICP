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

    function getShipInfoFilterValues($scope, filterFieldMap) {
        var values = {};
        $.each(filterFieldMap || {}, function (filterId, meta) {
            var fieldName = meta.fieldName || meta.FieldName || meta;
            var filterType = typeof meta === 'string'
                ? FILTER_TYPES.Checkbox
                : normalizeFilterType(meta.filterType || meta.FilterType);
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
                    text: ($filter.find('.shipinfo-filter-text-input').val() || '').trim()
                };
                return;
            }

            if (filterType === FILTER_TYPES.DateRange) {
                values[filterId] = {
                    from: ($filter.find('.shipinfo-filter-date-from').val() || '').trim(),
                    to: ($filter.find('.shipinfo-filter-date-to').val() || '').trim()
                };
                return;
            }

            if (filterType === FILTER_TYPES.Date) {
                values[filterId] = {
                    date: ($filter.find('.shipinfo-filter-date-input').val() || '').trim()
                };
            }
        });

        return values;
    }

    function buildShipInfoQueryPayload(saved, filterFieldMap) {
        var payload = {};
        $.each(filterFieldMap || {}, function (filterId, meta) {
            var fieldName = meta.fieldName || meta.FieldName || meta;
            var filterType = typeof meta === 'string'
                ? FILTER_TYPES.Checkbox
                : normalizeFilterType(meta.filterType || meta.FilterType);
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

    function restoreShipInfoFilterValues(saved, $scope, filterFieldMap) {
        if (!saved) {
            return;
        }

        $.each(filterFieldMap || {}, function (filterId, meta) {
            var filterType = typeof meta === 'string'
                ? FILTER_TYPES.Checkbox
                : normalizeFilterType(meta.filterType || meta.FilterType);
            var $filter = $scope.find('#' + filterId);
            if ($filter.length === 0 || !saved[filterId]) {
                return;
            }

            var entry = saved[filterId];
            if (filterType === FILTER_TYPES.Checkbox) {
                $filter.find('.column-filter-cb').each(function () {
                    $(this).prop('checked', entry.indexOf($(this).val()) >= 0);
                });
                var count = $filter.find('.column-filter-cb:checked').length;
                $filter.find('.filter-count').text('(' + count + ')');
                return;
            }

            if (filterType === FILTER_TYPES.Text) {
                $filter.find('.shipinfo-filter-text-input').val(entry.text || '');
                return;
            }

            if (filterType === FILTER_TYPES.DateRange) {
                $filter.find('.shipinfo-filter-date-from').val(entry.from || '');
                $filter.find('.shipinfo-filter-date-to').val(entry.to || '');
                return;
            }

            if (filterType === FILTER_TYPES.Date) {
                $filter.find('.shipinfo-filter-date-input').val(entry.date || '');
            }
        });
    }

    function bindShipInfoFilterActions(app) {
        $(document)
            .off('click.shipinfoFilterConfirm', '.shipinfo-page .shipinfo-filter-confirm')
            .on('click.shipinfoFilterConfirm', '.shipinfo-page .shipinfo-filter-confirm', function (e) {
                e.preventDefault();
                e.stopPropagation();
                var $container = $(this).closest('#shipInfoHeaderDataDiv, #shipInfoDetailDataDiv');
                if ($container.is('#shipInfoHeaderDataDiv') && app.headerTableInstance) {
                    app.headerTableInstance.reload();
                } else if ($container.is('#shipInfoDetailDataDiv') && app.detailTableInstance) {
                    app.detailTableInstance.reload();
                }
            });

        $(document)
            .off('click.shipinfoFilterReset', '.shipinfo-page .shipinfo-filter-reset')
            .on('click.shipinfoFilterReset', '.shipinfo-page .shipinfo-filter-reset', function (e) {
                e.preventDefault();
                e.stopPropagation();
                var $filter = $(this).closest('.shipinfo-filter');
                $filter.find('input').val('');
                var $container = $filter.closest('#shipInfoHeaderDataDiv, #shipInfoDetailDataDiv');
                if ($container.is('#shipInfoHeaderDataDiv') && app.headerTableInstance) {
                    app.headerTableInstance.reload();
                } else if ($container.is('#shipInfoDetailDataDiv') && app.detailTableInstance) {
                    app.detailTableInstance.reload();
                }
            });
    }

    global.ShipInfoTableFilters = {
        FILTER_TYPES: FILTER_TYPES,
        resolveFieldFilterType: resolveFieldFilterType,
        getShipInfoFilterValues: getShipInfoFilterValues,
        buildShipInfoQueryPayload: buildShipInfoQueryPayload,
        restoreShipInfoFilterValues: restoreShipInfoFilterValues,
        bindShipInfoFilterActions: bindShipInfoFilterActions
    };
})(window, window.jQuery);
