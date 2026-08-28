(function (global, $) {
    'use strict';
    if (!$ || !global.createUploader) return;

    function escapeHtml(value) { return $('<span>').text(value || '').html(); }

    global.ShipInfoFileUploaderAdapter = function (options) {
        var config = $.extend({ editable: false, onError: null }, options || {});
        var $host = $(config.host);
        var destroyed = false;
        var request = null;
        var uploader = null;

        function url(base, extra) {
            return base + '?headerKey=' + encodeURIComponent(config.headerRowKey) + (extra || '');
        }

        function fail(xhr) {
            if (destroyed) return;
            var message = (xhr && xhr.responseJSON && xhr.responseJSON.message) || '附件操作失敗';
            if (typeof config.onError === 'function') config.onError(message);
        }

        function renderList(items) {
            if (destroyed) return;
            var $list = $host.find('[data-attachment-list]').empty();
            if (!items.length) {
                $list.append('<div class="text-muted small">尚無附件</div>');
                return;
            }
            items.forEach(function (item) {
                var $row = $('<div class="list-group-item d-flex justify-content-between align-items-center gap-2"></div>');
                $row.append('<span class="text-truncate">' + escapeHtml(item.originalFileName) + '</span>');
                var $actions = $('<span class="btn-group btn-group-sm"></span>');
                $('<a class="btn btn-outline-secondary">下載</a>')
                    .attr('href', url(config.urls.download, '&id=' + encodeURIComponent(item.id)))
                    .appendTo($actions);
                if (config.editable) {
                    $('<button type="button" class="btn btn-outline-danger">刪除</button>').on('click', function () {
                        $.post(config.urls.delete, { headerKey: config.headerRowKey, id: item.id })
                            .done(load).fail(fail);
                    }).appendTo($actions);
                }
                $row.append($actions);
                $list.append($row);
            });
        }

        function load() {
            if (destroyed) return;
            if (request && request.readyState !== 4) request.abort();
            request = $.getJSON(config.urls.list, { headerKey: config.headerRowKey })
                .done(function (response) {
                    if (destroyed) return;
                    if (!response || !response.success) { fail(); return; }
                    renderList(response.data || []);
                })
                .fail(function (xhr, status) {
                    if (!destroyed && status !== 'abort') fail(xhr);
                });
        }

        function mount() {
            if (destroyed) return;
            $host.empty().append('<div data-attachment-upload></div><div class="list-group mt-2" data-attachment-list></div>');
            if (config.editable) {
                uploader = global.createUploader($host.find('[data-attachment-upload]'), {
                    title: 'Ship Info 附件上傳',
                    buttonText: '上傳附件',
                    uploadUrl: url(config.urls.upload),
                    fileTypes: '.pdf,.doc,.docx,.xls,.xlsx,.csv,.txt,.zip,.png,.jpg,.jpeg',
                    multiple: true,
                    sequentialUpload: true,
                    maxSize: config.maxSizeMb || 50,
                    onSuccess: load,
                    onError: function (message) { if (!destroyed && typeof config.onError === 'function') config.onError(message); }
                });
            }
            load();
        }

        mount();
        return {
            reload: load,
            destroy: function () {
                if (destroyed) return;
                destroyed = true;
                if (request && request.readyState !== 4) request.abort();
                if (uploader && typeof uploader.destroy === 'function') uploader.destroy();
                $host.empty();
            }
        };
    };
})(window, window.jQuery);
