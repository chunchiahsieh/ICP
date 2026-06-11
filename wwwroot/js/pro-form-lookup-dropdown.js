(function (global, $) {
  'use strict';

  global.ProFormLookupDropdown = global.ProFormLookupDropdown || {};

  var searchDebounceTimers = {};

  function closeDropdown($dropdown) {
    var toggle = $dropdown.find('[data-bs-toggle="dropdown"]')[0];
    if (toggle && global.bootstrap && global.bootstrap.Dropdown) {
      global.bootstrap.Dropdown.getOrCreateInstance(toggle).hide();
    }
  }

  function parseSelectedKeys(value) {
    if (!value) {
      return [];
    }

    return value.split(',').map(function (item) {
      return item.trim();
    }).filter(Boolean);
  }

  function updateCount($dropdown) {
    var count = $dropdown.find('.column-filter-cb:checked').length;
    $dropdown.find('.filter-count').text('(' + count + ')');
  }

  function renderOptions($dropdown, options) {
    var $container = $dropdown.find('.users-filter-options');
    $container.empty();

    $.each(options, function (index, item) {
      var key = typeof item === 'string' ? item : item.key;
      var text = typeof item === 'string' ? item : item.text;
      var id = $dropdown.attr('id') + '-cb-' + index;
      var $item = $('<div class="form-check"></div>');

      $item.append(
        $('<input type="checkbox" class="form-check-input column-filter-cb">')
          .attr('id', id)
          .attr('value', key)
          .attr('data-text', text)
      );
      $item.append(
        $('<label class="form-check-label"></label>')
          .attr('for', id)
          .text(text)
      );
      $container.append($item);
    });
  }

  function filterCheckboxList($dropdown, term) {
    term = (term || '').toLowerCase().trim();
    var visibleCount = 0;

    $dropdown.find('.users-filter-options .form-check').each(function () {
      var text = $(this).find('.form-check-label').text().toLowerCase();
      var match = !term || text.indexOf(term) >= 0;
      $(this).toggle(match);
      if (match) {
        visibleCount++;
      }
    });

    $dropdown.find('.users-filter-empty').toggleClass('d-none', visibleCount > 0);
  }

  function updateDisplay($dropdown) {
    var texts = [];
    $dropdown.find('.column-filter-cb:checked').each(function () {
      texts.push($(this).attr('data-text') || $(this).val());
    });
    $dropdown.closest('.mb-3').find('.form-field-lookup-display').text(texts.join(', '));
  }

  function applySelection($dropdown) {
    var mode = ($dropdown.data('mode') || 'multi').toString().toLowerCase();
    var $checked = $dropdown.find('.column-filter-cb:checked');

    if (mode === 'single' && $checked.length > 1) {
      $checked.slice(1).prop('checked', false);
      $checked = $dropdown.find('.column-filter-cb:checked');
    }

    var keys = $checked.map(function () {
      return $(this).val();
    }).get();

    var hiddenName = $dropdown.data('hidden-name');
    var $hidden = $dropdown.closest('form').find('[name="' + hiddenName + '"]');
    $hidden.val(mode === 'single' ? (keys[0] || '') : keys.join(','));
    updateCount($dropdown);
    updateDisplay($dropdown);
  }

  function restoreFromHidden($dropdown) {
    var hiddenName = $dropdown.data('hidden-name');
    var $hidden = $dropdown.closest('form').find('[name="' + hiddenName + '"]');
    var keys = parseSelectedKeys($hidden.val());
    var mode = ($dropdown.data('mode') || 'multi').toString().toLowerCase();

    if (mode === 'single' && keys.length > 1) {
      keys = keys.slice(0, 1);
    }

    $dropdown.find('.column-filter-cb').each(function () {
      $(this).prop('checked', keys.indexOf($(this).val()) >= 0);
    });

    updateCount($dropdown);
    updateDisplay($dropdown);
  }

  function loadOptions($dropdown, search, done) {
    var field = $dropdown.data('field');
    var url = $dropdown.data('options-url');
    var params = { field: field };

    if (search) {
      params.search = search;
    }

    $.get(url, params, function (options) {
      renderOptions($dropdown, options || []);
      restoreFromHidden($dropdown);
      filterCheckboxList($dropdown, $dropdown.find('.filter-search-input').val());
      if (typeof done === 'function') {
        done();
      }
    }).fail(function () {
      renderOptions($dropdown, []);
      if (typeof done === 'function') {
        done();
      }
    });
  }

  function bindDropdownEvents($form, ns) {
    $form.off('click' + ns, '.form-field-lookup .filter-confirm')
      .on('click' + ns, '.form-field-lookup .filter-confirm', function (e) {
        e.preventDefault();
        e.stopPropagation();
        var $dropdown = $(this).closest('.form-field-lookup');
        applySelection($dropdown);
        closeDropdown($dropdown);
      });

    $form.off('click' + ns, '.form-field-lookup .filter-reset')
      .on('click' + ns, '.form-field-lookup .filter-reset', function (e) {
        e.preventDefault();
        e.stopPropagation();
        var $dropdown = $(this).closest('.form-field-lookup');
        $dropdown.find('.filter-search-input').val('');
        $dropdown.find('.column-filter-cb').prop('checked', false);
        applySelection($dropdown);
        loadOptions($dropdown, null, function () {
          closeDropdown($dropdown);
        });
      });

    $form.off('click' + ns, '.form-field-lookup .filter-select-all')
      .on('click' + ns, '.form-field-lookup .filter-select-all', function (e) {
        e.preventDefault();
        e.stopPropagation();
        var $dropdown = $(this).closest('.form-field-lookup');
        if (($dropdown.data('mode') || '').toString().toLowerCase() === 'single') {
          return;
        }
        $dropdown.find('.users-filter-options .form-check:visible .column-filter-cb').prop('checked', true);
        updateCount($dropdown);
      });

    $form.off('click' + ns, '.form-field-lookup .filter-clear')
      .on('click' + ns, '.form-field-lookup .filter-clear', function (e) {
        e.preventDefault();
        e.stopPropagation();
        var $dropdown = $(this).closest('.form-field-lookup');
        $dropdown.find('.users-filter-options .form-check:visible .column-filter-cb').prop('checked', false);
        updateCount($dropdown);
      });

    $form.off('change' + ns, '.form-field-lookup[data-mode="single"] .column-filter-cb')
      .on('change' + ns, '.form-field-lookup[data-mode="single"] .column-filter-cb', function () {
        if (!$(this).is(':checked')) {
          return;
        }
        $(this).closest('.users-filter-options').find('.column-filter-cb').not(this).prop('checked', false);
        updateCount($(this).closest('.form-field-lookup'));
      });

    $form.off('click' + ns + ' keydown' + ns, '.form-field-lookup .filter-search-input')
      .on('click' + ns + ' keydown' + ns, '.form-field-lookup .filter-search-input', function (e) {
        e.stopPropagation();
      });

    $form.off('input' + ns, '.form-field-lookup .filter-search-input')
      .on('input' + ns, '.form-field-lookup .filter-search-input', function () {
        var $dropdown = $(this).closest('.form-field-lookup');
        var term = $(this).val();
        filterCheckboxList($dropdown, term);

        var dropdownId = $dropdown.attr('id');
        clearTimeout(searchDebounceTimers[dropdownId]);
        searchDebounceTimers[dropdownId] = setTimeout(function () {
          var searchTerm = ($dropdown.find('.filter-search-input').val() || '').trim();
          loadDropdownOptions($dropdown, searchTerm.length > 0 ? searchTerm : null);
        }, 300);
      });

    $form.off('shown.bs.dropdown' + ns, '.form-field-lookup')
      .on('shown.bs.dropdown' + ns, '.form-field-lookup', function () {
        $(this).find('.filter-search-input').trigger('focus');
      });

    $form.off('hidden.bs.dropdown' + ns, '.form-field-lookup')
      .on('hidden.bs.dropdown' + ns, '.form-field-lookup', function () {
        var $dropdown = $(this);
        $dropdown.find('.filter-search-input').val('');
        loadOptions($dropdown, null);
      });
  }

  function loadDropdownOptions($dropdown, search) {
    loadOptions($dropdown, search);
  }

  global.ProFormLookupDropdown.init = function (config) {
    config = config || {};
    var $form = $(config.formSelector || '#editForm');
    var ns = config.eventNamespace || '.proFormLookup';

    bindDropdownEvents($form, ns);

    $form.find('.form-field-lookup').each(function () {
      loadOptions($(this), null);
    });
  };

  global.ProFormLookupDropdown.refresh = function ($form) {
    $form = $form || $('#editForm');
    $form.find('.form-field-lookup').each(function () {
      loadOptions($(this), null);
    });
  };
})(window, window.jQuery);
