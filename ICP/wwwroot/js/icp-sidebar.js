(function () {
    'use strict';
    var stateKey = 'icp.sidebar.collapses';
    var recentKey = 'icp.sidebar.recent';
    var pinnedKey = 'icp.sidebar.pinned';
    var widthKey = 'icp.sidebar.width';
    var minimumWidth = 200;
    var maximumWidth = 420;
    var selectedRoot = null;
    var searching = false;
    function recentLimit() {
        var group = document.getElementById('sidebarRecentGroup');
        var value = group ? Number(group.dataset.recentLimit) : 3;
        return Number.isInteger(value) && value >= 0 ? Math.min(value, 50) : 3;
    }
    function recentEnabled() { return !!document.getElementById('sidebarRecentGroup'); }
    function pinnedLimit() {
        var group = document.getElementById('sidebarPinnedGroup');
        var value = group ? Number(group.dataset.pinnedLimit) : 5;
        return Number.isInteger(value) && value >= 0 ? Math.min(value, 20) : 5;
    }

    function allowed(code) {
        if (!code) return true;
        var config = window.IcpPermissions || {};
        return config.superUser || (config.allowedCodes || []).some(function (value) { return value.toLowerCase() === code.toLowerCase(); });
    }
    function savedState() { try { return JSON.parse(localStorage.getItem(stateKey) || '{}'); } catch (_) { return {}; } }
    function saveState(state) { try { localStorage.setItem(stateKey, JSON.stringify(state)); } catch (_) { /* Storage may be disabled. */ } }
    function savedPins() { try { var items = JSON.parse(localStorage.getItem(pinnedKey) || '[]'); return Array.isArray(items) ? items.filter(function (item) { return typeof item === 'string'; }) : []; } catch (_) { return []; } }
    function savePins(items) { try { localStorage.setItem(pinnedKey, JSON.stringify(items.slice(0, pinnedLimit()))); } catch (_) { /* Storage may be disabled. */ } }
    function rootCollapses() {
        return Array.prototype.slice.call(document.querySelectorAll('#sidenavAccordion [data-sidebar-menu-group] > .collapse[id]'));
    }
    function showOnlyRoot(target) {
        rootCollapses().forEach(function (element) {
            bootstrap.Collapse.getOrCreateInstance(element, { toggle: false })[element === target ? 'show' : 'hide']();
        });
    }
    function restoreCollapses() {
        var state = savedState();
        var roots = rootCollapses();
        var activeRoot = roots.find(function (element) { return element.querySelector('.nav-link.active'); });
        var rememberedRoot = roots.find(function (element) { return element.id === state.lastRoot; });
        selectedRoot = activeRoot || rememberedRoot || null;
        roots.forEach(function (element) {
            element.addEventListener('show.bs.collapse', function (event) {
                if (event.target !== element || searching) return;
                selectedRoot = element;
                saveState({ lastRoot: element.id });
                roots.filter(function (other) { return other !== element; }).forEach(function (other) {
                    bootstrap.Collapse.getOrCreateInstance(other, { toggle: false }).hide();
                });
            });
            element.addEventListener('hide.bs.collapse', function (event) {
                if (event.target !== element || searching || selectedRoot !== element) return;
                selectedRoot = null;
                saveState({ lastRoot: null });
            });
            // A delayed animation completion must not override the last requested root.
            ['shown.bs.collapse', 'hidden.bs.collapse'].forEach(function (name) {
                element.addEventListener(name, function (event) {
                    if (event.target !== element || searching) return;
                    showOnlyRoot(selectedRoot);
                });
            });
        });
        showOnlyRoot(selectedRoot);
    }
    function recent() { try { var items = JSON.parse(localStorage.getItem(recentKey) || '[]'); return Array.isArray(items) ? items.filter(function (item) { return item && typeof item.href === 'string'; }) : []; } catch (_) { return []; } }
    function sourceLinks() {
        return Array.prototype.slice.call(document.querySelectorAll('#sidenavAccordion .sidebar-menu-group a.nav-link[href]'))
            .filter(function (link) { return !link.closest('#sidebarRecentGroup, #sidebarPinnedGroup, [hidden]') && !link.dataset.sidebarGenerated && allowed(link.dataset.permissions); });
    }
    function linkLabel(link) { return link.dataset.sidebarSearchLabel || link.dataset.sidebarLabel || link.textContent.trim(); }
    function makePinButton(link, pinned) {
        var group = document.getElementById('sidebarPinnedGroup');
        var label = group ? (pinned ? group.dataset.unpinLabel : group.dataset.pinLabel) : '';
        var button = document.createElement('button');
        button.type = 'button'; button.className = 'sidebar-pin-button'; button.title = label; button.setAttribute('aria-label', label);
        button.innerHTML = '<i class="fa-solid ' + (pinned ? 'fa-thumbtack' : 'fa-thumbtack') + '" aria-hidden="true"></i>';
        button.addEventListener('click', function (event) {
            event.preventDefault(); event.stopPropagation();
            var items = savedPins();
            var index = items.indexOf(link.href);
            if (index >= 0) items.splice(index, 1);
            else if (pinnedLimit() > 0) { items.unshift(link.href); items = items.slice(0, pinnedLimit()); }
            savePins(items); renderPinned(); decorateSourceLinks();
        });
        return button;
    }
    function decorateSourceLinks() {
        var pins = savedPins();
        sourceLinks().forEach(function (link) {
            var row = link.closest('.sidebar-link-row');
            if (!row) {
                row = document.createElement('div'); row.className = 'sidebar-link-row';
                link.parentNode.insertBefore(row, link); row.appendChild(link);
            }
            var oldButton = row.querySelector('.sidebar-pin-button');
            if (oldButton) oldButton.remove();
            row.appendChild(makePinButton(link, pins.indexOf(link.href) >= 0));
        });
    }
    function copyMenuLink(source) {
        var link = source.cloneNode(true);
        link.classList.remove('active'); link.removeAttribute('aria-current'); link.dataset.sidebarGenerated = 'true';
        return link;
    }
    function renderPinned() {
        var target = document.getElementById('sidebarPinnedLinks');
        var group = document.getElementById('sidebarPinnedGroup');
        if (!target || !group) return;
        target.replaceChildren();
        var sources = sourceLinks();
        savedPins().map(function (href) { return sources.find(function (link) { return link.href === href; }); }).filter(Boolean).forEach(function (source) {
            var row = document.createElement('div'); row.className = 'sidebar-link-row';
            row.appendChild(copyMenuLink(source)); row.appendChild(makePinButton(source, true)); target.appendChild(row);
        });
        group.hidden = target.children.length === 0;
    }
    function renderRecent() {
        if (!recentEnabled()) return;
        var target = document.getElementById('sidebarRecentLinks');
        var group = document.getElementById('sidebarRecentGroup');
        if (!target || !group) return;
        target.replaceChildren();
        var currentLinks = sourceLinks();
        recent().map(function (item) {
            return currentLinks.find(function (link) { return link.href === item.href; });
        }).filter(Boolean).slice(0, recentLimit()).forEach(function (source) {
            var link = copyMenuLink(source); link.title = linkLabel(source); target.appendChild(link);
        });
        group.hidden = target.children.length === 0;
    }
    function clearRecent() {
        try { localStorage.removeItem(recentKey); } catch (_) { /* Storage may be disabled. */ }
        renderRecent();
    }
    function rememberActivePage() {
        if (!recentEnabled()) return;
        var link = document.querySelector('#sidenavAccordion .nav-link.active[href]');
        if (!link) return;
        var item = { href: link.href, label: linkLabel(link), permission: link.dataset.permissions || '' };
        var items = recent().filter(function (entry) { return entry.href !== item.href; }); items.unshift(item);
        try { localStorage.setItem(recentKey, JSON.stringify(items.slice(0, recentLimit()))); } catch (_) { /* Storage may be disabled. */ }
    }
    function setupSearch() {
        var input = document.getElementById('sidebarMenuSearch');
        if (!input) return;
        var emptyMessage = document.getElementById('sidebarSearchEmpty');
        var countMessage = document.getElementById('sidebarSearchCount');
        var recentGroup = document.getElementById('sidebarRecentGroup');
        var groups = Array.prototype.slice.call(document.querySelectorAll('[data-sidebar-menu-group]'))
            .filter(function (group) { return group.id !== 'sidebarRecentGroup' && group.id !== 'sidebarPinnedGroup' && !group.hidden; });
        var entries = groups.map(function (group) {
            return { group: group, links: Array.prototype.slice.call(group.querySelectorAll('a.nav-link')).filter(function (link) { return !link.hidden; }) };
        });
        function highlight(link, query) {
            var label = link.dataset.sidebarSearchLabel || link.dataset.sidebarLabel || link.textContent.trim();
            link.dataset.sidebarLabel = label;
            var originalHtml = link.dataset.sidebarOriginalHtml;
            if (originalHtml === undefined) { originalHtml = link.innerHTML; link.dataset.sidebarOriginalHtml = originalHtml; }
            link.innerHTML = originalHtml;
            if (!query) return;
            var normalized = label.toLocaleLowerCase();
            var start = normalized.indexOf(query);
            if (start < 0) return;
            var walker = document.createTreeWalker(link, NodeFilter.SHOW_TEXT);
            var remaining = start;
            var node;
            while ((node = walker.nextNode())) {
                if (remaining >= node.nodeValue.length) { remaining -= node.nodeValue.length; continue; }
                var match = document.createElement('mark');
                match.className = 'sidebar-search-match';
                match.textContent = node.nodeValue.slice(remaining, remaining + query.length);
                node.parentNode.replaceChild(match, node);
                match.before(document.createTextNode(node.nodeValue.slice(0, remaining)));
                match.after(document.createTextNode(node.nodeValue.slice(remaining + query.length)));
                break;
            }
        }
        input.addEventListener('input', function () {
            var query = input.value.trim().toLocaleLowerCase();
            searching = query.length > 0;
            var resultCount = 0;
            entries.forEach(function (entry) {
                var group = entry.group;
                var matches = entry.links.filter(function (link) {
                    var label = link.dataset.sidebarSearchLabel || link.dataset.sidebarLabel || link.textContent.trim();
                    link.dataset.sidebarLabel = label;
                    return label.toLocaleLowerCase().indexOf(query) >= 0;
                });
                resultCount += matches.length;
                group.hidden = query.length > 0 && matches.length === 0;
                entry.links.forEach(function (link) {
                    link.hidden = query.length > 0 && matches.indexOf(link) < 0;
                    var pin = link.closest('.sidebar-link-row') && link.closest('.sidebar-link-row').querySelector('.sidebar-pin-button');
                    if (pin) pin.hidden = link.hidden;
                    highlight(link, query);
                });
                group.querySelectorAll('.collapse').forEach(function (collapse) { if (query.length > 0 && matches.length) bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false }).show(); });
            });
            if (emptyMessage) emptyMessage.hidden = !query || resultCount > 0;
            if (countMessage) { countMessage.hidden = !query; countMessage.textContent = query ? countMessage.dataset.template.replace('{0}', resultCount) : ''; }
            if (recentGroup) recentGroup.hidden = !!query || recentGroup.querySelectorAll('a[href]').length === 0;
            var pinnedGroup = document.getElementById('sidebarPinnedGroup');
            if (pinnedGroup) pinnedGroup.hidden = !!query || pinnedGroup.querySelectorAll('a[href]').length === 0;
            if (!query) showOnlyRoot(selectedRoot);
        });
    }
    function setupRecentClear() {
        var button = document.getElementById('sidebarClearRecent');
        if (button) button.addEventListener('click', clearRecent);
    }
    function setSidebarWidth(value, persist) {
        var width = Math.max(minimumWidth, Math.min(maximumWidth, Math.round(value)));
        document.documentElement.style.setProperty('--icp-sidebar-width', width + 'px');
        if (persist) { try { localStorage.setItem(widthKey, String(width)); } catch (_) { /* Storage may be disabled. */ } }
    }
    function setupResize() {
        var handle = document.getElementById('sidebarResizeHandle');
        if (!handle) return;
        var saved;
        try { saved = Number(localStorage.getItem(widthKey)); } catch (_) { saved = NaN; }
        if (Number.isFinite(saved)) setSidebarWidth(saved, false);
        function desktop() { return window.matchMedia('(min-width: 992px)').matches; }
        function resize(clientX, persist) { if (desktop()) setSidebarWidth(clientX, persist); }
        handle.addEventListener('pointerdown', function (event) {
            if (!desktop()) return;
            event.preventDefault(); handle.setPointerCapture(event.pointerId); document.body.classList.add('sidebar-resizing');
            function move(moveEvent) { resize(moveEvent.clientX, false); }
            function end(endEvent) { resize(endEvent.clientX, true); document.body.classList.remove('sidebar-resizing'); handle.removeEventListener('pointermove', move); handle.removeEventListener('pointerup', end); handle.removeEventListener('pointercancel', end); }
            handle.addEventListener('pointermove', move); handle.addEventListener('pointerup', end); handle.addEventListener('pointercancel', end);
        });
        handle.addEventListener('keydown', function (event) {
            if (!desktop()) return;
            var current = document.getElementById('layoutSidenav_nav').getBoundingClientRect().width;
            var step = event.shiftKey ? 25 : 10;
            if (event.key === 'ArrowLeft') { event.preventDefault(); setSidebarWidth(current - step, true); }
            else if (event.key === 'ArrowRight') { event.preventDefault(); setSidebarWidth(current + step, true); }
            else if (event.key === 'Home') { event.preventDefault(); setSidebarWidth(minimumWidth, true); }
            else if (event.key === 'End') { event.preventDefault(); setSidebarWidth(maximumWidth, true); }
        });
    }
    function revealActiveItem() {
        var menu = document.querySelector('#sidenavAccordion .sb-sidenav-menu');
        var active = menu && menu.querySelector('a.nav-link.active[aria-current="page"]');
        if (!active) return;
        function reveal() {
            if (searching || !active.getClientRects().length) return;
            var itemBounds = active.getBoundingClientRect();
            var menuBounds = menu.getBoundingClientRect();
            if (itemBounds.bottom > menuBounds.bottom - 12) menu.scrollTop += itemBounds.bottom - menuBounds.bottom + 12;
            else if (itemBounds.top < menuBounds.top + 12) menu.scrollTop += itemBounds.top - menuBounds.top - 12;
        }
        // Wait for initial layout and restoring collapse animations; scroll only the menu.
        requestAnimationFrame(reveal);
        window.addEventListener('load', reveal, { once: true });
        menu.querySelectorAll('.collapse, .collapsing').forEach(function (element) {
            element.addEventListener('shown.bs.collapse', function (event) {
                if (event.target === element && element.contains(active)) reveal();
            });
        });
    }
    window.addEventListener('DOMContentLoaded', function () { decorateSourceLinks(); rememberActivePage(); renderPinned(); renderRecent(); restoreCollapses(); setupSearch(); setupRecentClear(); setupResize(); revealActiveItem(); });
})();
