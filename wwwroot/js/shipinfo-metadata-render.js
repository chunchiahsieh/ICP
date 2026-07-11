(function (global, $) {
    'use strict';

    if (!$) {
        return;
    }

    var lookupCache = {};

    function getField(field, key) {
        if (!field) {
            return undefined;
        }

        var camel = key.charAt(0).toLowerCase() + key.slice(1);
        return field[key] !== undefined ? field[key] : field[camel];
    }

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : value).html();
    }

    function resolveLabel(field, culture) {
        var resolved = getField(field, 'Label') || getField(field, 'label');
        if (resolved) {
            return resolved;
        }

        var normalizedCulture = (culture || 'zh-TW').toLowerCase();
        if (normalizedCulture.indexOf('zh') === 0) {
            return getField(field, 'DisplayNameZh') || getField(field, 'displayNameZh') || getField(field, 'DisplayName') || getField(field, 'displayName') || getField(field, 'FieldName') || getField(field, 'fieldName');
        }

        return getField(field, 'DisplayName') || getField(field, 'displayName') || getField(field, 'DisplayNameZh') || getField(field, 'displayNameZh') || getField(field, 'FieldName') || getField(field, 'fieldName');
    }

    function resolveSearchControlType(field) {
        return getField(field, 'SearchControlType') || getField(field, 'searchControlType') || getField(field, 'ControlType') || getField(field, 'controlType') || 'Text';
    }

    function resolveControlType(field) {
        return getField(field, 'ControlType') || getField(field, 'controlType') || 'Text';
    }

    function canUseField(field, hasPermission) {
        var permissionCode = getField(field, 'PermissionCode') || getField(field, 'permissionCode');
        if (!permissionCode) {
            return true;
        }

        return hasPermission(permissionCode);
    }

    function getAllFields(fields) {
        return (fields || [])
            .slice()
            .sort(function (a, b) {
                var orderA = Number(getField(a, 'DisplayOrder') || getField(a, 'displayOrder') || 0);
                var orderB = Number(getField(b, 'DisplayOrder') || getField(b, 'displayOrder') || 0);
                if (orderA !== orderB) {
                    return orderA - orderB;
                }

                var nameA = (getField(a, 'FieldName') || getField(a, 'fieldName') || '').toLowerCase();
                var nameB = (getField(b, 'FieldName') || getField(b, 'fieldName') || '').toLowerCase();
                return nameA.localeCompare(nameB);
            });
    }

    function pad2(n) {
        return n < 10 ? '0' + n : String(n);
    }

    /**
     * Normalize date strings to yyyy-MM-dd (accepts yyyy-MM-dd, yyyy/MM/dd, yyyy/M/d, parseable strings).
     */
    function normalizeDateInputValue(value) {
        if (value == null) {
            return '';
        }

        var text = String(value).trim();
        if (!text) {
            return '';
        }

        var dash = /^(\d{4})-(\d{1,2})-(\d{1,2})/.exec(text);
        if (dash) {
            return dash[1] + '-' + pad2(Number(dash[2])) + '-' + pad2(Number(dash[3]));
        }

        var slash = /^(\d{4})\/(\d{1,2})\/(\d{1,2})/.exec(text);
        if (slash) {
            return slash[1] + '-' + pad2(Number(slash[2])) + '-' + pad2(Number(slash[3]));
        }

        var parsed = Date.parse(text);
        if (!isNaN(parsed)) {
            var d = new Date(parsed);
            return d.getFullYear() + '-' + pad2(d.getMonth() + 1) + '-' + pad2(d.getDate());
        }

        return text;
    }

    function isValidYyyyMmDd(value) {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(value || '')) {
            return false;
        }

        var parts = value.split('-');
        var y = Number(parts[0]);
        var m = Number(parts[1]);
        var d = Number(parts[2]);
        var dt = new Date(y, m - 1, d);
        return dt.getFullYear() === y && dt.getMonth() === m - 1 && dt.getDate() === d;
    }

    function normalizeDateTimeInputValue(value) {
        if (value == null) {
            return '';
        }

        var text = String(value).trim();
        if (!text) {
            return '';
        }

        var firstToken = text.split(/[\sT]/)[0];
        var datePart = normalizeDateInputValue(firstToken);
        if (!datePart || !isValidYyyyMmDd(datePart)) {
            return normalizeDateInputValue(text);
        }

        var timeMatch = text.match(/[T\s](\d{1,2}):(\d{2})(?::(\d{2}))?/);
        if (!timeMatch) {
            return datePart;
        }

        return datePart + ' ' + pad2(Number(timeMatch[1])) + ':' + pad2(Number(timeMatch[2]));
    }

    function enrichDateRangeValues(values) {
        var enriched = $.extend({}, values || {});
        ['SaDate', 'Eta'].forEach(function (fieldName) {
            if (!enriched[fieldName + 'From'] && enriched[fieldName]) {
                enriched[fieldName + 'From'] = enriched[fieldName];
            }
        });

        Object.keys(enriched).forEach(function (key) {
            var lower = key.toLowerCase();
            if (lower === 'sadate' || lower === 'eta' || lower.endsWith('from') || lower.endsWith('to')
                || lower.endsWith('date')) {
                if (typeof enriched[key] === 'string' && enriched[key]) {
                    // Only normalize obvious date-like fields when key suggests date
                    if (/date|eta|etd|sadate/i.test(key)) {
                        enriched[key] = normalizeDateInputValue(enriched[key]);
                    }
                }
            }
        });

        return enriched;
    }
    function getVisibleFields(fields) {
        return (fields || [])
            .filter(function (field) {
                return getField(field, 'Visible') !== false && getField(field, 'visible') !== false;
            })
            .sort(function (a, b) {
                var orderA = Number(getField(a, 'DisplayOrder') || getField(a, 'displayOrder') || 0);
                var orderB = Number(getField(b, 'DisplayOrder') || getField(b, 'displayOrder') || 0);
                if (orderA !== orderB) {
                    return orderA - orderB;
                }

                var nameA = (getField(a, 'FieldName') || getField(a, 'fieldName') || '').toLowerCase();
                var nameB = (getField(b, 'FieldName') || getField(b, 'fieldName') || '').toLowerCase();
                return nameA.localeCompare(nameB);
            });
    }

    function buildLabelHtml(field, culture, requiredMark) {
        var label = resolveLabel(field, culture);
        var required = getField(field, 'Required') || getField(field, 'required');
        var tooltip = getField(field, 'Tooltip') || getField(field, 'tooltip');
        var requiredHtml = required ? ' <span class="text-danger">' + escapeHtml(requiredMark || '*') + '</span>' : '';
        var tooltipHtml = tooltip ? ' title="' + escapeHtml(tooltip) + '" data-bs-toggle="tooltip"' : '';
        return '<label class="form-label"' + tooltipHtml + ' for="' + escapeHtml(getField(field, 'FieldName') || getField(field, 'fieldName')) + '">' + escapeHtml(label) + requiredHtml + '</label>';
    }

    function loadLookupOptions(category, lookupUrl) {
        if (!category) {
            return $.Deferred().resolve([]).promise();
        }

        if (lookupCache[category]) {
            return $.Deferred().resolve(lookupCache[category]).promise();
        }

        return $.getJSON(lookupUrl, { category: category }).then(function (response) {
            if (!response || !response.success) {
                return [];
            }

            lookupCache[category] = response.data || [];
            return lookupCache[category];
        });
    }

    function renderSelect($select, options, selectedValue) {
        $select.empty();
        $select.append('<option value=""></option>');
        (options || []).forEach(function (option) {
            var value = option.value || option.Value || '';
            var text = option.text || option.Text || value;
            var selected = String(selectedValue || '') === String(value) ? ' selected' : '';
            $select.append('<option value="' + escapeHtml(value) + '"' + selected + '>' + escapeHtml(text) + '</option>');
        });
    }

    function createInputControl(field, options) {
        var fieldName = getField(field, 'FieldName') || getField(field, 'fieldName');
        var controlType = options.mode === 'search' ? resolveSearchControlType(field) : resolveControlType(field);
        var placeholder = getField(field, 'Placeholder') || getField(field, 'placeholder') || '';
        var maxLength = getField(field, 'MaxLength') || getField(field, 'maxLength');
        var editable = getField(field, 'Editable') !== false && getField(field, 'editable') !== false;
        var readOnly = options.mode === 'view'
            || getField(field, 'ReadOnly') || getField(field, 'readOnly')
            || (options.mode === 'edit' && !editable);
        var lockControl = readOnly && (options.mode === 'view' || options.mode === 'edit');
        var value = options.values ? (options.values[fieldName] ?? options.values[fieldName.toLowerCase()] ?? '') : (getField(field, 'DefaultValue') || getField(field, 'defaultValue') || '');
        var commonAttrs = ' data-field="' + escapeHtml(fieldName) + '" data-control-type="' + escapeHtml(controlType) + '"';
        if (lockControl) {
            commonAttrs += ' readonly disabled';
        }

        if (controlType === 'DateRange') {
            var fromValue = '';
            var toValue = '';
            if (options.values && options.values[fieldName + 'From']) {
                fromValue = normalizeDateInputValue(options.values[fieldName + 'From']);
            }
            if (options.values && options.values[fieldName + 'To']) {
                toValue = normalizeDateInputValue(options.values[fieldName + 'To']);
            }

            var $dateRange = $('<div class="row g-2 shipinfo-date-range"></div>')
                .attr('data-field', fieldName)
                .append(
                    $('<div class="col"></div>').append(
                        $('<input type="text" class="form-control shipinfo-control" inputmode="numeric" placeholder="yyyy-MM-dd" pattern="\\d{4}-\\d{2}-\\d{2}" />')
                            .attr('data-range-part', 'from')
                            .attr('data-field', fieldName + 'From')
                            .attr('data-control-type', 'Date')
                            .val(fromValue)
                    ),
                    $('<div class="col-auto align-self-center">~</div>'),
                    $('<div class="col"></div>').append(
                        $('<input type="text" class="form-control shipinfo-control" inputmode="numeric" placeholder="yyyy-MM-dd" pattern="\\d{4}-\\d{2}-\\d{2}" />')
                            .attr('data-range-part', 'to')
                            .attr('data-field', fieldName + 'To')
                            .attr('data-control-type', 'Date')
                            .val(toValue)
                    )
                );

            if (lockControl) {
                $dateRange.find('.shipinfo-control').prop('readonly', true).prop('disabled', true);
            }

            return $dateRange;
        }

        if (controlType === 'Select') {
            var $select = $('<select class="form-select shipinfo-control"></select>').attr('data-field', fieldName).attr('data-control-type', controlType);
            if (lockControl) {
                $select.prop('disabled', true);
            }

            var category = getField(field, 'LookupCategory') || getField(field, 'lookupCategory');
            if (category && options.lookupUrl) {
                $select.append('<option value="">' + escapeHtml(options.loadingText || '...') + '</option>');
                loadLookupOptions(category, options.lookupUrl).done(function (lookupItems) {
                    renderSelect($select, lookupItems, value);
                });
            } else {
                renderSelect($select, [], value);
            }

            return $select;
        }

        if (controlType === 'Textarea') {
            return $('<textarea class="form-control shipinfo-control" rows="3"></textarea>')
                .attr('data-field', fieldName)
                .attr('data-control-type', controlType)
                .attr('placeholder', placeholder)
                .prop('readonly', lockControl)
                .prop('disabled', lockControl)
                .val(value == null ? '' : value);
        }

        if (controlType === 'Checkbox') {
            var checked = value === true || value === 'true' || value === '1' || value === 'on';
            return $('<div class="form-check"></div>').append(
                $('<input type="checkbox" class="form-check-input shipinfo-control" />')
                    .attr('data-field', fieldName)
                    .attr('data-control-type', controlType)
                    .prop('checked', checked)
                    .prop('disabled', lockControl)
            );
        }

        var inputType = 'text';
        if (controlType === 'Number' || controlType === 'Decimal' || controlType === 'Currency') {
            inputType = 'number';
        }

        var displayValue = value == null ? '' : value;
        if (controlType === 'Date') {
            displayValue = normalizeDateInputValue(displayValue);
        } else if (controlType === 'DateTime') {
            displayValue = normalizeDateTimeInputValue(displayValue);
        }

        var $input = $('<input class="form-control shipinfo-control" />')
            .attr('type', inputType)
            .attr('data-field', fieldName)
            .attr('data-control-type', controlType)
            .attr('placeholder', placeholder || (controlType === 'Date' ? 'yyyy-MM-dd' : (controlType === 'DateTime' ? 'yyyy-MM-dd HH:mm' : '')))
            .val(displayValue);

        if (controlType === 'Date') {
            $input.attr('inputmode', 'numeric').attr('pattern', '\\d{4}-\\d{2}-\\d{2}');
        }

        if (maxLength) {
            $input.attr('maxlength', maxLength);
        }

        if (lockControl) {
            $input.prop('readonly', true).prop('disabled', true);
        }

        if (options.mode === 'search') {
            $input.addClass('shipinfo-search-input');
        }

        return $input;
    }

    function renderSearchFields($container, fields, options) {
        $container.empty();
        getVisibleFields(fields).forEach(function (field) {
            if (!canUseField(field, options.hasPermission)) {
                return;
            }

            if (!(getField(field, 'Searchable') || getField(field, 'searchable'))) {
                return;
            }

            var fieldName = getField(field, 'FieldName') || getField(field, 'fieldName');
            var $group = $('<div class="col-12 col-md-6 col-lg-4"></div>');
            $group.append(buildLabelHtml(field, options.culture, options.requiredMark));
            $group.append(createInputControl(field, {
                mode: 'search',
                culture: options.culture,
                lookupUrl: options.lookupUrl,
                loadingText: options.loadingText,
                values: options.values
            }));
            $container.append($group);
        });

        $container.find('[data-bs-toggle="tooltip"]').each(function () {
            if (global.bootstrap && global.bootstrap.Tooltip) {
                new global.bootstrap.Tooltip(this);
            }
        });
    }

    function groupFields(fields, includeHidden) {
        var groups = {};
        var source = includeHidden ? getAllFields(fields) : getVisibleFields(fields);
        source.forEach(function (field) {
            var groupName = getField(field, 'Group') || getField(field, 'group') || 'Other';
            if (!groups[groupName]) {
                groups[groupName] = [];
            }

            groups[groupName].push(field);
        });

        return groups;
    }

    function renderFormFields($container, fields, options) {
        $container.empty();
        var includeHidden = options.includeHidden === true;
        var groups = groupFields(fields, includeHidden);
        var renderValues = enrichDateRangeValues(options.values);
        Object.keys(groups).forEach(function (groupName) {
            var $section = $('<div class="mb-3"></div>');
            if (Object.keys(groups).length > 1) {
                $section.append('<h6 class="text-muted border-bottom pb-2 mb-3">' + escapeHtml(groupName) + '</h6>');
            }

            var $row = $('<div class="row g-3"></div>');
            groups[groupName].forEach(function (field) {
                if (!canUseField(field, options.hasPermission)) {
                    return;
                }

                var $group = $('<div class="col-12 col-md-6"></div>');
                $group.append(buildLabelHtml(field, options.culture, options.requiredMark));
                $group.append(createInputControl(field, {
                    mode: options.mode || 'edit',
                    culture: options.culture,
                    lookupUrl: options.lookupUrl,
                    loadingText: options.loadingText,
                    values: renderValues
                }));
                $row.append($group);
            });

            $section.append($row);
            $container.append($section);
        });

        $container.find('[data-bs-toggle="tooltip"]').each(function () {
            if (global.bootstrap && global.bootstrap.Tooltip) {
                new global.bootstrap.Tooltip(this);
            }
        });

        appendConcurrencyFields($container, options.values || {});

        var $first = $container.find('.shipinfo-control:not([disabled])').first();
        if ($first.length) {
            $first.trigger('focus');
        }
    }

    function appendConcurrencyFields($container, values) {
        var id = values.Id || values.id;
        var rowVersion = values.RowVersion || values.rowVersion;
        var updateTime = values.UpdateTime || values.updateTime;
        if (id) {
            $container.append('<input type="hidden" class="shipinfo-control shipinfo-meta-field" data-field="Id" value="' + escapeHtml(id) + '" />');
        }

        if (rowVersion) {
            $container.append('<input type="hidden" class="shipinfo-meta-field" data-meta-field="rowVersion" value="' + escapeHtml(rowVersion) + '" />');
        }

        if (updateTime) {
            $container.append('<input type="hidden" class="shipinfo-meta-field" data-meta-field="updateTime" value="' + escapeHtml(updateTime) + '" />');
        }
    }

    function renderTableHead($headRow, fields, options) {
        $headRow.empty();
        (options.fixedColumns || []).forEach(function (column) {
            $headRow.append('<th scope="col" class="' + escapeHtml(column.className || '') + '">' + escapeHtml(column.label) + '</th>');
        });

        getVisibleFields(fields).forEach(function (field) {
            if (!canUseField(field, options.hasPermission)) {
                return;
            }

            $headRow.append('<th scope="col">' + escapeHtml(resolveLabel(field, options.culture)) + '</th>');
        });

        return getVisibleFields(fields).filter(function (field) {
            return canUseField(field, options.hasPermission);
        });
    }

    function appendTableCells($row, item, fields, options) {
        getVisibleFields(fields).forEach(function (field) {
            if (!canUseField(field, options.hasPermission)) {
                return;
            }

            var fieldName = getField(field, 'FieldName') || getField(field, 'fieldName');
            var value = item[fieldName];
            if (value === undefined) {
                value = item[fieldName.charAt(0).toLowerCase() + fieldName.slice(1)];
            }

            $row.append('<td>' + escapeHtml(value == null ? '' : value) + '</td>');
        });
    }

    function collectControlValues($container) {
        var values = {};
        $container.find('.shipinfo-control').each(function () {
            var $control = $(this);
            var fieldName = $control.data('field');
            if (!fieldName) {
                return;
            }

            if ($control.attr('type') === 'checkbox') {
                values[fieldName] = $control.is(':checked') ? 'true' : 'false';
                return;
            }

            var raw = ($control.val() || '').toString().trim();
            var controlType = ($control.attr('data-control-type') || '').toString();
            if (controlType === 'Date') {
                raw = normalizeDateInputValue(raw);
            } else if (controlType === 'DateTime') {
                raw = normalizeDateTimeInputValue(raw);
            }

            values[fieldName] = raw;
        });

        $container.find('.shipinfo-date-range').each(function () {
            var baseField = $(this).data('field');
            var fromValue = normalizeDateInputValue($(this).find('[data-range-part="from"]').val() || '');
            var toValue = normalizeDateInputValue($(this).find('[data-range-part="to"]').val() || '');
            values[baseField + 'From'] = fromValue;
            values[baseField + 'To'] = toValue;
        });

        return values;
    }

    function collectSaveMeta($container) {
        var meta = {
            id: null,
            rowVersion: null,
            updateTime: null
        };

        var idValue = $container.find('[data-field="Id"]').val();
        if (idValue) {
            meta.id = idValue;
        }

        var rowVersion = $container.find('[data-meta-field="rowVersion"]').val();
        if (rowVersion) {
            meta.rowVersion = rowVersion;
        }

        var updateTime = $container.find('[data-meta-field="updateTime"]').val();
        if (updateTime) {
            meta.updateTime = updateTime;
        }

        return meta;
    }

    function focusFirstInvalid($container) {
        var $firstInvalid = $container.find('.is-invalid').first();
        if (!$firstInvalid.length) {
            return;
        }

        var $modalBody = $firstInvalid.closest('.modal-body');
        if ($modalBody.length) {
            $modalBody.animate({ scrollTop: Math.max(0, $firstInvalid.position().top - 24) }, 200);
        } else {
            $('html, body').animate({ scrollTop: $firstInvalid.offset().top - 120 }, 200);
        }

        $firstInvalid.trigger('focus');
    }

    function validateClientFields($container, fields, culture, requiredMark) {
        var values = collectControlValues($container);
        var errors = [];

        getVisibleFields(fields).forEach(function (field) {
            if (!canUseField(field, function () { return true; })) {
                return;
            }

            if (getField(field, 'Editable') === false || getField(field, 'editable') === false) {
                return;
            }

            var fieldName = getField(field, 'FieldName') || getField(field, 'fieldName');
            var label = resolveLabel(field, culture);
            var controlType = resolveControlType(field);
            var required = getField(field, 'Required') || getField(field, 'required');

            if (controlType === 'DateRange') {
                var fromValue = normalizeDateInputValue(values[fieldName + 'From']);
                var toValue = normalizeDateInputValue(values[fieldName + 'To']);
                if (fromValue && !isValidYyyyMmDd(fromValue)) {
                    errors.push({ fieldName: fieldName + 'From', message: label + ' date format is invalid (yyyy-MM-dd)' });
                }
                if (toValue && !isValidYyyyMmDd(toValue)) {
                    errors.push({ fieldName: fieldName + 'To', message: label + ' date format is invalid (yyyy-MM-dd)' });
                }
                if (fromValue && toValue && isValidYyyyMmDd(fromValue) && isValidYyyyMmDd(toValue) && fromValue > toValue) {
                    errors.push({ fieldName: fieldName + 'From', message: label + ' end date must be on or after start date' });
                }
                return;
            }

            var value = values[fieldName];

            if (required && !value) {
                errors.push({ fieldName: fieldName, message: label + (requiredMark || ' is required') });
                return;
            }

            if (!value) {
                return;
            }

            var maxLength = getField(field, 'MaxLength') || getField(field, 'maxLength');
            var minLength = getField(field, 'MinLength') || getField(field, 'minLength');
            var regex = getField(field, 'Regex') || getField(field, 'regex');
            var minValue = getField(field, 'MinValue') || getField(field, 'minValue');
            var maxValue = getField(field, 'MaxValue') || getField(field, 'maxValue');

            if (minLength && value.length < Number(minLength)) {
                errors.push({ fieldName: fieldName, message: label + ' minimum length is ' + minLength });
            }

            if (maxLength && value.length > Number(maxLength)) {
                errors.push({ fieldName: fieldName, message: label + ' maximum length is ' + maxLength });
            }

            if (regex) {
                try {
                    if (!new RegExp(regex).test(value)) {
                        errors.push({ fieldName: fieldName, message: label + ' format is invalid' });
                    }
                } catch (e) {
                    // ignore invalid regex metadata
                }
            }

            if (controlType === 'Number' || controlType === 'Decimal' || controlType === 'Currency') {
                var numberValue = Number(value);
                if (isNaN(numberValue)) {
                    errors.push({ fieldName: fieldName, message: label + ' must be a number' });
                } else {
                    if (minValue !== undefined && minValue !== null && numberValue < Number(minValue)) {
                        errors.push({ fieldName: fieldName, message: label + ' minimum value is ' + minValue });
                    }

                    if (maxValue !== undefined && maxValue !== null && numberValue > Number(maxValue)) {
                        errors.push({ fieldName: fieldName, message: label + ' maximum value is ' + maxValue });
                    }
                }
            }

            if (controlType === 'Date') {
                if (!isValidYyyyMmDd(value)) {
                    errors.push({ fieldName: fieldName, message: label + ' date format is invalid (yyyy-MM-dd)' });
                }
            }

            if (controlType === 'DateTime') {
                var normalizedDt = normalizeDateTimeInputValue(value);
                var dateOnly = normalizedDt.substring(0, 10);
                if (!isValidYyyyMmDd(dateOnly)) {
                    errors.push({ fieldName: fieldName, message: label + ' date format is invalid (yyyy-MM-dd)' });
                }
            }
        });

        $container.find('.is-invalid').removeClass('is-invalid');
        errors.forEach(function (error) {
            $container.find('[data-field="' + error.fieldName + '"]').addClass('is-invalid');
        });

        if (errors.length > 0) {
            focusFirstInvalid($container);
        }

        return errors;
    }

    function validateClientRequired($container, fields, culture, requiredMark) {
        return validateClientFields($container, fields, culture, requiredMark);
    }

    function setFieldsEditable($container, editableFieldNames, options) {
        var editableMap = {};
        (editableFieldNames || []).forEach(function (name) {
            editableMap[String(name).toLowerCase()] = true;
        });

        getAllFields(options.fields || []).forEach(function (field) {
            var fieldName = getField(field, 'FieldName') || getField(field, 'fieldName');
            var isEditable = !!editableMap[String(fieldName).toLowerCase()];
            var controlType = resolveControlType(field);

            if (controlType === 'DateRange') {
                $container.find('.shipinfo-date-range[data-field="' + fieldName + '"] .shipinfo-control').each(function () {
                    $(this).prop('readonly', !isEditable).prop('disabled', !isEditable);
                });
                return;
            }

            $container.find('[data-field="' + fieldName + '"].shipinfo-control').each(function () {
                var $control = $(this);
                if ($control.is('select')) {
                    $control.prop('disabled', !isEditable);
                } else {
                    $control.prop('readonly', !isEditable).prop('disabled', !isEditable);
                }
            });
        });
    }

    global.ShipInfoRender = {
        getVisibleFields: getVisibleFields,
        getAllFields: getAllFields,
        enrichDateRangeValues: enrichDateRangeValues,
        normalizeDateInputValue: normalizeDateInputValue,
        setFieldsEditable: setFieldsEditable,
        resolveLabel: resolveLabel,
        canUseField: canUseField,
        renderSearchFields: renderSearchFields,
        renderFormFields: renderFormFields,
        renderTableHead: renderTableHead,
        appendTableCells: appendTableCells,
        collectControlValues: collectControlValues,
        collectSaveMeta: collectSaveMeta,
        validateClientFields: validateClientFields,
        validateClientRequired: validateClientRequired,
        clearLookupCache: function () {
            lookupCache = {};
        }
    };
})(window, window.jQuery);
