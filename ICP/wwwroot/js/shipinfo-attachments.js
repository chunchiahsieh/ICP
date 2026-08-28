(function (global, $) {
    'use strict';
    var app = global.ShipInfoApp;
    if (!app || !$) return;
    var adapter = null;
    app.disposeHeaderAttachments = function () {
        if (adapter) adapter.destroy();
        adapter = null;
    };
    app.renderHeaderAttachments = function (headerRowKey, editable) {
        app.disposeHeaderAttachments();
        var $section = $('#shipInfoHeaderAttachments');
        if (!headerRowKey || !$section.length || !global.ShipInfoFileUploaderAdapter) {
            $section.addClass('d-none');
            return;
        }
        $section.removeClass('d-none');
        adapter = global.ShipInfoFileUploaderAdapter({
            host: '#shipInfoHeaderAttachmentsBody',
            headerRowKey: headerRowKey,
            editable: !!editable,
            urls: {
                list: app.urls.getAttachments,
                upload: app.urls.uploadAttachment,
                download: app.urls.downloadAttachment,
                delete: app.urls.deleteAttachment
            },
            onError: function (message) { app.showToast(message, 'danger'); }
        });
    };
})(window, window.jQuery);
