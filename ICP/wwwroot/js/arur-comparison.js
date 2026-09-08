(function () {
  'use strict';
  window.ArurComparison = { init: function (config) {
    const form = document.getElementById('comparisonForm');
    const rows = document.getElementById('compareRows');
    const pageSelect = document.getElementById('comparePage');
    const size = document.getElementById('compareSize');
    const prev = document.getElementById('comparePrev');
    const next = document.getElementById('compareNext');
    let current = 1, pages = 1, pending;
    async function load(page) {
      if (pending) pending.abort();
      const controller = new AbortController();
      pending = controller;
      const error = document.getElementById('compareError');
      error.hidden = true;
      document.getElementById('compareTableContainer').setAttribute('aria-busy', 'true');
      rows.replaceChildren();
      prev.disabled = next.disabled = pageSelect.disabled = true;
      const data = new FormData(form);
      data.set('Page', page); data.set('PageSize', size.value);
      try {
        const response = await fetch(config.url, { method: 'POST', body: data, signal: controller.signal });
        if (!response.ok || response.redirected) {
          let message = config.error;
          try { message = (await response.json()).title || message; } catch (_) { }
          throw new Error(message);
        }
        const result = await response.json();
        current = result.page; pages = Math.max(1, Math.ceil(result.total / result.pageSize));
        for (const row of result.rows) {
          const tr = document.createElement('tr');
          for (const value of [row.number, row.resultLabel, row.rtNo, row.invoiceNo, row.fieldLabel, row.icpValue, row.ilcValue]) {
            const td = document.createElement('td'); td.textContent = value;
            td.style.whiteSpace = 'pre-wrap';
            if (row.result !== 'Equal') td.classList.add('text-danger');
            tr.append(td);
          }
          rows.append(tr);
        }
        if (!result.rows.length) {
          const tr = document.createElement('tr'), td = document.createElement('td');
          td.colSpan = 7; td.textContent = config.empty; tr.append(td); rows.append(tr);
        }
        const start = result.total ? (current - 1) * result.pageSize + 1 : 0;
        document.getElementById('compareSummary').textContent = start + '-' + Math.min(current * result.pageSize, result.total) + ' / ' + result.total;
        const options = document.createDocumentFragment();
        for (let i = 1; i <= pages; i++) options.append(new Option(String(i), String(i)));
        pageSelect.replaceChildren(options); pageSelect.value = current;
        document.getElementById('comparePages').textContent = '/ ' + pages;
        prev.disabled = current <= 1; next.disabled = current >= pages; pageSelect.disabled = pages <= 1;
      } catch (e) {
        if (e.name !== 'AbortError') {
          error.textContent = e.message || config.error; error.hidden = false;
          document.getElementById('compareSummary').textContent = '';
        }
      } finally {
        if (pending === controller) document.getElementById('compareTableContainer').setAttribute('aria-busy', 'false');
      }
    }
    document.getElementById('compareQuery').addEventListener('click', () => load(1));
    form.addEventListener('keydown', e => { if (e.key === 'Enter' && e.target.tagName === 'INPUT') { e.preventDefault(); load(1); } });
    size.addEventListener('change', () => load(1));
    pageSelect.addEventListener('change', () => load(Number(pageSelect.value)));
    prev.addEventListener('click', () => { if (current > 1) load(current - 1); });
    next.addEventListener('click', () => { if (current < pages) load(current + 1); });
    load(1);
  } };
}());
