(function (global, $) {
  'use strict';

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

  function initRoleAssignment(config) {
    config = config || {};

    function proDt(overrides) {
      return global.ProDataTables.buildConfig($.extend({
        filterDropdownSelector: '.column-filter-dropdown',
        searchDebounceMs: 300,
        pageLength: 10
      }, overrides || {}));
    }

    var currentStep = 1;
    var selectedRoles = new Map();
    var selectedUsers = new Map();
    var selectedListIds = new Set();
    var pendingBatchDelete = false;
    var wizardDrawerSelector = config.wizardDrawerSelector
      || config.wizardModalSelector
      || '#batchCreateWizardDrawer';
    var wizardDrawerInstance = getOffcanvasInstance(wizardDrawerSelector);
    var confirmModalInstance = getModalInstance('#crudConfirmModal');
    var rolesTableInstance;
    var usersTableInstance;
    var resultTableInstance;
    var rolesTableInitialized = false;
    var usersTableInitialized = false;

    var $wizardDrawer = $(wizardDrawerSelector);
    var $wizardAlert = $('#wizardAlert');
    var $btnWizardBack = $('#btnWizardBack');
    var $btnWizardNext = $('#btnWizardNext');
    var $btnWizardSubmit = $('#btnWizardSubmit');
    var userPayloadKey = config.userPayloadKey || 'telIds';

    function hideWizardAlert() {
      $wizardAlert.addClass('d-none').text('');
    }

    function showWizardAlert(message) {
      $wizardAlert.removeClass('d-none').text(message);
    }

    function updateStepHints() {
      $('#rolesStepSelectedHint').text(icpMsg('selectedCount', selectedRoles.size));
      $('#usersStepSelectedHint').text(icpMsg('selectedCount', selectedUsers.size));
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

    function restoreUserPicks($container) {
      bindPickTable($container, selectedUsers);
      updateStepHints();
    }

    function restoreListSelection($container) {
      bindListTable($container, selectedListIds);
    }

    function initRolesPickTable() {
      if (!global.ProDataTables || !ProDataTables.initUsers) {
        return;
      }

      rolesTableInstance = ProDataTables.initUsers(proDt({
        tableSelector: config.rolesTableSelector,
        dataDivSelector: config.rolesPickDivSelector,
        filterFieldMap: config.rolesFilterFieldMap,
        filterOptionsUrl: config.rolesFilterOptionsUrl,
        queryUrl: config.rolesQueryUrl,
        initialSort: [[1, 'asc']],
        onAfterRender: restoreRolePicks
      }));
      rolesTableInitialized = true;
    }

    function initUsersPickTable() {
      if (!global.ProDataTables || !ProDataTables.initUsers) {
        return;
      }

      usersTableInstance = ProDataTables.initUsers(proDt({
        tableSelector: config.usersTableSelector,
        dataDivSelector: config.usersPickDivSelector,
        filterFieldMap: config.usersFilterFieldMap,
        filterOptionsUrl: config.usersFilterOptionsUrl,
        queryUrl: config.usersQueryUrl,
        initialSort: [[1, 'asc']],
        onAfterRender: restoreUserPicks
      }));
      usersTableInitialized = true;
    }

    function ensureRolesTableForStep() {
      if (!rolesTableInitialized) {
        initRolesPickTable();
      } else {
        bindPickTable($(config.rolesPickDivSelector), selectedRoles);
        adjustDataTable(config.rolesTableSelector);
      }
    }

    function ensureUsersTableForStep() {
      if (!usersTableInitialized) {
        initUsersPickTable();
      } else {
        bindPickTable($(config.usersPickDivSelector), selectedUsers);
        adjustDataTable(config.usersTableSelector);
      }
    }

    function renderPreview() {
      var roleCount = selectedRoles.size;
      var userCount = selectedUsers.size;

      $('#previewRoleCount').text(roleCount);
      $('#previewUserCount').text(userCount);
      $('#previewEstimatedCount').text(roleCount * userCount);
      $('#previewFormula').text(roleCount + ' × ' + userCount);

      var $roleList = $('#previewRoleList').empty();
      selectedRoles.forEach(function (item) {
        $roleList.append(
          '<li class="list-group-item py-1">' +
          escapeHtml(item.code) + ' - ' + escapeHtml(item.name) +
          '</li>'
        );
      });
      if (roleCount === 0) {
        $roleList.append('<li class="list-group-item py-1 text-muted">' + icpMsg('none') + '</li>');
      }

      var $userList = $('#previewUserList').empty();
      selectedUsers.forEach(function (item) {
        $userList.append(
          '<li class="list-group-item py-1">' +
          escapeHtml(item.code) + ' - ' + escapeHtml(item.name) +
          '</li>'
        );
      });
      if (userCount === 0) {
        $userList.append('<li class="list-group-item py-1 text-muted">' + icpMsg('none') + '</li>');
      }
    }

    function showStep(step) {
      currentStep = step;
      hideWizardAlert();

      $('#wizardStepRoles').toggleClass('d-none', step !== 1);
      $('#wizardStepUsers').toggleClass('d-none', step !== 2);
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
        ensureUsersTableForStep();
        setTimeout(function () {
          adjustDataTable(config.usersTableSelector);
        }, 50);
      } else if (step === 3) {
        renderPreview();
      }
    }

    function resetWizardState() {
      currentStep = 1;
      selectedRoles.clear();
      selectedUsers.clear();
      hideWizardAlert();
      setSubmitLoading(false);
      $('#wizardStepRoles').removeClass('d-none');
      $('#wizardStepUsers').addClass('d-none');
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

    function showPageMessage(type, message) {
      var $message = $('#pageMessage');
      $message
        .removeClass('d-none alert-success alert-danger')
        .addClass(type === 'success' ? 'alert-success' : 'alert-danger')
        .text(message);
    }

    if (global.ProDataTables && ProDataTables.initUsers) {
      resultTableInstance = ProDataTables.initUsers(proDt({
        tableSelector: config.resultTableSelector,
        dataDivSelector: config.resultDivSelector,
        filterFieldMap: config.resultFilterFieldMap,
        filterOptionsUrl: config.resultFilterOptionsUrl,
        queryUrl: config.resultQueryUrl,
        initialSort: [[1, 'asc']],
        onAfterRender: restoreListSelection
      }));
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
          showWizardAlert(icpMsg('selectAtLeastOneRole'));
          return;
        }
        showStep(2);
      } else if (currentStep === 2) {
        if (selectedUsers.size === 0) {
          showWizardAlert(config.userRequiredMessage || icpMsg('selectAtLeastOneUser'));
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

    $(document).on('change', config.usersPickDivSelector + ' .row-pick-cb', function () {
      var $row = $(this).closest('tr');
      var meta = getRowPickMeta($row);
      if ($(this).is(':checked')) {
        selectedUsers.set(meta.id, { code: meta.code, name: meta.name });
      } else {
        selectedUsers.delete(meta.id);
      }
      bindPickTable($(config.usersPickDivSelector), selectedUsers);
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

    $(document).on('change', config.usersPickDivSelector + ' .pick-select-all', function () {
      var checked = $(this).is(':checked');
      $(config.usersPickDivSelector).find('tbody tr[data-id]').each(function () {
        var $row = $(this);
        var meta = getRowPickMeta($row);
        $row.find('.row-pick-cb').prop('checked', checked);
        if (checked) {
          selectedUsers.set(meta.id, { code: meta.code, name: meta.name });
        } else {
          selectedUsers.delete(meta.id);
        }
      });
      updateStepHints();
    });

    $btnWizardSubmit.on('click', function () {
      var roleIds = Array.from(selectedRoles.keys());
      var userKeys = Array.from(selectedUsers.keys());

      if (roleIds.length === 0 || userKeys.length === 0) {
        showWizardAlert(icpMsg('selectAtLeastOneRoleAndUser'));
        return;
      }

      setSubmitLoading(true);
      hideWizardAlert();

      var payload = { roleIds: roleIds };
      payload[userPayloadKey] = userKeys;

      $.ajax({
        url: config.batchCreateUrl,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
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
              icpMsg('batchCreateSuccess', result.insertedCount, result.skippedCount)
            );
          } else {
            showWizardAlert(result.message || icpMsg('createFailed'));
          }
        },
        error: function () {
          showWizardAlert(icpMsg('createFailedRetry'));
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
        showPageMessage('danger', icpMsg('selectAtLeastOneRecord'));
        return;
      }

      pendingBatchDelete = true;
      $('#crudConfirmMessage').text(config.batchDeleteConfirmMessage || '確定要刪除所選資料嗎？');
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
            showPageMessage('success', icpMsg('deleteSuccess', result.deletedCount || ids.length));
          } else {
            showPageMessage('danger', result.message || icpMsg('deleteFailed'));
          }
        },
        error: function () {
          showPageMessage('danger', icpMsg('deleteFailedRetry'));
        }
      });
    });

    $($(config.confirmModalSelector || '#crudConfirmModal')).on('hidden.bs.modal', function () {
      pendingBatchDelete = false;
    });
  }

  global.ProRoleAssignment = global.ProRoleAssignment || {};
  global.ProRoleAssignment.init = initRoleAssignment;
  global.ProRoleTelIds = global.ProRoleTelIds || {};
  global.ProRoleTelIds.init = initRoleAssignment;
  global.ProRoleDepIds = global.ProRoleDepIds || {};
  global.ProRoleDepIds.init = initRoleAssignment;
})(window, window.jQuery);
