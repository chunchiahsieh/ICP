(function (global, $) {
  'use strict';

  global.ProDataTables = global.ProDataTables || {};

  function closeFilterDropdown($dropdown) {
    var toggle = $dropdown.find('[data-bs-toggle="dropdown"]')[0];
    if (toggle && global.bootstrap && global.bootstrap.Dropdown) {
      global.bootstrap.Dropdown.getOrCreateInstance(toggle).hide();
    }
  }

  function ProDataTablesInitUsers(config) {
    var tableSortState = config.initialSort || [[0, 'desc']];
    var filterSearchDebounceTimers = {};
    var instanceNs = 'ProDT' + (config.dataDivSelector || '#DataDiv').replace(/[^a-zA-Z0-9]/g, '_');

    function getFiltersInScope() {
      return $(config.dataDivSelector).find(config.filterDropdownSelector);
    }

    function saveTableSortState() {
      var tableEl = document.querySelector(config.tableSelector);
      if (!tableEl) return;
      if (!$.fn.dataTable.isDataTable(tableEl)) return;
      var api = $(config.tableSelector).DataTable();
      tableSortState = api.order();
    }

    function getFilterValuesForDropdown($dropdown) {
      return $dropdown.find('.column-filter-cb:checked').map(function () {
        return $(this).val();
      }).get();
    }

    function getFilterValues() {
      var values = {};
      getFiltersInScope().each(function () {
        values[this.id] = getFilterValuesForDropdown($(this));
      });
      return values;
    }

    function buildQueryPayload(saved) {
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
      $container.empty();
      $.each(options, function (index, value) {
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
            .text(value)
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

      $.get(config.filterOptionsUrl, params, function (options) {
        renderCheckboxOptions($dropdown, options);
        restoreChecks($dropdown, selected);

        // Apply client filter to keep immediate UX even while typing.
        var currentTerm = $dropdown.find('.filter-search-input').val();
        filterCheckboxList($dropdown, currentTerm);

        if (done) done();
      });
    }

    function loadFilterOptions(done) {
      var $filters = getFiltersInScope();
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
      $.each(saved, function (dropdownId, values) {
        var $dropdown = $('#' + dropdownId);
        restoreChecks($dropdown, values);
      });
    }

    function initColumnFilters() {
      getFiltersInScope().each(function () {
        updateFilterCount($(this));
      });
    }

    function initDataTable() {
      var tableEl = document.querySelector(config.tableSelector);
      if (!tableEl) return;

      if ($.fn.dataTable.isDataTable(tableEl)) {
        $(config.tableSelector).DataTable().destroy();
      }

      $(config.tableSelector).DataTable({
        orderCellsTop: true,
        order: tableSortState,
        searching: false,
        pageLength: config.pageLength || 25,
        lengthMenu: config.lengthMenu || [[10, 25, 50, 100], [10, 25, 50, 100]],
        destroy: true
      });
    }

    function Query() {
      saveTableSortState();
      var saved = getFilterValues();
      $(config.dataDivSelector).empty();

      $.ajax({
        url: config.queryUrl,
        type: 'POST',
        traditional: true,
        data: buildQueryPayload(saved),
        success: function (data) {
          $(config.dataDivSelector).html(data);
          loadFilterOptions(function () {
            restoreFilterValues(saved);
            initColumnFilters();
            initDataTable();
            if (typeof config.onAfterRender === 'function') {
              config.onAfterRender($(config.dataDivSelector));
            }
          });
        }
      });
    }

    // Event bindings scoped per instance.
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
      .off('hidden.bs.dropdown.' + instanceNs, config.dataDivSelector + ' .column-filter-dropdown');

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
      }, config.searchDebounceMs || 300);
    });

    $(document).on('shown.bs.dropdown.' + instanceNs, config.dataDivSelector + ' .column-filter-dropdown', function () {
      $(this).find('.filter-search-input').trigger('focus');
    });

    $(document).on('hidden.bs.dropdown.' + instanceNs, config.dataDivSelector + ' .column-filter-dropdown', function () {
      var $dropdown = $(this);
      $dropdown.find('.filter-search-input').val('');
      loadDropdownOptions($dropdown, null);
    });

    // Initial load.
    Query();

    return { reload: Query };
  }

  global.ProDataTables.initUsers = function (config) {
    config = config || {};

    if (!config.tableSelector) config.tableSelector = '#datatable';
    if (!config.dataDivSelector) config.dataDivSelector = '#DataDiv';
    if (!config.filterDropdownSelector) config.filterDropdownSelector = '.column-filter-dropdown';
    if (!config.filterFieldMap) config.filterFieldMap = {};

    return ProDataTablesInitUsers(config);
  };
})(window, window.jQuery);

