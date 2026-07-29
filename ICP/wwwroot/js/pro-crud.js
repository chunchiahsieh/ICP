(function (global, $) {
  'use strict';

  global.ProCrud = global.ProCrud || {};

  function getModalInstance(selector) {
    var el = document.querySelector(selector);
    if (!el || !global.bootstrap || !global.bootstrap.Modal) {
      return null;
    }
    return global.bootstrap.Modal.getOrCreateInstance(el);
  }

  function clearFormErrors($form) {
    $form.find('.crud-field-error').text('');
    $form.find('.crud-form-error').addClass('d-none').text('');
  }

  function showFormErrors($form, errors) {
    clearFormErrors($form);
    if (!errors) {
      return;
    }

    $.each(errors, function (key, messages) {
      var fieldKey = key.indexOf('.') >= 0 ? key.split('.').pop() : key;
      var $error = $form.find('[data-field-error="' + fieldKey + '"]');
      if ($error.length) {
        $error.text(messages.join(' '));
      }
    });
  }

  function serializeForm($form) {
    var data = {};
    $form.serializeArray().forEach(function (item) {
      var $input = $form.find('[name="' + item.name + '"]');
      if ($input.attr('type') === 'checkbox') {
        data[item.name] = $input.is(':checked');
      } else {
        data[item.name] = item.value;
      }
    });

    $form.find('input[type="checkbox"]').each(function () {
      if (!this.name) return;
      if (!(this.name in data)) {
        data[this.name] = false;
      }
    });

    return data;
  }

  function populateSelect($select, items, valueField, textFn, selectedValue) {
    $select.empty();
    $select.append($('<option value="">' + icpMsg('pleaseSelect') + '</option>'));
    $.each(items || [], function (_, item) {
      var value = item[valueField];
      var $option = $('<option></option>').attr('value', value).text(textFn(item));
      if (selectedValue && selectedValue.toString() === value.toString()) {
        $option.prop('selected', true);
      }
      $select.append($option);
    });
  }

  global.ProCrud.init = function (config) {
    config = config || {};

    var $editModal = $(config.editModalSelector || '#editModal');
    var $confirmModal = $(config.confirmModalSelector || '#crudConfirmModal');
    var $form = $(config.formSelector || '#editForm');
    var editModalInstance = getModalInstance(config.editModalSelector || '#editModal');
    var confirmModalInstance = getModalInstance(config.confirmModalSelector || '#crudConfirmModal');
    var pendingConfirmAction = null;
    var pendingBatchDisable = false;
    var selectedListIds = new Set();
    var listContainerSelector = config.listContainerSelector || '#DataDiv';

    function syncListCheckboxes() {
      $(listContainerSelector).find('tbody tr[data-id]').each(function () {
        var id = String($(this).data('id'));
        $(this).find('.row-list-cb').prop('checked', selectedListIds.has(id));
      });

      var $rows = $(listContainerSelector).find('tbody tr[data-id] .row-list-cb');
      var $checked = $rows.filter(':checked');
      $(listContainerSelector).find('.list-select-all').prop(
        'checked',
        $rows.length > 0 && $checked.length === $rows.length
      );
    }

    function reloadTable() {
      if (typeof config.onSuccess === 'function') {
        config.onSuccess();
      }
      syncListCheckboxes();
    }

    function openEditModal(title) {
      $('#editModalLabel').text(title || icpMsg('edit'));
      clearFormErrors($form);
      if (editModalInstance) {
        editModalInstance.show();
      }
    }

    function loadLookups(model, done) {
      var pending = 0;
      function complete() {
        pending--;
        if (pending <= 0 && done) {
          done(model);
        }
      }

      if (config.roleLookupUrl) {
        pending++;
        $.get(config.roleLookupUrl, function (roles) {
          populateSelect(
            $form.find('[name="RoleId"]'),
            roles,
            'id',
            function (r) { return r.roleCode + ' - ' + r.roleName; },
            model ? model.roleId : null
          );
          complete();
        });
      }

      if (config.resourceLookupUrl) {
        pending++;
        $.get(config.resourceLookupUrl, function (resources) {
          populateSelect(
            $form.find('[name="ResourceId"]'),
            resources,
            'id',
            function (r) { return r.resourceCode + ' - ' + r.resourceName; },
            model ? model.resourceId : null
          );
          complete();
        });
      }

      if (pending === 0 && done) {
        done(model);
      }
    }

    function bindModel(model) {
      model = model || {};
      $form.find('[name="Id"]').val(model.id || '');
      $form.find('[name="RoleCode"]').val(model.roleCode || '');
      $form.find('[name="RoleName"]').val(model.roleName || '');
      $form.find('[name="TelId"]').val(model.telId || '');
      $form.find('[name="DepId"]').val(model.depId || '');
      $form.find('[name="Description"]').val(model.description || '');
      $form.find('[name="DataScope"]').val(model.dataScope || '');
      $form.find('[name="IsEnabled"]').prop('checked', model.isEnabled !== false);
      $form.find('[name="AllowView"]').prop('checked', !!model.allowView);
      $form.find('[name="AllowCreate"]').prop('checked', !!model.allowCreate);
      $form.find('[name="AllowEdit"]').prop('checked', !!model.allowEdit);
      $form.find('[name="AllowDelete"]').prop('checked', !!model.allowDelete);
      $form.find('[name="AllowExport"]').prop('checked', !!model.allowExport);
      $form.find('[name="AllowApprove"]').prop('checked', !!model.allowApprove);
      $form.find('[name="ActionCode"]').val(model.actionCode || '');
      $form.find('[name="IsAllowed"]').prop('checked', model.isAllowed !== false);

      var isEdit = !!model.id;
      $form.find('[name="RoleId"], [name="ResourceId"], [name="ActionCode"]').prop('disabled', isEdit && config.lockForeignKeysOnEdit);
    }

    function openCreate() {
      bindModel({});
      loadLookups(null, function () {
        openEditModal(config.createTitle || icpMsg('create'));
      });
    }

    function openEdit(id) {
      $.get(config.getUrl, { id: id }, function (model) {
        bindModel(model);
        loadLookups(model, function () {
          openEditModal(config.editTitle || icpMsg('edit'));
        });
      }).fail(function () {
        alert(icpMsg('loadFailed'));
      });
    }

    function saveForm() {
      clearFormErrors($form);
      var data = serializeForm($form);
      $form.find('[name="RoleId"], [name="ResourceId"], [name="ActionCode"]').prop('disabled', false);

      if (!data.Id) {
        delete data.Id;
      }

      $.ajax({
        url: config.saveUrl,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(data),
        success: function (result) {
          if (result.success) {
            if (editModalInstance) {
              editModalInstance.hide();
            }
            reloadTable();
            return;
          }

          if (result.errors) {
            showFormErrors($form, result.errors);
          } else if (result.message) {
            $form.find('.crud-form-error').removeClass('d-none').text(result.message);
          }

          if (config.lockForeignKeysOnEdit && data.Id) {
            $form.find('[name="RoleId"], [name="ResourceId"], [name="ActionCode"]').prop('disabled', true);
          }
        },
        error: function () {
          $form.find('.crud-form-error').removeClass('d-none').text(icpMsg('saveFailedRetry'));
        }
      });
    }

    function confirmAction(message, actionUrl, id) {
      $('#crudConfirmMessage').text(message);
      pendingBatchDisable = false;
      pendingConfirmAction = { url: actionUrl, id: id };
      if (confirmModalInstance) {
        confirmModalInstance.show();
      }
    }

    $(config.createBtnSelector || '#btnCreate').on('click', function () {
      openCreate();
    });

    $(document).on('click', config.editBtnSelector || '.btn-crud-edit', function () {
      var id = $(this).closest('tr').data('id');
      if (id) {
        openEdit(id);
      }
    });

    $(document).on('click', config.disableBtnSelector || '.btn-crud-disable', function () {
      var id = $(this).closest('tr').data('id');
      if (id) {
        confirmAction(config.disableConfirmMessage || icpMsg('disableConfirm'), config.disableUrl, id);
      }
    });

    $(document).on('click', config.deleteBtnSelector || '.btn-crud-delete', function () {
      var id = $(this).closest('tr').data('id');
      if (id) {
        confirmAction(config.deleteConfirmMessage || icpMsg('deleteConfirm'), config.deleteUrl, id);
      }
    });

    $('#btnSaveEdit').on('click', function () {
      saveForm();
    });

    $('#crudConfirmOk').on('click', function () {
      if (pendingBatchDisable) {
        var ids = Array.from(selectedListIds);
        pendingBatchDisable = false;

        $.ajax({
          url: config.batchDisableUrl,
          type: 'POST',
          contentType: 'application/json',
          data: JSON.stringify({ ids: ids }),
          success: function (result) {
            if (result.success) {
              if (confirmModalInstance) {
                confirmModalInstance.hide();
              }
              selectedListIds.clear();
              reloadTable();
            } else {
              alert(result.message || icpMsg('operationFailed'));
            }
          },
          error: function () {
            alert(icpMsg('operationFailedRetry'));
          }
        });
        return;
      }

      if (!pendingConfirmAction) {
        return;
      }

      $.post(pendingConfirmAction.url, { id: pendingConfirmAction.id })
        .done(function (result) {
          if (result.success) {
            if (confirmModalInstance) {
              confirmModalInstance.hide();
            }
            reloadTable();
          } else {
            alert(result.message || icpMsg('operationFailed'));
          }
        })
        .fail(function () {
          alert(icpMsg('operationFailedRetry'));
        })
        .always(function () {
          pendingConfirmAction = null;
        });
    });

    if (config.batchDisableUrl) {
      $(document).on('change', listContainerSelector + ' .row-list-cb', function () {
        var id = String($(this).data('id'));
        if ($(this).is(':checked')) {
          selectedListIds.add(id);
        } else {
          selectedListIds.delete(id);
        }
        syncListCheckboxes();
      });

      $(document).on('change', listContainerSelector + ' .list-select-all', function () {
        var checked = $(this).is(':checked');
        $(listContainerSelector).find('tbody tr[data-id]').each(function () {
          var $cb = $(this).find('.row-list-cb');
          if (!$cb.length) {
            return;
          }
          var id = String($(this).data('id'));
          $cb.prop('checked', checked);
          if (checked) {
            selectedListIds.add(id);
          } else {
            selectedListIds.delete(id);
          }
        });
      });

      $(config.batchDisableBtnSelector || '#btnBatchDisable').on('click', function () {
        if (selectedListIds.size === 0) {
          alert(icpMsg('selectAtLeastOneRecord'));
          return;
        }

        pendingBatchDisable = true;
        pendingConfirmAction = null;
        $('#crudConfirmMessage').text(config.batchDisableConfirmMessage || icpMsg('disableConfirm'));
        if (confirmModalInstance) {
          confirmModalInstance.show();
        }
      });

      $($(config.confirmModalSelector || '#crudConfirmModal')).on('hidden.bs.modal', function () {
        pendingBatchDisable = false;
      });
    }
  };
})(window, window.jQuery);
