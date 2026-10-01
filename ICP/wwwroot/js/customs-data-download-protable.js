(function (global, $) {
    'use strict';

    function isFieldVisible(field) {
        return field.visible !== false && field.Visible !== false;
    }

    function isFieldSearchable(field) {
        return field.searchable !== false && field.Searchable !== false;
    }

    function compareNatural(left, right) {
        left = left == null ? '' : String(left);
        right = right == null ? '' : String(right);
        var leftIndex = 0;
        var rightIndex = 0;
        function isDigit(value) { return value >= '0' && value <= '9'; }

        while (leftIndex < left.length && rightIndex < right.length) {
            if (isDigit(left[leftIndex]) && isDigit(right[rightIndex])) {
                var leftEnd = leftIndex;
                var rightEnd = rightIndex;
                while (leftEnd < left.length && isDigit(left[leftEnd])) leftEnd++;
                while (rightEnd < right.length && isDigit(right[rightEnd])) rightEnd++;
                while (leftIndex < leftEnd && left[leftIndex] === '0') leftIndex++;
                while (rightIndex < rightEnd && right[rightIndex] === '0') rightIndex++;
                var leftDigits = left.slice(leftIndex, leftEnd);
                var rightDigits = right.slice(rightIndex, rightEnd);
                if (leftDigits.length !== rightDigits.length)
                    return leftDigits.length < rightDigits.length ? -1 : 1;
                if (leftDigits !== rightDigits) return leftDigits < rightDigits ? -1 : 1;
                leftIndex = leftEnd;
                rightIndex = rightEnd;
            } else {
                var leftChar = left[leftIndex].toUpperCase();
                var rightChar = right[rightIndex].toUpperCase();
                if (leftChar.length !== 1) leftChar = left[leftIndex];
                if (rightChar.length !== 1) rightChar = right[rightIndex];
                if (leftChar !== rightChar) return leftChar < rightChar ? -1 : 1;
                leftIndex++;
                rightIndex++;
            }
        }
        return (left.length - leftIndex) - (right.length - rightIndex);
    }

    function buildSortColumnDefs(fields) {
        var dataTable = $ && $.fn && $.fn.dataTable;
        if (!dataTable || !dataTable.ext || !dataTable.ext.type) return [];

        var typeName = 'customs-download-natural-v1';
        dataTable.ext.type.order[typeName + '-asc'] = compareNatural;
        dataTable.ext.type.order[typeName + '-desc'] = function (left, right) {
            return -compareNatural(left, right);
        };
        var naturalFields = ['mawb', 'hawb', 'invoiceno'];
        var targets = [];
        (fields || []).filter(isFieldVisible).forEach(function (field, index) {
            var name = field.fieldName || field.FieldName || '';
            if (naturalFields.indexOf(name.toLowerCase()) >= 0) targets.push(index);
        });
        return targets.length ? [{ targets: targets, type: typeName }] : [];
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
        buildSortColumnDefs: buildSortColumnDefs,
        buildFilterFieldMap: buildFilterFieldMap,
        buildFilterHooks: buildFilterHooks,
        bindFilterActions: bindFilterActions,
        downloadExcel: downloadExcel
    };
})(window, window.jQuery);
