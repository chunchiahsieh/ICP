(function (global, $) {
    'use strict';

    function isFieldVisible(field) {
        return field.visible !== false && field.Visible !== false;
    }

    function isFieldSearchable(field) {
        return field.searchable !== false && field.Searchable !== false;
    }

    function buildTariffFilterFieldMap(fields) {
        var map = {};
        var filtersApi = global.ProTableFilters;
        (fields || []).forEach(function (field) {
            var fieldName = field.fieldName || field.FieldName;
            if (!fieldName || !isFieldVisible(field) || !isFieldSearchable(field)) {
                return;
            }

            map['filter-' + fieldName] = {
                fieldName: fieldName,
                filterType: filtersApi
                    ? filtersApi.resolveFieldFilterType(field)
                    : (field.filterType || field.FilterType || 'Checkbox')
            };
        });
        return map;
    }

    function buildTariffFilterHooks(filterFieldMap) {
        var filtersApi = global.ProTableFilters;
        if (!filtersApi) {
            return {};
        }

        return {
            filterFieldMap: filterFieldMap,
            customGetFilterValues: function ($scope) {
                return filtersApi.getProTableFilterValues($scope, filterFieldMap);
            },
            customBuildQueryPayload: function (saved) {
                return filtersApi.buildProTableQueryPayload(saved, filterFieldMap);
            },
            customRestoreFilterValues: function (saved, $scope) {
                filtersApi.restoreProTableFilterValues(saved, $scope, filterFieldMap);
            }
        };
    }

    function bindTariffFilterActions(tableInstance) {
        if (!global.ProTableFilters) {
            return;
        }

        global.ProTableFilters.bindProTableFilterActions({
            pageSelector: '.tariff-data-page',
            dataDivSelector: '#DataDiv',
            resolveReload: function () {
                if (tableInstance && tableInstance.reload) {
                    return function () { tableInstance.reload(); };
                }

                return null;
            }
        });
    }

    global.TariffProTable = {
        buildTariffFilterFieldMap: buildTariffFilterFieldMap,
        buildTariffFilterHooks: buildTariffFilterHooks,
        bindTariffFilterActions: bindTariffFilterActions
    };
})(window, window.jQuery);
