(() => {
  'use strict';
  const svgNS = 'http://www.w3.org/2000/svg';
  function icon(name) { const svg = document.createElementNS(svgNS, 'svg'); svg.classList.add('ui-icon'); svg.setAttribute('aria-hidden','true'); const use = document.createElementNS(svgNS,'use'); use.setAttribute('href','/shared/ui-icons.svg#'+name); svg.append(use); return svg; }
  function decorateButton(button) { const label = button.textContent.trim(); if (/^(Delete|Remove|Disable)/i.test(label)) button.dataset.intent = 'danger'; button.title ||= label; }
  const confirmation = document.createElement('dialog'); confirmation.id = 'portal-confirm'; confirmation.className = 'portal-confirm'; confirmation.setAttribute('aria-labelledby','portal-confirm-title'); confirmation.setAttribute('aria-describedby','portal-confirm-message');
  const confirmTitle = document.createElement('h2'); confirmTitle.id = 'portal-confirm-title'; confirmTitle.textContent = 'Confirm action';
  const confirmMessage = document.createElement('p'); confirmMessage.id = 'portal-confirm-message';
  const confirmActions = document.createElement('div'); confirmActions.className = 'confirm-actions';
  const reject = document.createElement('button'); reject.type = 'button'; reject.className = 'btn outline'; reject.textContent = 'Cancel';
  const accept = document.createElement('button'); accept.type = 'button'; accept.className = 'btn primary'; accept.textContent = 'Confirm'; accept.dataset.confirmAccept = 'true';
  confirmActions.append(reject,accept); confirmation.append(confirmTitle,confirmMessage,confirmActions); document.body.append(confirmation);
  let finishConfirmation = null;
  function confirm(message) {
    if (finishConfirmation) return Promise.resolve(false);
    return new Promise(resolve => {
      confirmMessage.textContent = message; const destructive = /delete|remove|disable|archive|unpublish/i.test(message);
      accept.className = destructive ? 'btn btn-danger' : 'btn primary';
      finishConfirmation = result => { finishConfirmation = null; confirmation.close(); resolve(result); };
      confirmation.showModal(); reject.focus();
    });
  }
  reject.addEventListener('click', () => finishConfirmation?.(false)); accept.addEventListener('click', () => finishConfirmation?.(true));
  confirmation.addEventListener('cancel', event => { event.preventDefault(); finishConfirmation?.(false); });
  window.PortalUI = {icon,decorateButton,confirm};
  const nav = document.getElementById('portal-navigation'), toggle = document.getElementById('portal-nav-toggle'), close = document.getElementById('portal-nav-close'), backdrop = document.getElementById('portal-nav-backdrop');
  const small = matchMedia('(max-width: 1024px)');
  function setOpen(open, returnFocus = true) {
    document.body.classList.toggle('navigation-open',open); toggle?.setAttribute('aria-expanded',String(open));
    if (backdrop) backdrop.hidden = !open; if (nav) nav.inert = small.matches && !open;
    if (open) (close || nav?.querySelector('a'))?.focus(); else if (returnFocus) toggle?.focus();
  }
  toggle?.addEventListener('click', () => setOpen(!document.body.classList.contains('navigation-open')));
  close?.addEventListener('click', () => setOpen(false)); backdrop?.addEventListener('click', () => setOpen(false));
  small.addEventListener('change', () => setOpen(false,false));
  if (nav) nav.inert = small.matches;
  document.addEventListener('keydown', event => {
    if (!document.body.classList.contains('navigation-open')) return;
    if (event.key === 'Escape') { event.preventDefault(); setOpen(false); }
    if (event.key === 'Tab' && small.matches && nav) {
      const nodes = [...nav.querySelectorAll('a,button')].filter(el => !el.disabled && el.getClientRects().length);
      const first = nodes[0], last = nodes.at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    }
  });
  document.querySelectorAll('.nav-item.selected').forEach(link => link.setAttribute('aria-current','page'));
  document.querySelectorAll('button').forEach(decorateButton);
  document.querySelectorAll('input[required],textarea[required],select[required]').forEach(input => {
    const label = input.closest('label'); if (!label) return;
    const text = [...label.childNodes].find(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim());
    if (text) { const wrapper = document.createElement('span'); wrapper.className = 'field-label'; wrapper.textContent = text.textContent; const star = document.createElement('span'); star.className = 'required-mark'; star.textContent = ' *'; star.setAttribute('aria-hidden','true'); wrapper.append(star); text.replaceWith(wrapper); }
  });
  document.querySelectorAll('form').forEach(form => form.addEventListener('invalid', () => form.classList.add('was-validated'),true));
  document.querySelectorAll('.table-wrap').forEach((wrap,i) => { wrap.tabIndex = 0; wrap.setAttribute('role','region'); wrap.setAttribute('aria-label', (document.querySelector('h1')?.textContent || 'Management') + ' table ' + (i+1)); });
  // Login/recovery pages have no workspace main; keep the skip link meaningful.
  if (!document.getElementById('main-content')) { const main = document.querySelector('main,.login-panel'); if (main) { main.id = 'main-content'; main.tabIndex = -1; } }
})();
