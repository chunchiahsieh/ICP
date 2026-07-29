(function () {
    'use strict';

    function getMessages() {
        var i18n = window.IcpI18n || {};
        return {
            success: i18n.refreshSessionPermissionsSuccess || 'Permissions reloaded.',
            failed: i18n.refreshSessionPermissionsFailed || 'Failed to reload permissions.'
        };
    }

    function refreshSessionPermissions(refreshUrl) {
        var messages = getMessages();

        return fetch(refreshUrl, {
            method: 'POST',
            headers: {
                Accept: 'application/json'
            }
        })
            .then(function (response) {
                return response.json()
                    .catch(function () { return null; })
                    .then(function (data) {
                        if (!response.ok || !data || !data.success) {
                            throw new Error('refresh failed');
                        }

                        return data;
                    });
            })
            .then(function () {
                alert(messages.success);
                window.location.reload();
            })
            .catch(function () {
                alert(messages.failed);
            });
    }

    function bindRefreshButtons(refreshUrl) {
        document.querySelectorAll('[data-icp-refresh-permissions]').forEach(function (button) {
            button.addEventListener('click', function (event) {
                event.preventDefault();

                if (button.disabled) {
                    return;
                }

                button.disabled = true;
                refreshSessionPermissions(refreshUrl).finally(function () {
                    button.disabled = false;
                });
            });
        });
    }

    function init() {
        var refreshUrl = window.IcpRefreshPermissionsUrl;
        if (!refreshUrl) {
            return;
        }

        bindRefreshButtons(refreshUrl);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
