(function () {
    'use strict';

    function getConfig() {
        return window.IcpPermissions || { superUser: false, allowedCodes: [] };
    }

    function isMenuCategoryCode(resourceCode) {
        var segments = (resourceCode || '').split('.');
        return segments.length === 4
            && segments[0].toLowerCase() === 'views'
            && segments[1].toLowerCase() === 'shared'
            && segments[2].toLowerCase() === '_sidebarnav';
    }

    function isAllowed(code) {
        if (!code) {
            return false;
        }

        var config = getConfig();
        if (config.superUser) {
            return true;
        }

        var normalized = code.toLowerCase();
        var allowedCodes = config.allowedCodes || [];
        for (var i = 0; i < allowedCodes.length; i++) {
            if ((allowedCodes[i] || '').toLowerCase() === normalized) {
                return true;
            }
        }

        return false;
    }

    function isInHiddenScanContainer(element) {
        return !!(element.closest('.d-none, [aria-hidden="true"]'));
    }

    function triggersPageDeniedBanner(element, resourceCode) {
        if (isInHiddenScanContainer(element)) {
            return false;
        }

        if (element.getAttribute('data-permission-scope') === 'page') {
            return true;
        }

        if (element.tagName.toLowerCase() !== 'div') {
            return false;
        }

        var segments = (resourceCode || '').split('.');
        if (segments.length >= 5
            && segments[0].toLowerCase() === 'views'
            && segments[1].toLowerCase() === 'shared'
            && segments[2].toLowerCase() === '_sidebarnav') {
            return true;
        }

        if (segments.length === 4
            && segments[0].toLowerCase() === 'views'
            && segments[segments.length - 1].toLowerCase() === 'view') {
            return true;
        }

        return false;
    }

    function showPageDeniedBanner() {
        var banner = document.getElementById('icp-page-access-denied');
        if (!banner) {
            return;
        }

        var message = (window.IcpI18n && window.IcpI18n.permissionAccessDenied) || '';
        if (message) {
            banner.textContent = message;
        }

        banner.classList.remove('d-none');
    }

    function applyPermissions() {
        var pageDeniedShown = false;
        var elements = document.querySelectorAll('[data-permissions]');

        elements.forEach(function (element) {
            var code = element.getAttribute('data-permissions');
            if (!code || isAllowed(code)) {
                return;
            }

            element.hidden = true;

            if (triggersPageDeniedBanner(element, code) && !pageDeniedShown) {
                showPageDeniedBanner();
                pageDeniedShown = true;
            }
        });

        hideOrphanMenuCategoryHeadings();
    }

    function hideOrphanMenuCategoryHeadings() {
        document.querySelectorAll('.sb-sidenav-menu-heading[data-permissions]').forEach(function (heading) {
            var code = heading.getAttribute('data-permissions');
            if (!code || !isMenuCategoryCode(code) || !isAllowed(code)) {
                heading.hidden = true;
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', applyPermissions);
    } else {
        applyPermissions();
    }
})();
