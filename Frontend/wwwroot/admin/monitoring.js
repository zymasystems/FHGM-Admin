(() => {
  'use strict';
  const el = id => document.getElementById(id);
  function card(parent, title, value) { const div = document.createElement('div'), label = document.createElement('span'), strong = document.createElement('strong'); label.className = 'muted'; label.textContent = title; strong.className = 'summary-text'; strong.textContent = value; div.append(label, strong); parent.append(div); }
  async function refresh() {
    const data = await api('monitoring'); const health = el('monitor-health'), counts = el('monitor-counts'), details = el('monitor-process'); health.replaceChildren(); counts.replaceChildren(); details.replaceChildren();
    card(health, 'Backend status', data.status);
    for (const service of data.services) card(health, service.name, service.status + ' · ' + service.durationMs + ' ms');
    card(health, 'R2 gallery storage', data.r2Configured ? 'Configured (availability checked during uploads)' : 'Not configured');
    const frontend = await fetch('/health/ready', { credentials: 'same-origin' }); card(health, 'Frontend dependency health', frontend.ok ? 'Healthy' : 'Unavailable');
    for (const [key, value] of Object.entries(data.counts || {})) card(counts, label(key), String(value));
    for (const [title, value] of [['Runtime', data.runtime], ['Uptime', Math.floor(data.uptimeSeconds / 60) + ' minutes'], ['Process memory', data.workingSetMb + ' MB'], ['Managed memory', data.managedMemoryMb + ' MB']]) { const term = document.createElement('dt'), description = document.createElement('dd'); term.className = 'col-sm-5'; description.className = 'col-sm-7'; term.textContent = title; description.textContent = value; details.append(term, description); }
    el('monitor-checked').textContent = 'Last checked: ' + date(data.checkedAt);
  }
  el('monitor-refresh').addEventListener('click', () => perform(el('monitor-refresh'), refresh));
  refresh().catch(error => notice(error.message, true));
})();
