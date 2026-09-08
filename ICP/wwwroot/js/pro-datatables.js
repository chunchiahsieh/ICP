(function (global, $) {
  'use strict';

  global.ProDataTables = global.ProDataTables || {};

  global.ProDataTables.defaults = {
    tableSelector: '#datatable',
    dataDivSelector: '#DataDiv',
    filterDropdownSelector: '.column-filter-dropdown',
    pageLength: 10,
    searchDebounceMs: 300,
    initialSort: [[0, 'desc']]
  };

  global.ProDataTables.linkedDetailTableOptions = {
    paging: false,
    lengthChange: false,
    info: false,
    dom: "<'row'<'col-sm-12'tr>>"
  };

  global.ProDataTables.resolveLengthMenu = function (config) {
    if (config && config.lengthMenu) {
      return config.lengthMenu;
    }
    if (global.IcpDataTablesLengthMenu) {
      return global.IcpDataTablesLengthMenu;
    }
    return [[10, 25, 50, 100], [10, 25, 50, 100]];
  };

  global.ProDataTables.buildConfig = function (overrides) {
    return $.extend({}, global.ProDataTables.defaults, overrides || {});
  };

  function closeFilterDropdown($dropdown) {
    var toggle = $dropdown.find('[data-bs-toggle="dropdown"]')[0];
    if (toggle && global.bootstrap && global.bootstrap.Dropdown) {
      global.bootstrap.Dropdown.getOrCreateInstance(toggle).hide();
    }
  }

  function ProDataTablesInitUsers(config) {
    var tableSortState = config.initialSort || [[0, 'desc']];
    var tablePageLengthState = config.pageLength > 0 ? config.pageLength : global.ProDataTables.defaults.pageLength;
    var pageState = 1;
    var totalRecordCount = 0;
    var filterSearchDebounceTimers = {};
    var instanceNs = 'ProDT' + (config.dataDivSelector || '#DataDiv').replace(/[^a-zA-Z0-9]/g, '_');

    function getFiltersInScope() {
      return $(config.dataDivSelector).find(config.filterDropdownSelector);
    }

    function getTableInScope() {
      return $(config.dataDivSelector).find(config.tableSelector);
    }

    function findFilterDropdown(dropdownId) {
      var $dropdown = $(config.dataDivSelector).find('#' + dropdownId);
      if ($dropdown.length === 0) {
        $dropdown = $('#' + dropdownId);
      }
      return $dropdown;
    }

    function saveTableState() {
      var $table = getTableInScope();
      if ($table.length === 0) return;
      var tableEl = $table[0];
      if (!$.fn.dataTable.isDataTable(tableEl)) return;
      var dt = $table.DataTable();
      tableSortState = dt.order();
      // Paging is rendered by the server pager below. DataTables remains responsible
      // only for table presentation, not for retaining a full client-side result set.
    }

    function getFilterValuesForDropdown($dropdown) {
      return $dropdown.find('.column-filter-cb:checked').map(function () {
        return $(this).val();
      }).get();
    }

    function getFilterValues() {
      if (typeof config.customGetFilterValues === 'function') {
        return config.customGetFilterValues($(config.dataDivSelector), config.filterFieldMap);
      }

      var values = {};
      getFiltersInScope().each(function () {
        values[this.id] = getFilterValuesForDropdown($(this));
      });
      return values;
    }

    function buildQueryPayload(saved) {
      if (typeof config.customBuildQueryPayload === 'function') {
        return config.customBuildQueryPayload(saved, config.filterFieldMap);
      }

      var payload = {};
      $.each(config.filterFieldMap, function (dropdownId, paramName) {
        var selected = (saved && saved[dropdownId]) ? saved[dropdownId] : [];
        $.each(selected, function (index, value) {
          payload[paramName + '[' + index + ']'] = value;
        });
      });
      return payload;
    }

    function updateFilterCount($dropdown) {
      var count = $dropdown.find('.column-filter-cb:checked').length;
      $dropdown.find('.filter-count').text('(' + count + ')');
    }

    function renderCheckboxOptions($dropdown, options) {
      var $container = $dropdown.find('.users-filter-options');
      var column = $dropdown.data('column');
      var formatLabel = typeof config.formatFilterOptionLabel === 'function'
        ? config.formatFilterOptionLabel
        : null;
      $container.empty();
      $.each(options, function (index, option) {
        var value = option;
        var label = option;
        if (option && typeof option === 'object') {
          value = option.value != null ? option.value : option.Value;
          label = option.label != null ? option.label : (option.Label != null ? option.Label : value);
        }
        if (formatLabel) {
          label = formatLabel(column, value) || value;
        }
        var id = $dropdown.attr('id') + '-cb-' + index;
        var $item = $('<div class="form-check"></div>');
        $item.append(
          $('<input type="checkbox" class="form-check-input column-filter-cb">')
            .attr('id', id)
            .attr('value', value)
        );
        $item.append(
          $('<label class="form-check-label"></label>')
            .attr('for', id)
            .text(label)
        );
        $container.append($item);
      });
    }

    function restoreChecks($dropdown, values) {
      $dropdown.find('.column-filter-cb').each(function () {
        $(this).prop('checked', values.indexOf($(this).val()) >= 0);
      });
      updateFilterCount($dropdown);
    }

    function filterCheckboxList($dropdown, term) {
      term = (term || '').toLowerCase().trim();
      var visibleCount = 0;
      $dropdown.find('.users-filter-options .form-check').each(function () {
        var text = $(this).find('.form-check-label').text().toLowerCase();
        var match = !term || text.indexOf(term) >= 0;
        $(this).toggle(match);
        if (match) visibleCount++;
      });
      $dropdown.find('.users-filter-empty').toggleClass('d-none', visibleCount > 0);
    }

    function loadDropdownOptions($dropdown, search, done) {
      var column = $dropdown.data('column');
      var selected = getFilterValuesForDropdown($dropdown);

      var params = { column: column };
      if (search) params.search = search;

      if (config.filterOptionsExtraParams) {
        var filterExtra = typeof config.filterOptionsExtraParams === 'function'
          ? config.filterOptionsExtraParams()
          : config.filterOptionsExtraParams;
        if (filterExtra) {
          $.extend(params, filterExtra);
        }
      }

      $.get(config.filterOptionsUrl, params, function (options) {
        renderCheckboxOptions($dropdown, options);
        restoreChecks($dropdown, selected);

        var currentTerm = $dropdown.find('.filter-search-input').val();
        filterCheckboxList($dropdown, currentTerm);

        if (done) done();
      });
    }

    function loadFilterOptions(done) {
      var $filters = getFiltersInScope().filter('.column-filter-dropdown');
      if ($filters.length === 0) {
        if (done) done();
        return;
      }

      var pending = $filters.length;
      $filters.each(function () {
        var $dropdown = $(this);
        loadDropdownOptions($dropdown, null, function () {
          pending--;
          if (pending === 0 && done) done();
        });
      });
    }

    function restoreFilterValues(saved) {
      if (!saved) return;
      if (typeof config.customRestoreFilterValues === 'function') {
        config.customRestoreFilterValues(saved, $(config.dataDivSelector), config.filterFieldMap);
        return;
      }

      $.each(saved, function (dropdownId, values) {
        var $dropdown = findFilterDropdown(dropdownId);
        if ($dropdown.length) {
          restoreChecks($dropdown, values);
        }
      });
    }

    function initColumnFilters() {
      getFiltersInScope().each(function () {
        updateFilterCount($(this));
      });
    }

    function initDataTable() {
      var $table = getTableInScope();
      if ($table.length === 0) return;

      var tableEl = $table[0];
      if ($.fn.dataTable.isDataTable(tableEl)) {
        $table.DataTable().destroy();
      }

      $table.find('tbody tr').each(function () {
        var $cells = $(this).children('td');
        if ($cells.length === 1 && $cells.first().attr('colspan')) {
          $(this).remove();
        }
      });

      var $headerCells = $table.find('thead tr').first().children('th');
      var columnCount = $headerCells.length;
      var order = (tableSortState || []).filter(function (entry) {
        return entry && entry[0] < columnCount;
      });
      if (!order.length) {
        order = (config.initialSort || [[0, 'desc']]).filter(function (entry) {
          return entry && entry[0] < columnCount;
        });
      }

      // Honor th.sorting_disabled so checkbox/action columns do not trigger sort (and page reset).
      var disabledTargets = [];
      $headerCells.each(function (index) {
        if ($(this).hasClass('sorting_disabled')) {
          disabledTargets.push(index);
        }
      });

      var dtOptions = {
        orderCellsTop: true,
        order: order,
        searching: false,
        paging: false,
        lengthChange: false,
        info: false
      };

      if (global.IcpDataTablesLanguage) {
        dtOptions.language = $.extend(true, {}, global.IcpDataTablesLanguage);
      }

      var emptyMessage = $table.attr('data-empty-table');
      if (emptyMessage) {
        dtOptions.language = dtOptions.language || {};
        dtOptions.language.emptyTable = emptyMessage;
      }

      if (config.dataTableOptions) {
        dtOptions = $.extend(true, {}, dtOptions, config.dataTableOptions);
      }

      if (disabledTargets.length) {
        var autoColumnDefs = [{ targets: disabledTargets, orderable: false }];
        dtOptions.columnDefs = autoColumnDefs.concat(dtOptions.columnDefs || []);
      }

      if (typeof config.onDraw === 'function') {
        var existingDrawCallback = dtOptions.drawCallback;
        var onDraw = config.onDraw;
        dtOptions.drawCallback = function () {
          if (typeof existingDrawCallback === 'function') {
            existingDrawCallback.apply(this, arguments);
          }
          onDraw($(config.dataDivSelector));
        };
      }

      $table.DataTable(dtOptions);
    }

    function renderServerPager() {
      var $root = $(config.dataDivSelector);
      $root.find('.pro-server-pager').remove();

      var pageCount = Math.max(1, Math.ceil(totalRecordCount / tablePageLengthState));
      var start = totalRecordCount === 0 ? 0 : ((pageState - 1) * tablePageLengthState) + 1;
      var end = Math.min(pageState * tablePageLengthState, totalRecordCount);
      var $pager = $('<div class="pro-server-pager d-flex align-items-center justify-content-between flex-wrap gap-2 mt-2"></div>');
      var $summary = $('<div class="small text-muted"></div>').text(start + '-' + end + ' / ' + totalRecordCount);
      var $controls = $('<div class="d-flex align-items-center gap-2"></div>');
      var $size = $('<select class="form-select form-select-sm pro-server-page-size" style="width:auto"></select>');
      $.each(global.ProDataTables.resolveLengthMenu(config)[0], function (_, value) {
        $size.append($('<option></option>').val(value).text(value));
      });
      $size.val(String(tablePageLengthState));
      var $previous = $('<button type="button" class="btn btn-sm btn-outline-secondary pro-server-page-prev">‹</button>')
        .prop('disabled', pageState <= 1);
      var $page = $('<select class="form-select form-select-sm pro-server-page-jump" style="width:auto"></select>')
        .attr('aria-label', (global.IcpI18n && global.IcpI18n.pageLabel) || 'Page')
        .prop('disabled', pageCount <= 1);
      var pageOptions = document.createDocumentFragment();
      for (var page = 1; page <= pageCount; page++) {
        pageOptions.appendChild(new Option(String(page), String(page)));
      }
      $page.append(pageOptions).val(String(pageState));
      var $current = $('<span class="small text-nowrap"></span>').text('/ ' + pageCount);
      var $next = $('<button type="button" class="btn btn-sm btn-outline-secondary pro-server-page-next">›</button>')
        .prop('disabled', pageState >= pageCount);
      $controls.append($size, $previous, $page, $current, $next);
      $pager.append($summary, $controls);
      $root.append($pager);
    }

    function Query(options) {
      options = options || {};
      if (config.preserveSort === false) {
        tableSortState = config.initialSort || [[0, 'desc']];
      }

      if (options.page) {
        pageState = options.page;
      } else if (!options.keepPage) {
        pageState = 1;
      }

      saveTableState();
      var saved = getFilterValues();
      $(config.dataDivSelector).empty();

      var requestData = buildQueryPayload(saved);
      requestData.Page = pageState;
      requestData.PageSize = tablePageLengthState;
      if (config.extraQueryParams) {
        var extra = typeof config.extraQueryParams === 'function'
          ? config.extraQueryParams()
          : config.extraQueryParams;
        if (extra) {
          $.extend(requestData, extra);
        }
      }

      $.ajax({
        url: config.queryUrl,
        type: 'POST',
        traditional: true,
        data: requestData,
        success: function (data, _status, xhr) {
          totalRecordCount = parseInt(xhr.getResponseHeader('X-ICP-Total-Count'), 10) || 0;
          pageState = parseInt(xhr.getResponseHeader('X-ICP-Page'), 10) || pageState;
          tablePageLengthState = parseInt(xhr.getResponseHeader('X-ICP-Page-Size'), 10) || tablePageLengthState;
          $(config.dataDivSelector).html(data);
          loadFilterOptions(function () {
            restoreFilterValues(saved);
            initColumnFilters();
            initDataTable();
            renderServerPager();
            if (typeof config.onAfterRender === 'function') {
              config.onAfterRender($(config.dataDivSelector));
            }
          });
        }
      });
    }

    $(document)
      .off('click.' + instanceNs, config.dataDivSelector + ' .filter-confirm')
      .off('click.' + instanceNs, config.dataDivSelector + ' .filter-reset')
      .off('click.' + instanceNs, config.dataDivSelector + ' .filter-select-all')
      .off('click.' + instanceNs, config.dataDivSelector + ' .filter-clear')
      .off('change.' + instanceNs, config.dataDivSelector + ' .column-filter-cb')
      .off('click.' + instanceNs, config.dataDivSelector + ' .filter-search-input')
      .off('keydown.' + instanceNs, config.dataDivSelector + ' .filter-search-input')
      .off('input.' + instanceNs, config.dataDivSelector + ' .filter-search-input')
      .off('shown.bs.dropdown.' + instanceNs, config.dataDivSelector + ' .column-filter-dropdown')
      .off('hidden.bs.dropdown.' + instanceNs, config.dataDivSelector + ' .column-filter-dropdown')
      .off('click.' + instanceNs, config.dataDivSelector + ' .pro-server-page-prev')
      .off('click.' + instanceNs, config.dataDivSelector + ' .pro-server-page-next')
      .off('change.' + instanceNs, config.dataDivSelector + ' .pro-server-page-jump')
      .off('change.' + instanceNs, config.dataDivSelector + ' .pro-server-page-size');

    $(document).on('click.' + instanceNs, config.dataDivSelector + ' .pro-server-page-prev', function () {
      if (pageState > 1) Query({ page: pageState - 1, keepPage: true });
    });

    $(document).on('click.' + instanceNs, config.dataDivSelector + ' .pro-server-page-next', function () {
      var pageCount = Math.max(1, Math.ceil(totalRecordCount / tablePageLengthState));
      if (pageState < pageCount) Query({ page: pageState + 1, keepPage: true });
    });

    $(document).on('change.' + instanceNs, config.dataDivSelector + ' .pro-server-page-jump', function () {
      var page = Number($(this).val());
      var pageCount = Math.max(1, Math.ceil(totalRecordCount / tablePageLengthState));
      if (Number.isInteger(page) && page >= 1 && page <= pageCount && page !== pageState) {
        Query({ page: page, keepPage: true });
      }
    });

    $(document).on('change.' + instanceNs, config.dataDivSelector + ' .pro-server-page-size', function () {
      tablePageLengthState = parseInt($(this).val(), 10) || global.ProDataTables.defaults.pageLength;
      Query();
    });

    $(document).on('change.' + instanceNs, config.dataDivSelector + ' .column-filter-cb', function () {
      updateFilterCount($(this).closest('.column-filter-dropdown'));
    });

    $(document).on('click.' + instanceNs, config.dataDivSelector + ' .filter-select-all', function (e) {
      e.preventDefault();
      e.stopPropagation();
      var $dropdown = $(this).closest('.column-filter-dropdown');
      $dropdown.find('.users-filter-options .form-check:visible .column-filter-cb').prop('checked', true);
      updateFilterCount($dropdown);
    });

    $(document).on('click.' + instanceNs, config.dataDivSelector + ' .filter-clear', function (e) {
      e.preventDefault();
      e.stopPropagation();
      var $dropdown = $(this).closest('.column-filter-dropdown');
      $dropdown.find('.users-filter-options .form-check:visible .column-filter-cb').prop('checked', false);
      updateFilterCount($dropdown);
    });

    $(document).on('click.' + instanceNs, config.dataDivSelector + ' .filter-confirm', function (e) {
      e.preventDefault();
      e.stopPropagation();
      var $dropdown = $(this).closest('.column-filter-dropdown');
      closeFilterDropdown($dropdown);
      Query();
    });

    $(document).on('click.' + instanceNs, config.dataDivSelector + ' .filter-reset', function (e) {
      e.preventDefault();
      e.stopPropagation();
      var $dropdown = $(this).closest('.column-filter-dropdown');
      $dropdown.find('.filter-search-input').val('');
      $dropdown.find('.column-filter-cb').prop('checked', false);
      updateFilterCount($dropdown);

      loadDropdownOptions($dropdown, null, function () {
        closeFilterDropdown($dropdown);
        Query();
      });
    });

    $(document).on('click.' + instanceNs + ' keydown.' + instanceNs, config.dataDivSelector + ' .filter-search-input', function (e) {
      e.stopPropagation();
    });

    $(document).on('input.' + instanceNs, config.dataDivSelector + ' .filter-search-input', function () {
      var $dropdown = $(this).closest('.column-filter-dropdown');
      var term = $(this).val();

      filterCheckboxList($dropdown, term);

      var dropdownId = $dropdown.attr('id');
      clearTimeout(filterSearchDebounceTimers[dropdownId]);
      filterSearchDebounceTimers[dropdownId] = setTimeout(function () {
        var searchTerm = (term || '').trim();
        loadDropdownOptions($dropdown, searchTerm.length > 0 ? searchTerm : null);
      }, config.searchDebounceMs);
    });

    $(document).on('shown.bs.dropdown.' + instanceNs, config.dataDivSelector + ' .column-filter-dropdown', function () {
      $(this).find('.filter-search-input').trigger('focus');
    });

    $(document).on('hidden.bs.dropdown.' + instanceNs, config.dataDivSelector + ' .column-filter-dropdown', function () {
      var $dropdown = $(this);
      $dropdown.find('.filter-search-input').val('');
      loadDropdownOptions($dropdown, null);
    });

    if (config.autoLoad !== false) {
      Query();
    }

    return { reload: Query };
  }

  global.ProDataTables.initUsers = function (config) {
    config = global.ProDataTables.buildConfig(config);
    if (!config.filterFieldMap) {
      config.filterFieldMap = {};
    }
    return ProDataTablesInitUsers(config);
  };
})(window, window.jQuery);

