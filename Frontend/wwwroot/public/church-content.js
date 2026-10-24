(() => {
  'use strict';
  const script = document.currentScript, popupApi = script?.dataset.popupsApi || '/api/public/popups/active';
  for (const root of document.querySelectorAll('[data-fhgm-events]')) {
    const base = root.dataset.api || '/api/public/events'; let page = 1, offset = 0, busy = false;
    const status = document.createElement('p'); status.setAttribute('role', 'status');
    const grid = document.createElement('div'); grid.className = 'fhgm-events-grid';
    const nav = document.createElement('div'); nav.className = 'fhgm-events-nav'; const prev = document.createElement('button'), next = document.createElement('button'), count = document.createElement('span');
    prev.type = next.type = 'button'; prev.className = next.className = 'fhgm-content-button'; prev.textContent = 'Previous'; next.textContent = 'Next'; nav.append(prev, count, next); root.append(status, grid, nav);
    async function load() {
      if (busy) return; busy = true; prev.disabled = next.disabled = true;
      try {
        const response = await fetch(base + '?page=' + page, { credentials: 'omit', cache: 'no-store' });
        if (!response.ok) throw new Error('Events are temporarily unavailable. Please try again later.');
        const data = await response.json(); offset = new Date(data.serverTime).getTime() - Date.now(); grid.replaceChildren();
        for (const item of data.items) {
          const card = document.createElement('article'); card.className = 'fhgm-event-card'; card.dataset.endsAt = item.endsAt;
          const title = document.createElement('h2'), time = document.createElement('p'), location = document.createElement('p'), description = document.createElement('p');
          title.textContent = item.title; time.className = 'fhgm-event-time'; time.textContent = new Date(item.startsAt).toLocaleString() + ' – ' + new Date(item.endsAt).toLocaleString(); location.textContent = item.location; description.textContent = item.description;
          card.append(title, time, location, description); grid.append(card);
        }
        status.textContent = data.items.length ? '' : 'There are no upcoming events to show right now.'; count.textContent = 'Page ' + page; prev.disabled = page <= 1; next.disabled = page * 12 >= data.total;
      } catch (error) { status.textContent = error.message; }
      finally { busy = false; }
    }
    prev.addEventListener('click', () => { page--; load(); }); next.addEventListener('click', () => { page++; load(); });
    setInterval(() => { for (const card of Array.from(grid.children)) if (new Date(card.dataset.endsAt).getTime() <= Date.now() + offset) card.remove(); if (!grid.childElementCount && !busy) status.textContent = 'There are no upcoming events to show right now.'; }, 1000);
    setInterval(load, 60000); load();
  }
  const memory = new Set(); let dialog = null, visible = null, offset = 0, previousFocus = null;
  const key = item => 'fhgm-popup-' + item.id + '-' + item.version;
  function dismissed(item) { try { return memory.has(key(item)) || sessionStorage.getItem(key(item)) === 'dismissed'; } catch { return memory.has(key(item)); } }
  function dismiss() { if (visible) { memory.add(key(visible)); try { sessionStorage.setItem(key(visible), 'dismissed'); } catch {} } dialog?.close(); previousFocus?.focus?.(); }
  async function refreshPopups() {
    try {
      const response = await fetch(popupApi, { credentials: 'omit', cache: 'no-store' }); if (!response.ok) return;
      const data = await response.json(); offset = new Date(data.serverTime).getTime() - Date.now();
      if (dialog?.open) {
        if (!data.items.some(item => item.id === visible.id && item.version === visible.version)) dialog.close();
        else return;
      }
      const item = data.items.find(item => !dismissed(item)); if (!item) return;
      if (!dialog) {
        dialog = document.createElement('dialog'); dialog.className = 'fhgm-popup'; dialog.setAttribute('aria-labelledby', 'fhgm-popup-title');
        const close = document.createElement('button'); close.className = 'fhgm-content-button fhgm-popup-close'; close.type = 'button'; close.textContent = 'Close'; close.addEventListener('click', dismiss);
        const title = document.createElement('h2'); title.id = 'fhgm-popup-title'; const message = document.createElement('p'); message.id = 'fhgm-popup-message'; const link = document.createElement('a'); link.id = 'fhgm-popup-link';
        dialog.setAttribute('aria-describedby', message.id); dialog.append(close, title, message, link); dialog.addEventListener('cancel', event => { event.preventDefault(); dismiss(); }); document.body.append(dialog);
      }
      visible = item; dialog.querySelector('h2').textContent = item.title; dialog.querySelector('p').textContent = item.message;
      const link = dialog.querySelector('a'); link.hidden = !item.buttonUrl; link.textContent = item.buttonText; if (item.buttonUrl) link.href = item.buttonUrl;
      previousFocus = document.activeElement; dialog.showModal();
    } catch { /* Public content remains usable if announcements cannot be loaded. */ }
  }
  setInterval(() => { if (dialog?.open && new Date(visible.endsAt).getTime() <= Date.now() + offset) { dialog.close(); previousFocus?.focus?.(); } }, 1000);
  setInterval(refreshPopups, 60000); refreshPopups();
})();
