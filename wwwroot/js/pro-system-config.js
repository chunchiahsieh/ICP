(function (global, $) {
  'use strict';

  global.ProSystemConfig = global.ProSystemConfig || {};

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
      data[item.name] = item.value;
    });
    return data;
  }

  function bindModel($form, model, category) {
    model = model || {};
    $form.find('[name="Id"]').val(model.id || model.Id || '');
    $form.find('[name="Category"]').val(model.category || model.Category || category || '');
    $form.find('[name="Key1"]').val(model.key1 || model.Key1 || '');
    $form.find('[name="Value1"]').val(model.value1 || model.Value1 || '');
  }

  global.ProSystemConfig.init = function (config) {
    config = config || {};

    var $form = $(config.formSelector || '#editForm');
    var editModalInstance = getModalInstance(config.editModalSelector || '#editModal');
    var confirmModalInstance = getModalInstance(config.confirmModalSelector || '#crudConfirmModal');
    var pendingConfirmAction = null;
    var pendingBatchDelete = false;
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

    function openCreate() {
      bindModel($form, {}, config.category);
      openEditModal(config.createTitle || icpMsg('create'));
    }

    function openEdit(id) {
      $.get(config.getUrl, { id: id }, function (model) {
        bindModel($form, model, config.category);
        openEditModal(config.editTitle || icpMsg('edit'));
      }).fail(function () {
        alert(icpMsg('loadFailed'));
      });
    }

    function saveForm() {
      clearFormErrors($form);
      var data = serializeForm($form);
      if (!data.Id) {
        delete data.Id;
      } else {
        data.Id = parseInt(data.Id, 10);
      }
      if (!data.Category) {
        data.Category = config.category;
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
        },
        error: function () {
          $form.find('.crud-form-error').removeClass('d-none').text(icpMsg('saveFailedRetry'));
        }
      });
    }

    function confirmAction(message, actionUrl, id) {
      $('#crudConfirmMessage').text(message);
      pendingBatchDelete = false;
      pendingConfirmAction = { url: actionUrl, id: id };
      if (confirmModalInstance) {
        confirmModalInstance.show();
      }
    }

    $(config.createBtnSelector || '#btnCreate').on('click', openCreate);

    $(document).on('click', config.editBtnSelector || '.btn-crud-edit', function () {
      var id = $(this).closest('tr').data('id');
      if (id) {
        openEdit(id);
      }
    });

    if (config.deleteUrl) {
      $(document).on('click', config.deleteBtnSelector || '.btn-delete', function () {
        var id = $(this).closest('tr').data('id');
        if (id) {
          confirmAction(config.deleteConfirmMessage || icpMsg('deleteConfirm'), config.deleteUrl, id);
        }
      });
    }

    $('#btnSaveEdit').on('click', saveForm);

    $('#crudConfirmOk').on('click', function () {
      if (pendingBatchDelete) {
        var ids = Array.from(selectedListIds).map(function (id) { return parseInt(id, 10); });
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

    if (config.batchDeleteUrl) {
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

      $(config.batchDeleteBtnSelector || '#btnBatchDelete').on('click', function () {
        if (selectedListIds.size === 0) {
          alert(icpMsg('selectAtLeastOneRecord'));
          return;
        }

        pendingBatchDelete = true;
        pendingConfirmAction = null;
        $('#crudConfirmMessage').text(config.batchDeleteConfirmMessage || icpMsg('deleteConfirm'));
        if (confirmModalInstance) {
          confirmModalInstance.show();
        }
      });

      $($(config.confirmModalSelector || '#crudConfirmModal')).on('hidden.bs.modal', function () {
        pendingBatchDelete = false;
      });
    }
  };
})(window, window.jQuery);
