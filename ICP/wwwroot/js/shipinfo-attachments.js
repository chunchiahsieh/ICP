(function (global, $) {
    'use strict';
    var app = global.ShipInfoApp;
    if (!app || !$) return;
    var adapter = null;
    app.disposeHeaderAttachments = function () {
        if (adapter) adapter.destroy();
        adapter = null;
    };
    app.renderHeaderAttachments = function (headerRowKey, editable, host) {
        app.disposeHeaderAttachments();
        var $host = $(host);
        if (!headerRowKey || !$host.length || !global.ShipInfoFileUploaderAdapter) {
            return;
        }
        adapter = global.ShipInfoFileUploaderAdapter({
            host: $host,
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
