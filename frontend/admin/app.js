'use strict';
const $ = id => document.getElementById(id);
let csrf = '', me = null, current = null, activeView = 'blogs';
const pages = { blogs: 1, users: 1, audit: 1 };
const isEditor = () => me.roles.some(r => ['SuperAdmin', 'BlogEditor'].includes(r));
const isBlogTeam = () => me.roles.some(r => ['SuperAdmin', 'BlogEditor', 'Writer'].includes(r));
const date = value => value ? new Date(value).toLocaleString() : '—';
const label = value => value.replace(/([a-z])([A-Z])/g, '$1 $2');
function notice(message, error = false) { $('notice').textContent = message; $('notice').className = error ? 'error' : ''; }
async function token() { const r = await fetch('/api/auth/csrf', { credentials: 'same-origin' }); if (!r.ok) throw new Error('Could not establish a secure session.'); csrf = (await r.json()).token; }
async function api(path, method = 'GET', body) {
  const headers = { 'Accept': 'application/json' };
  if (method !== 'GET') { if (!csrf) await token(); headers['X-CSRF-TOKEN'] = csrf; headers['Content-Type'] = 'application/json'; }
  const response = await fetch('/api/' + path, { method, headers, credentials: 'same-origin', body: body === undefined ? undefined : JSON.stringify(body) });
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    if (response.status === 401 && me) { me = null; $('editor').close(); showLogin(); }
    throw new Error(data?.message || (data?.errors ? Object.values(data.errors).flat().join(' ') : data?.title) || ({401:'Please sign in again.',403:'Your role does not allow this action.',429:'Too many attempts. Wait a minute and try again.'}[response.status]) || 'The request failed. Please try again.');
  }
  return data;
}
function showLogin() { $('login-screen').hidden = false; $('workspace').hidden = true; csrf = ''; }
async function showWorkspace() {
  $('login-screen').hidden = true; $('workspace').hidden = false;
  $('identity').textContent = me.displayName + '\n' + me.roles.map(label).join(', ');
  document.querySelector('[data-view="blogs"]').hidden = !isBlogTeam();
  for (const view of ['users', 'audit']) document.querySelector(`[data-view="${view}"]`).hidden = !me.roles.includes('SuperAdmin');
  await token();
  if (isBlogTeam()) await switchView('blogs');
  else {
    for (const view of ['blogs', 'users', 'audit']) $(view + '-view').hidden = true;
    notice('Your account is active. Media and communications tools are planned for the next implementation stage.');
  }
}
async function switchView(view) {
  activeView = view; notice('');
  for (const name of ['blogs', 'users', 'audit']) $(name + '-view').hidden = view !== name;
  document.querySelectorAll('[data-view]').forEach(b => b.classList.toggle('selected', b.dataset.view === view));
  await loadView();
}
function cell(row, value) { const td = document.createElement('td'); td.textContent = value ?? ''; row.append(td); return td; }
function button(text, handler) { const b = document.createElement('button'); b.type = 'button'; b.className = 'outline'; b.textContent = text; b.addEventListener('click', () => perform(b, handler)); return b; }
async function perform(target, action) { target.disabled = true; try { await action(); } catch (error) { notice(error.message, true); } finally { target.disabled = false; } }
function pagination(prefix, data, size) { $(prefix + '-page').textContent = `Page ${data.page} · ${data.total} records`; $(prefix + '-prev').disabled = data.page <= 1; $(prefix + '-next').disabled = data.page * size >= data.total; }
async function loadView() {
  if (activeView === 'blogs') {
    const data = await api('blog?page=' + pages.blogs); $('blog-total').textContent = data.total; $('blog-rows').replaceChildren(); $('blog-empty').hidden = data.total !== 0;
    for (const post of data.items) {
      const tr = document.createElement('tr'); cell(tr, post.title);
      const badge = document.createElement('span'); badge.className = 'badge ' + post.status; badge.textContent = label(post.status); cell(tr, '').append(badge);
      cell(tr, post.author); cell(tr, date(post.updatedAt)); cell(tr, '').append(button('Open →', () => openEditor(post.id))); $('blog-rows').append(tr);
    }
    pagination('blog', data, 25);
  } else if (activeView === 'users') {
    const data = await api('users?page=' + pages.users); $('user-rows').replaceChildren();
    for (const user of data.items) {
      const tr = document.createElement('tr'); cell(tr, user.displayName); cell(tr, user.email); cell(tr, user.roles.map(label).join(', ')); cell(tr, user.isActive ? 'Active' : 'Disabled'); const td = cell(tr, '');
      if (user.id !== me.id && !user.roles.includes('SuperAdmin')) td.append(button(user.isActive ? 'Disable' : 'Enable', async () => { if (!confirm(`${user.isActive ? 'Disable' : 'Enable'} access for ${user.displayName}?`)) return; await api('users/' + encodeURIComponent(user.id) + '/active', 'PUT', { isActive: !user.isActive }); await loadView(); }));
      $('user-rows').append(tr);
    }
    pagination('user', data, 25);
  } else {
    const data = await api('audit?page=' + pages.audit); $('audit-rows').replaceChildren();
    for (const entry of data.items) { const tr = document.createElement('tr'); cell(tr, entry.action); cell(tr, entry.entity + ' · ' + entry.entityId); cell(tr, entry.userId); cell(tr, date(entry.createdAt)); $('audit-rows').append(tr); }
    pagination('audit', data, 50);
  }
}
const fields = ['title','slug','excerpt','content','category','tags','seoTitle','seoDescription'];
async function openEditor(id) {
  current = id ? await api('blog/' + id) : null;
  $('blog-form').reset(); $('editor-error').textContent = '';
  for (const key of fields) $('blog-form').elements[key].value = current?.[key] || '';
  $('editor-heading').textContent = current ? 'Edit article' : 'New article';
  $('editor-status').textContent = current ? label(current.status) + ' · Version ' + current.version : 'Draft · Your changes will be saved when you select Save draft.';
  $('review-note').hidden = !current?.reviewNote; $('review-note').textContent = current?.reviewNote || '';
  const editable = !current || (['Draft','ChangesRequested','Rejected','Submitted'].includes(current.status) && (isEditor() || current.status !== 'Submitted'));
  for (const key of fields) $('blog-form').elements[key].disabled = !editable;
  $('save-blog').hidden = !editable; $('save-blog').textContent = current ? 'Save changes' : 'Save draft'; $('workflow').replaceChildren();
  if (current) {
    const actions = { Draft:[['submit','Submit for review']], ChangesRequested:[['submit','Resubmit for review']], Rejected:[['submit','Resubmit for review']], Submitted:[['request-changes','Request changes'],['reject','Reject'],['approve','Approve article']], Approved:[['publish','Publish article'],['archive','Archive']], Published:[['archive','Archive / unpublish']], Archived:[['restore','Restore to draft']] };
    for (const [action, text] of actions[current.status] || []) {
      if (!isEditor() && action !== 'submit') continue;
      $('workflow').append(button(text, async () => {
        const data = new FormData($('blog-form'));
        if (editable && fields.some(key => (data.get(key) || '') !== (current[key] || ''))) { $('editor-error').textContent = 'Save your changes before continuing the workflow.'; return; }
        let note = '';
        if (['request-changes','reject'].includes(action)) { note = prompt('Add a review note for the writer:'); if (note === null) return; }
        if (action === 'publish' && !confirm('Publish this article on the public website?')) return;
        try { await api('blog/' + current.id + '/workflow', 'POST', { action, version: current.version, note }); const articleId = current.id; await openEditor(articleId); await loadView(); } catch (error) { $('editor-error').textContent = error.message; }
      }));
    }
  }
  if (!$('editor').open) $('editor').showModal();
}
$('login-form').addEventListener('submit', async event => {
  event.preventDefault(); const submit = event.submitter; submit.disabled = true; $('login-error').textContent = '';
  try { await token(); me = await api('auth/login', 'POST', Object.fromEntries(new FormData(event.target))); event.target.reset(); await showWorkspace(); } catch (error) { $('login-error').textContent = error.message; } finally { submit.disabled = false; }
});
$('logout').addEventListener('click', () => perform($('logout'), async () => { await api('auth/logout','POST',{}); me = null; showLogin(); }));
$('new-blog').addEventListener('click', () => perform($('new-blog'), () => openEditor()));
$('close-editor').addEventListener('click', () => { if (confirm('Close the editor? Unsaved changes will be lost.')) $('editor').close(); });
$('editor').addEventListener('cancel', event => { if (!confirm('Close the editor? Unsaved changes will be lost.')) event.preventDefault(); });
$('blog-form').addEventListener('submit', async event => {
  event.preventDefault(); const submit = event.submitter; submit.disabled = true; $('editor-error').textContent = '';
  try { const body = Object.fromEntries(new FormData(event.target)); body.version = current?.version || 1; const post = await api(current ? 'blog/' + current.id : 'blog', current ? 'PUT' : 'POST', body); await openEditor(post.id); await loadView(); } catch (error) { $('editor-error').textContent = error.message; } finally { submit.disabled = false; }
});
$('user-form').addEventListener('submit', async event => { event.preventDefault(); await perform(event.submitter, async () => { await api('users','POST',Object.fromEntries(new FormData(event.target))); event.target.reset(); pages.users = 1; await loadView(); notice('Team account created.'); }); });
document.querySelectorAll('[data-view]').forEach(b => b.addEventListener('click', () => perform(b, () => switchView(b.dataset.view))));
for (const [prefix, view] of [['blog','blogs'],['user','users'],['audit','audit']]) for (const [direction, offset] of [['prev',-1],['next',1]]) $(prefix + '-' + direction).addEventListener('click', () => perform($(prefix + '-' + direction), async () => { pages[view] += offset; await loadView(); }));
(async () => { try { me = await api('auth/me'); await showWorkspace(); } catch (error) { showLogin(); if (!error.message.includes('sign in')) $('login-error').textContent = error.message; } })();
