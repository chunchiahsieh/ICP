(function (global, $) {
    'use strict';

    function isFieldVisible(field) {
        return field.visible !== false && field.Visible !== false;
    }

    function isFieldSearchable(field) {
        return field.searchable !== false && field.Searchable !== false;
    }

    function buildFilterFieldMap(fields) {
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

    function buildFilterHooks(filterFieldMap) {
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

    function bindFilterActions(tableInstance) {
        if (!global.ProTableFilters) {
            return;
        }

        global.ProTableFilters.bindProTableFilterActions({
            pageSelector: '.customs-download-page',
            dataDivSelector: '#DataDiv',
            resolveReload: function () {
                if (tableInstance && tableInstance.reload) {
                    return function () { tableInstance.reload(); };
                }

                return null;
            }
        });
    }

    function downloadExcel(options) {
        var filtersApi = global.ProTableFilters;
        var payload = {};
        if (filtersApi && filtersApi.getProTableFilterValues && filtersApi.buildProTableQueryPayload) {
            var saved = filtersApi.getProTableFilterValues($('#DataDiv'), options.filterFieldMap || {});
            payload = filtersApi.buildProTableQueryPayload(saved, options.filterFieldMap || {}) || {};
        }

        var form = document.createElement('form');
        form.method = 'POST';
        form.action = options.downloadUrl;
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

    global.CustomsDataDownloadProTable = {
        buildFilterFieldMap: buildFilterFieldMap,
        buildFilterHooks: buildFilterHooks,
        bindFilterActions: bindFilterActions,
        downloadExcel: downloadExcel
    };
})(window, window.jQuery);
