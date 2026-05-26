(function (global, $) {
  'use strict';

  global.ProRolePermissions = global.ProRolePermissions || {};

  function getModalInstance(selector) {
    var el = document.querySelector(selector);
    if (!el || !global.bootstrap || !global.bootstrap.Modal) {
      return null;
    }
    return global.bootstrap.Modal.getOrCreateInstance(el);
  }

  function getOffcanvasInstance(selector) {
    var el = document.querySelector(selector);
    if (!el || !global.bootstrap || !global.bootstrap.Offcanvas) {
      return null;
    }
    return global.bootstrap.Offcanvas.getOrCreateInstance(el);
  }

  function getRowPickMeta($row) {
    return {
      id: String($row.data('id')),
      code: String($row.data('code') || ''),
      name: String($row.data('name') || '')
    };
  }

  function bindListTable($container, selectedSet) {
    $container.find('tbody tr[data-id]').each(function () {
      var id = String($(this).data('id'));
      $(this).find('.row-list-cb').prop('checked', selectedSet.has(id));
    });

    var $rows = $container.find('tbody .row-list-cb');
    var $selectAll = $container.find('thead .list-select-all');
    if ($rows.length === 0) {
      $selectAll.prop('checked', false);
      return;
    }

    $selectAll.prop('checked', $rows.length === $rows.filter(':checked').length);
  }

  function bindPickTable($container, selectedMap) {
    $container.find('tbody tr[data-id]').each(function () {
      var meta = getRowPickMeta($(this));
      $(this).find('.row-pick-cb').prop('checked', selectedMap.has(meta.id));
    });

    var $rows = $container.find('tbody .row-pick-cb');
    var $selectAll = $container.find('thead .pick-select-all');
    if ($rows.length === 0) {
      $selectAll.prop('checked', false);
      return;
    }

    var allChecked = $rows.length === $rows.filter(':checked').length;
    $selectAll.prop('checked', allChecked);
  }

  function adjustDataTable(tableSelector) {
    var tableEl = document.querySelector(tableSelector);
    if (!tableEl || !$.fn.dataTable.isDataTable(tableEl)) {
      return;
    }
    $(tableSelector).DataTable().columns.adjust().draw(false);
  }

  function escapeHtml(text) {
    return $('<div>').text(text).html();
  }

  global.ProRolePermissions.init = function (config) {
    config = config || {};

    var currentStep = 1;
    var selectedRoles = new Map();
    var selectedResources = new Map();
    var selectedListIds = new Set();
    var pendingBatchDelete = false;
    var wizardDrawerSelector = config.wizardDrawerSelector
      || config.wizardModalSelector
      || '#batchCreateWizardDrawer';
    var wizardDrawerInstance = getOffcanvasInstance(wizardDrawerSelector);
    var confirmModalInstance = getModalInstance('#crudConfirmModal');
    var rolesTableInstance;
    var resourcesTableInstance;
    var resultTableInstance;
    var rolesTableInitialized = false;
    var resourcesTableInitialized = false;

    var $wizardDrawer = $(wizardDrawerSelector);
    var $wizardAlert = $('#wizardAlert');
    var $btnWizardBack = $('#btnWizardBack');
    var $btnWizardNext = $('#btnWizardNext');
    var $btnWizardSubmit = $('#btnWizardSubmit');

    function hideWizardAlert() {
      $wizardAlert.addClass('d-none').text('');
    }

    function showWizardAlert(message) {
      $wizardAlert.removeClass('d-none').text(message);
    }

    function updateStepHints() {
      $('#rolesStepSelectedHint').text('已選 ' + selectedRoles.size + ' 筆');
      $('#resourcesStepSelectedHint').text('已選 ' + selectedResources.size + ' 筆');
    }

    function updateStepIndicator(step) {
      $('#wizardStepIndicator [data-wizard-step]').each(function () {
        var stepNum = parseInt($(this).data('wizard-step'), 10);
        $(this).removeClass('active');
        if (stepNum === step) {
          $(this).addClass('active');
        }
      });
    }

    function updateFooterButtons(step) {
      $btnWizardBack.toggleClass('d-none', step === 1);
      $btnWizardNext.toggleClass('d-none', step === 3);
      $btnWizardSubmit.toggleClass('d-none', step !== 3);
    }

    function restoreRolePicks($container) {
      bindPickTable($container, selectedRoles);
      updateStepHints();
    }

    function restoreResourcePicks($container) {
      bindPickTable($container, selectedResources);
      updateStepHints();
    }

    function initRolesPickTable() {
      if (!global.ProDataTables || !ProDataTables.initUsers) {
        return;
      }

      rolesTableInstance = ProDataTables.initUsers({
        tableSelector: config.rolesTableSelector,
        dataDivSelector: config.rolesPickDivSelector,
        filterDropdownSelector: '.column-filter-dropdown',
        filterFieldMap: config.rolesFilterFieldMap,
        filterOptionsUrl: config.rolesFilterOptionsUrl,
        queryUrl: config.rolesQueryUrl,
        pageLength: 10,
        lengthMenu: [[10, 25, 50, 100], [10, 25, 50, 100]],
        initialSort: [[1, 'asc']],
        searchDebounceMs: 300,
        onAfterRender: restoreRolePicks
      });
      rolesTableInitialized = true;
    }

    function initResourcesPickTable() {
      if (!global.ProDataTables || !ProDataTables.initUsers) {
        return;
      }

      resourcesTableInstance = ProDataTables.initUsers({
        tableSelector: config.resourcesTableSelector,
        dataDivSelector: config.resourcesPickDivSelector,
        filterDropdownSelector: '.column-filter-dropdown',
        filterFieldMap: config.resourcesFilterFieldMap,
        filterOptionsUrl: config.resourcesFilterOptionsUrl,
        queryUrl: config.resourcesQueryUrl,
        pageLength: 10,
        lengthMenu: [[10, 25, 50, 100], [10, 25, 50, 100]],
        initialSort: [[1, 'asc']],
        searchDebounceMs: 300,
        onAfterRender: restoreResourcePicks
      });
      resourcesTableInitialized = true;
    }

    function ensureRolesTableForStep() {
      if (!rolesTableInitialized) {
        initRolesPickTable();
      } else {
        bindPickTable($(config.rolesPickDivSelector), selectedRoles);
        adjustDataTable(config.rolesTableSelector);
      }
    }

    function ensureResourcesTableForStep() {
      if (!resourcesTableInitialized) {
        initResourcesPickTable();
      } else {
        bindPickTable($(config.resourcesPickDivSelector), selectedResources);
        adjustDataTable(config.resourcesTableSelector);
      }
    }

    function renderPreview() {
      var roleCount = selectedRoles.size;
      var resourceCount = selectedResources.size;

      $('#previewRoleCount').text(roleCount);
      $('#previewResourceCount').text(resourceCount);
      $('#previewEstimatedCount').text(roleCount * resourceCount);
      $('#previewFormula').text(roleCount + ' × ' + resourceCount);

      var $roleList = $('#previewRoleList').empty();
      selectedRoles.forEach(function (item) {
        $roleList.append(
          '<li class="list-group-item py-1">' +
          escapeHtml(item.code) + ' - ' + escapeHtml(item.name) +
          '</li>'
        );
      });
      if (roleCount === 0) {
        $roleList.append('<li class="list-group-item py-1 text-muted">（無）</li>');
      }

      var $resourceList = $('#previewResourceList').empty();
      selectedResources.forEach(function (item) {
        $resourceList.append(
          '<li class="list-group-item py-1">' +
          escapeHtml(item.code) + ' - ' + escapeHtml(item.name) +
          '</li>'
        );
      });
      if (resourceCount === 0) {
        $resourceList.append('<li class="list-group-item py-1 text-muted">（無）</li>');
      }
    }

    function showStep(step) {
      currentStep = step;
      hideWizardAlert();

      $('#wizardStepRoles').toggleClass('d-none', step !== 1);
      $('#wizardStepResources').toggleClass('d-none', step !== 2);
      $('#wizardStepPreview').toggleClass('d-none', step !== 3);

      updateStepIndicator(step);
      updateFooterButtons(step);
      updateStepHints();

      if (step === 1) {
        ensureRolesTableForStep();
        setTimeout(function () {
          adjustDataTable(config.rolesTableSelector);
        }, 50);
      } else if (step === 2) {
        ensureResourcesTableForStep();
        setTimeout(function () {
          adjustDataTable(config.resourcesTableSelector);
        }, 50);
      } else if (step === 3) {
        renderPreview();
      }
    }

    function resetWizardState() {
      currentStep = 1;
      selectedRoles.clear();
      selectedResources.clear();
      hideWizardAlert();
      setSubmitLoading(false);
      $('#wizardStepRoles').removeClass('d-none');
      $('#wizardStepResources').addClass('d-none');
      $('#wizardStepPreview').addClass('d-none');
      updateStepIndicator(1);
      updateFooterButtons(1);
      updateStepHints();
    }

    function setSubmitLoading(loading) {
      $btnWizardSubmit.prop('disabled', loading);
      $btnWizardBack.prop('disabled', loading);
      $btnWizardNext.prop('disabled', loading);
      $btnWizardSubmit.find('.wizard-submit-spinner').toggleClass('d-none', !loading);
      $btnWizardSubmit.find('.wizard-submit-label').toggleClass('d-none', loading);
    }

    function restoreListSelection($container) {
      bindListTable($container, selectedListIds);
    }

    function showPageMessage(type, message) {
      var $message = $('#pageMessage');
      $message
        .removeClass('d-none alert-success alert-danger')
        .addClass(type === 'success' ? 'alert-success' : 'alert-danger')
        .text(message);
    }

    if (global.ProDataTables && ProDataTables.initUsers) {
      resultTableInstance = ProDataTables.initUsers({
        tableSelector: config.resultTableSelector,
        dataDivSelector: config.resultDivSelector,
        filterDropdownSelector: '.column-filter-dropdown',
        filterFieldMap: config.resultFilterFieldMap,
        filterOptionsUrl: config.resultFilterOptionsUrl,
        queryUrl: config.resultQueryUrl,
        pageLength: 10,
        lengthMenu: [[10, 25, 50, 100], [10, 25, 50, 100]],
        initialSort: [[1, 'asc']],
        searchDebounceMs: 300,
        onAfterRender: restoreListSelection
      });
    }

    $('#btnOpenBatchCreateWizard').on('click', function () {
      resetWizardState();
      if (wizardDrawerInstance) {
        wizardDrawerInstance.show();
      }
    });

    $wizardDrawer.on('shown.bs.offcanvas', function () {
      showStep(currentStep);
    });

    $wizardDrawer.on('hidden.bs.offcanvas', function () {
      resetWizardState();
    });

    $('#btnWizardNext').on('click', function () {
      if (currentStep === 1) {
        if (selectedRoles.size === 0) {
          showWizardAlert('請至少選擇一筆角色');
          return;
        }
        showStep(2);
      } else if (currentStep === 2) {
        if (selectedResources.size === 0) {
          showWizardAlert('請至少選擇一筆資源');
          return;
        }
        showStep(3);
      }
    });

    $('#btnWizardBack').on('click', function () {
      if (currentStep > 1) {
        showStep(currentStep - 1);
      }
    });

  $(document).on('change', config.rolesPickDivSelector + ' .row-pick-cb', function () {
      var $row = $(this).closest('tr');
      var meta = getRowPickMeta($row);
      if ($(this).is(':checked')) {
        selectedRoles.set(meta.id, { code: meta.code, name: meta.name });
      } else {
        selectedRoles.delete(meta.id);
      }
      bindPickTable($(config.rolesPickDivSelector), selectedRoles);
      updateStepHints();
    });

    $(document).on('change', config.resourcesPickDivSelector + ' .row-pick-cb', function () {
      var $row = $(this).closest('tr');
      var meta = getRowPickMeta($row);
      if ($(this).is(':checked')) {
        selectedResources.set(meta.id, { code: meta.code, name: meta.name });
      } else {
        selectedResources.delete(meta.id);
      }
      bindPickTable($(config.resourcesPickDivSelector), selectedResources);
      updateStepHints();
    });

    $(document).on('change', config.rolesPickDivSelector + ' .pick-select-all', function () {
      var checked = $(this).is(':checked');
      $(config.rolesPickDivSelector).find('tbody tr[data-id]').each(function () {
        var $row = $(this);
        var meta = getRowPickMeta($row);
        $row.find('.row-pick-cb').prop('checked', checked);
        if (checked) {
          selectedRoles.set(meta.id, { code: meta.code, name: meta.name });
        } else {
          selectedRoles.delete(meta.id);
        }
      });
      updateStepHints();
    });

    $(document).on('change', config.resourcesPickDivSelector + ' .pick-select-all', function () {
      var checked = $(this).is(':checked');
      $(config.resourcesPickDivSelector).find('tbody tr[data-id]').each(function () {
        var $row = $(this);
        var meta = getRowPickMeta($row);
        $row.find('.row-pick-cb').prop('checked', checked);
        if (checked) {
          selectedResources.set(meta.id, { code: meta.code, name: meta.name });
        } else {
          selectedResources.delete(meta.id);
        }
      });
      updateStepHints();
    });

    $btnWizardSubmit.on('click', function () {
      var roleIds = Array.from(selectedRoles.keys());
      var resourceIds = Array.from(selectedResources.keys());

      if (roleIds.length === 0 || resourceIds.length === 0) {
        showWizardAlert('請至少選擇一筆角色與一筆資源');
        return;
      }

      setSubmitLoading(true);
      hideWizardAlert();

      $.ajax({
        url: config.batchCreateUrl,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ roleIds: roleIds, resourceIds: resourceIds }),
        success: function (result) {
          if (result.success) {
            if (wizardDrawerInstance) {
              wizardDrawerInstance.hide();
            }
            if (resultTableInstance && resultTableInstance.reload) {
              resultTableInstance.reload();
            }
            showPageMessage(
              'success',
              '建立完成：新增 ' + result.insertedCount + ' 筆，略過 ' + result.skippedCount + ' 筆。'
            );
          } else {
            showWizardAlert(result.message || '建立失敗');
          }
        },
        error: function () {
          showWizardAlert('建立失敗，請稍後再試。');
        },
        complete: function () {
          setSubmitLoading(false);
        }
      });
    });

    $(document).on('change', config.resultDivSelector + ' .row-list-cb', function () {
      var id = String($(this).data('id'));
      if ($(this).is(':checked')) {
        selectedListIds.add(id);
      } else {
        selectedListIds.delete(id);
      }
      bindListTable($(config.resultDivSelector), selectedListIds);
    });

    $(document).on('change', config.resultDivSelector + ' .list-select-all', function () {
      var checked = $(this).is(':checked');
      $(config.resultDivSelector).find('tbody tr[data-id]').each(function () {
        var id = String($(this).data('id'));
        $(this).find('.row-list-cb').prop('checked', checked);
        if (checked) {
          selectedListIds.add(id);
        } else {
          selectedListIds.delete(id);
        }
      });
    });

    $('#btnBatchDelete').on('click', function () {
      if (selectedListIds.size === 0) {
        showPageMessage('danger', '請至少選擇一筆資料');
        return;
      }

      pendingBatchDelete = true;
      $('#crudConfirmMessage').text(config.batchDeleteConfirmMessage || '確定要刪除所選的角色權限嗎？');
      if (confirmModalInstance) {
        confirmModalInstance.show();
      }
    });

    $('#crudConfirmOk').on('click', function () {
      if (!pendingBatchDelete) {
        return;
      }

      var ids = Array.from(selectedListIds);
      pendingBatchDelete = false;

      $.ajax({
        url: config.batchDeleteUrl,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ ids: ids }),
        success: function (result) {
          if (result.success) {
            if (confirmModalInstance) {
              confirmModalInstance.hide();
            }
            selectedListIds.clear();
            if (resultTableInstance && resultTableInstance.reload) {
              resultTableInstance.reload();
            }
            showPageMessage('success', '已刪除 ' + (result.deletedCount || ids.length) + ' 筆。');
          } else {
            showPageMessage('danger', result.message || '刪除失敗');
          }
        },
        error: function () {
          showPageMessage('danger', '刪除失敗，請稍後再試。');
        }
      });
    });

    $('#crudConfirmModal').on('hidden.bs.modal', function () {
      pendingBatchDelete = false;
    });
  };
})(window, window.jQuery);
