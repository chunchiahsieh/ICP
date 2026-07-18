(function ($) {
    'use strict';

    function hasPermission(code) {
        var config = window.IcpPermissions || { superUser: false, allowedCodes: [] };
        if (config.superUser) {
            return true;
        }

        var normalized = (code || '').toLowerCase();
        return (config.allowedCodes || []).some(function (allowed) {
            return (allowed || '').toLowerCase() === normalized;
        });
    }

    function cancelPendingQuietly(cancelUrl, filePath) {
        if (!cancelUrl || !filePath) {
            return $.Deferred().resolve().promise();
        }

        return $.ajax({
            url: cancelUrl,
            type: 'POST',
            data: { filePath: filePath },
            dataType: 'json'
        });
    }

    function initialize($page) {
        var permission = $page.data('permission');
        if (!hasPermission(permission) || typeof window.createUploader !== 'function') {
            return;
        }

        var pendingFilePath = '';
        var cancelUrl = $page.data('cancel-url');
        var previewTitleText = $page.data('preview-title');
        var $save = $page.find('.mass-update-save');
        var $result = $page.find('.mass-update-result');
        var $title = $page.find('.mass-update-result-title');
        var $message = $page.find('.mass-update-message');
        var $data = $page.find('.mass-update-data');

        function bindStatusFilter() {
            var $preview = $data.find('.mass-update-preview');
            var $filter = $preview.find('.mass-update-status-filter');
            if (!$filter.length) {
                return;
            }

            $filter.off('change.massUpdateStatus').on('change.massUpdateStatus', function () {
                var selected = String($(this).val() || '');
                $preview.find('tbody tr[data-status]').each(function () {
                    var status = String($(this).attr('data-status') || '');
                    $(this).toggle(!selected || status === selected);
                });
            });
        }

        function bindPreviewSort() {
            if (window.UploadPreviewSort && typeof window.UploadPreviewSort.bind === 'function') {
                window.UploadPreviewSort.bind($data);
            }
        }

        function loadPreview(filePath, canSave) {
            $.post($page.data('query-url'), { filePath: filePath })
                .done(function (html) {
                    $data.html(html);
                    bindStatusFilter();
                    bindPreviewSort();
                    var previewCanSave = $data.find('[data-can-save]').attr('data-can-save') === 'true';
                    $save.prop('disabled', !(canSave && previewCanSave));
                    if (!previewCanSave) {
                        $message.removeClass('d-none alert-success alert-info')
                            .addClass('alert-warning')
                            .text($page.data('cannot-save'));
                    }
                })
                .fail(function (xhr) {
                    $data.html('<div class="alert alert-danger m-0">' +
                        $('<div>').text(xhr.responseText || $page.data('save-failed')).html() +
                        '</div>');
                    $save.prop('disabled', true);
                });
        }

        function showUploadResult(response) {
            if (!response || !response.success) {
                return;
            }

            var nextPath = response.filePath || response.FilePath || '';
            var previousPath = pendingFilePath;
            var canSave = response.canSave === true || response.CanSave === true;

            function activate() {
                pendingFilePath = nextPath;
                $title.removeClass('text-success').text(previewTitleText);
                $message.removeClass('d-none alert-success')
                    .toggleClass('alert-info', canSave)
                    .toggleClass('alert-warning', !canSave)
                    .text(response.message || '');
                $result.removeClass('d-none');
                $save.prop('disabled', !canSave);
                loadPreview(pendingFilePath, canSave);
            }

            if (previousPath && previousPath !== nextPath) {
                cancelPendingQuietly(cancelUrl, previousPath).always(activate);
            } else {
                activate();
            }
        }

        function clearPendingUi(message, isSuccess) {
            pendingFilePath = '';
            $data.empty();
            $title.removeClass('text-success').text(previewTitleText);
            $message
                .removeClass('d-none alert-info alert-warning alert-success')
                .addClass(isSuccess ? 'alert-success' : 'alert-info')
                .text(message || '');
            $save.prop('disabled', true);
        }

        $save.on('click', function () {
            if (!pendingFilePath) {
                alert($page.data('please-upload'));
                return;
            }

            $save.prop('disabled', true);
            $.post($page.data('save-url'), { filePath: pendingFilePath })
                .done(function (response) {
                    if (response && response.success) {
                        clearPendingUi(response.message || '', true);
                        $result.removeClass('d-none');
                        return;
                    }

                    $save.prop('disabled', false);
                    alert(response && response.message ? response.message : $page.data('save-failed'));
                })
                .fail(function (xhr) {
                    $save.prop('disabled', false);
                    alert((xhr.responseJSON && xhr.responseJSON.message) || $page.data('save-failed'));
                });
        });

        window.createUploader('#' + $page.data('uploader-id'), {
            title: $page.data('uploader-title'),
            buttonText: $page.data('uploader-button'),
            buttonClass: 'btn btn-primary align-self-center d-inline-flex align-items-center gap-2',
            uploadUrl: $page.data('upload-url'),
            fileTypes: '.xlsx,.xls,.csv',
            multiple: false,
            maxSize: Number($page.data('max-size')),
            maxSizeHint: $page.data('max-size-hint'),
            fieldName: 'file',
            onSuccess: showUploadResult,
            onRemove: function (filePath) {
                if (!filePath) {
                    return;
                }

                cancelPendingQuietly(cancelUrl, filePath).always(function () {
                    if (pendingFilePath === filePath) {
                        clearPendingUi('', false);
                        $message.addClass('d-none');
                        $result.addClass('d-none');
                    }
                });
            }
        });
    }

    $(function () {
        $('.mass-update-page').each(function () {
            initialize($(this));
        });
    });
})(jQuery);
