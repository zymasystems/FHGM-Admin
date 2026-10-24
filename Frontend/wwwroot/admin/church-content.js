(() => {
  'use strict';
  const root = document.getElementById('content-manager'), kind = root.dataset.contentKind, form = document.getElementById('content-form');
  const el = id => document.getElementById(id);
  let selected = null, page = 1, items = [], loading = false;
  const noun = kind === 'events' ? 'event' : 'announcement';
  const localInput = value => { const d = new Date(value); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16); };
  function reset() { selected = null; form.reset(); el('content-form-title').textContent = `New ${noun}`; }
  function state(item) { if (!item.isPublished) return 'Draft'; if (new Date(item.endsAt) <= new Date()) return 'Expired'; if (new Date(item.startsAt) > new Date()) return kind === 'events' ? 'Upcoming' : 'Scheduled'; return kind === 'events' ? 'Ongoing' : 'Active'; }
  function edit(item) {
    selected = item; form.reset();
    for (const control of form.elements) {
      if (!control.name) continue;
      if (control.type === 'checkbox') control.checked = item[control.name];
      else control.value = control.type === 'datetime-local' ? localInput(item[control.name]) : item[control.name] ?? '';
    }
    el('content-form-title').textContent = `Edit ${noun}`; form.elements.title.focus(); form.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
  async function load() {
    if (loading) return; loading = true;
    try {
      const data = await api(kind + '?page=' + page); items = data.items; el('content-rows').replaceChildren(); el('content-empty').hidden = items.length !== 0;
      for (const item of items) {
        const row = document.createElement('tr'); cell(row, item.title); cell(row, state(item)); cell(row, date(item.startsAt)); cell(row, date(item.endsAt));
        const actions = cell(row, ''); actions.append(button('Edit', async () => edit(await api(`${kind}/${item.id}`))), document.createTextNode(' '), button('Delete', async () => {
          if (!confirm(`Delete this ${noun}? This removes it from the website and dashboard.`)) return;
          await api(`${kind}/${item.id}?version=${item.version}`, 'DELETE'); if (selected?.id === item.id) reset(); await load(); notice(`${noun[0].toUpperCase() + noun.slice(1)} deleted.`);
        })); el('content-rows').append(row);
      }
      el('content-page').textContent = `Page ${page} · ${data.total} records`; el('content-prev').disabled = page <= 1; el('content-next').disabled = page * 25 >= data.total;
    } finally { loading = false; }
  }
  el('content-time-zone').textContent = `Times use your device time zone: ${Intl.DateTimeFormat().resolvedOptions().timeZone}. Public expiry is checked automatically against the saved end time.`;
  form.addEventListener('submit', event => { event.preventDefault(); perform(event.submitter, async () => {
    const body = Object.fromEntries(new FormData(form)); body.isPublished = form.elements.isPublished.checked;
    body.startsAt = new Date(body.startsAt).toISOString(); body.endsAt = new Date(body.endsAt).toISOString(); body.version = selected?.version || 1;
    if (body.endsAt <= body.startsAt) throw new Error('Choose an end date/time after the start date/time.');
    if (kind === 'popups') body.priority = Number(body.priority);
    const saved = await api(selected ? `${kind}/${selected.id}` : kind, selected ? 'PUT' : 'POST', body); selected = saved; edit(saved); await load(); notice(`${noun[0].toUpperCase() + noun.slice(1)} saved.`);
  }); });
  el('content-new').addEventListener('click', reset);
  el('content-refresh').addEventListener('click', () => perform(el('content-refresh'), load));
  for (const [id, step] of [['content-prev', -1], ['content-next', 1]]) el(id).addEventListener('click', () => perform(el(id), async () => { page += step; await load(); }));
  load().catch(error => notice(error.message, true));
})();
