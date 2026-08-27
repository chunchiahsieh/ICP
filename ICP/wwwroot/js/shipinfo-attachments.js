(function (global, $) {
    'use strict';
    var app = global.ShipInfoApp;
    if (!app || !$) return;
    function key() { return app.state.selectedHeaderRowKey; }
    function esc(v) { return $('<span>').text(v || '').html(); }
    function load() {
        var headerKey = key(); if (!headerKey) return;
        $.getJSON(app.urls.getAttachments, { headerKey: headerKey }).done(function (r) {
            var items = r && r.success ? r.data || [] : [];
            var $list = $('#shipInfoAttachmentsList').empty();
            if (!items.length) { $list.append('<div class="text-muted">尚無附件</div>'); return; }
            items.forEach(function (x) {
                var $row = $('<div class="list-group-item d-flex justify-content-between align-items-center gap-2"></div>');
                $row.append('<span class="text-truncate">' + esc(x.originalFileName) + '</span>');
                var $actions = $('<span class="btn-group btn-group-sm"></span>');
                $('<a class="btn btn-outline-secondary">下載</a>').attr('href', app.urls.downloadAttachment + '?headerKey=' + encodeURIComponent(headerKey) + '&id=' + encodeURIComponent(x.id)).appendTo($actions);
                $('<button type="button" class="btn btn-outline-danger">刪除</button>').on('click', function () { $.post(app.urls.deleteAttachment, { headerKey: headerKey, id: x.id }).done(load).fail(function (xhr) { app.showToast((xhr.responseJSON && xhr.responseJSON.message) || '刪除附件失敗', 'danger'); }); }).appendTo($actions);
                $row.append($actions); $list.append($row);
            });
        });
    }
    function open() {
        if (!key()) return;
        bootstrap.Modal.getOrCreateInstance(document.getElementById('shipInfoAttachmentsModal')).show();
        $('#shipInfoAttachmentUploader').empty();
        global.createUploader('#shipInfoAttachmentUploader', { title: 'Ship Info 附件上傳', buttonText: '上傳附件', uploadUrl: app.urls.uploadAttachment + '?headerKey=' + encodeURIComponent(key()), multiple: true, sequentialUpload: true, onSuccess: load, onError: function (m) { app.showToast(m, 'danger'); } });
        load();
    }
    $('#btnShipInfoAttachments').on('click', open);
    $(document).on('shipinfo:selectionChanged', function () { $('#btnShipInfoAttachments').prop('disabled', !key()); });
})(window, window.jQuery);
