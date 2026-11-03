'use strict';
const $ = id => document.getElementById(id);
let editorGeneration = 0;
let savingBlog = false, autosaveTimer = null, autosaveBlocked = false;
let csrf = '', me = null, current = null, activeView = document.body.dataset.view || 'overview';
const pages = { blogs: 1, users: 1, audit: 1 };
const isEditor = () => me.roles.some(r => ['SuperAdmin', 'ChurchAdmin', 'BlogEditor'].includes(r));
const isBlogTeam = () => me.roles.some(r => ['SuperAdmin', 'ChurchAdmin', 'BlogEditor', 'Writer'].includes(r));
const date = value => value ? new Date(value).toLocaleString() : '—';
const label = value => value === 'SuperAdmin' ? 'Developer Super Admin' : value.replace(/([a-z])([A-Z])/g, '$1 $2');
function notice(message, error = false) { $('notice').textContent = message; $('notice').className = error ? 'error' : ''; }
async function token() { const r = await fetch('/api/auth/csrf', { credentials: 'same-origin' }); if (!r.ok) throw new Error('Could not establish a secure session.'); csrf = (await r.json()).token; }
async function api(path, method = 'GET', body) {
  const headers = { 'Accept': 'application/json' };
  if (method !== 'GET') { if (!csrf) await token(); headers['X-CSRF-TOKEN'] = csrf; headers['Content-Type'] = 'application/json'; }
  const response = await fetch('/api/' + path, { method, headers, credentials: 'same-origin', body: body === undefined ? undefined : JSON.stringify(body) });
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    if (data?.passwordChangeRequired) location.assign('/account/security');
    if (response.status === 401 && me) { me = null; $('editor')?.close(); showLogin(); }
    throw new Error(data?.message || (data?.errors ? Object.values(data.errors).flat().join(' ') : data?.title) || ({401:'Please sign in again.',403:'Your role does not allow this action.',429:'Too many attempts. Wait a minute and try again.'}[response.status]) || 'The request failed. Please try again.');
  }
  return data;
}
function showLogin() { location.assign('/account/login'); }
async function showWorkspace() {
  if (me.mustChangePassword) { location.assign('/account/security'); return; }
  if ($('identity')) $('identity').textContent = me.displayName + '\n' + me.roles.map(label).join(', ');
  document.querySelectorAll('.nav-item').forEach(link => link.classList.toggle('selected', link.pathname === location.pathname));
  await token();
  if (['blogs','users','audit'].includes(activeView)) await loadView();
}
function cell(row, value) { const td = document.createElement('td'); td.textContent = value ?? ''; row.append(td); return td; }
function button(text, handler) { const b = document.createElement('button'); b.type = 'button'; b.className = 'btn btn-outline-secondary outline'; b.textContent = text; b.addEventListener('click', () => perform(b, handler)); return b; }
async function perform(target, action) { target.disabled = true; try { await action(); } catch (error) { notice(error.message, true); } finally { target.disabled = false; } }
function pagination(prefix, data, size) { $(prefix + '-page').textContent = `Page ${data.page} · ${data.total} records`; $(prefix + '-prev').disabled = data.page <= 1; $(prefix + '-next').disabled = data.page * size >= data.total; }
async function loadView() {
  if (activeView === 'blogs') {
    const data = await api('blog?page=' + pages.blogs); $('blog-total').textContent = data.total; $('blog-rows').replaceChildren(); $('blog-empty').hidden = data.total !== 0;
    for (const post of data.items) {
      const tr = document.createElement('tr'); cell(tr, post.title);
      const badge = document.createElement('span'); badge.className = 'badge ' + post.status; badge.textContent = label(post.status); cell(tr, '').append(badge);
      cell(tr, post.author); cell(tr, date(post.updatedAt)); const actions = cell(tr, ''); actions.append(button('Open →', () => openEditor(post.id)));
      if (post.status === 'Published') { const view = document.createElement('a'); view.href = 'https://hisgraceministries.co.za/blog-post.html?slug=' + encodeURIComponent(post.slug); view.target = '_blank'; view.rel = 'noopener'; view.textContent = ' View on website'; actions.append(view); } $('blog-rows').append(tr);
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
    for (const entry of data.items) { const tr = document.createElement('tr'); cell(tr, entry.action); cell(tr, entry.entity + ' · ' + entry.entityId); cell(tr, entry.userName); cell(tr, date(entry.createdAt)); $('audit-rows').append(tr); }
    pagination('audit', data, 50);
  }
}
const fields = ['title','slug','excerpt','content','category','tags','seoTitle','seoDescription','contentFormat','featuredImageUrl'];
async function openEditor(id) {
  editorGeneration++;
  clearTimeout(autosaveTimer); autosaveBlocked = false; $('article-preview').hidden = true;
  current = id ? await api('blog/' + id) : null;
  $('blog-form').reset(); $('editor-error').textContent = '';
  for (const key of fields) $('blog-form').elements[key].value = current?.[key] || (key === 'contentFormat' ? 'PlainText' : '');
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
        if (savingBlog) { $('editor-error').textContent = 'Wait for the draft to finish saving.'; return; }
        clearTimeout(autosaveTimer);
        const data = new FormData($('blog-form'));
        if (editable && fields.some(key => (data.get(key) || '') !== (current[key] || (key === 'contentFormat' ? 'PlainText' : '')))) { $('editor-error').textContent = 'Save your changes before continuing the workflow.'; return; }
        let note = '';
        if (['request-changes','reject'].includes(action)) { note = prompt('Add a review note for the writer:'); if (note === null) return; }
        if (action === 'publish' && !confirm('Publish this article on the public website?')) return;
        try { await api('blog/' + current.id + '/workflow', 'POST', { action, version: current.version, note }); const articleId = current.id; await openEditor(articleId); await loadView(); } catch (error) { $('editor-error').textContent = error.message; }
      }));
    }
  }
  if (!$('editor').open) $('editor').showModal();
}
$('login-form')?.addEventListener('submit', async event => {
  event.preventDefault(); const submit = event.submitter; submit.disabled = true; $('login-error').textContent = '';
  try { await token(); me = await api('auth/login', 'POST', Object.fromEntries(new FormData(event.target))); location.assign(me.mustChangePassword ? '/account/security' : event.target.dataset.returnUrl || '/admin/'); } catch (error) { $('login-error').textContent = error.message; } finally { submit.disabled = false; }
});
$('logout')?.addEventListener('click', () => perform($('logout'), async () => { await api('auth/logout','POST',{}); me = null; showLogin(); }));
$('new-blog')?.addEventListener('click', () => perform($('new-blog'), () => openEditor()));
$('close-editor')?.addEventListener('click', () => { if (confirm('Close the editor? Unsaved changes will be lost.')) $('editor').close(); });
$('editor')?.addEventListener('cancel', event => { if (!confirm('Close the editor? Unsaved changes will be lost.')) event.preventDefault(); });
async function saveBlog(automatic = false) {
  const form = $('blog-form');
  if (savingBlog || form.elements.title.disabled || !form.checkValidity() || automatic && autosaveBlocked) return;
  const generation = editorGeneration;
  savingBlog = true; $('save-blog').disabled = true; $('editor-error').textContent = '';
  try {
    const body = Object.fromEntries(new FormData(form)); body.version = current?.version || 1;
    const saved = await api(current ? 'blog/' + current.id : 'blog', current ? 'PUT' : 'POST', body);
    if (generation !== editorGeneration) return;
    current = saved; $('autosave-status').textContent = `Saved at ${new Date().toLocaleTimeString()} · Version ${saved.version}`;
    if (!automatic) { await openEditor(saved.id); await loadView(); }
  } catch (error) { autosaveBlocked = true; $('editor-error').textContent = error.message + ' Autosave paused; reopen the article to resolve changes.'; }
  finally { savingBlog = false; $('save-blog').disabled = false; }
}
$('blog-form')?.addEventListener('submit', async event => { event.preventDefault(); clearTimeout(autosaveTimer); await saveBlog(); });
$('blog-form')?.addEventListener('input', () => {
  clearTimeout(autosaveTimer);
  if (!$('blog-form').elements.title.disabled) autosaveTimer = setTimeout(() => { if ($('editor').open) saveBlog(true); }, 3000);
});
$('format-toolbar')?.addEventListener('click', event => {
  const format = event.target.dataset.format; if (!format) return;
  const form = $('blog-form'), input = form.elements.content; if (input.disabled) return;
  const selected = input.value.slice(input.selectionStart, input.selectionEnd) || 'Text';
  let value = {heading: '## ' + selected, bold: '**' + selected + '**', italic: '*' + selected + '*', list: '- ' + selected}[format];
  if (format === 'link') { const url = prompt('HTTPS link'); if (!url) return; try { if (new URL(url).protocol !== 'https:') return; } catch { return; } value = '[' + selected + '](' + url + ')'; }
  form.elements.contentFormat.value = 'Markdown'; input.setRangeText(value, input.selectionStart, input.selectionEnd, 'end'); input.dispatchEvent(new Event('input', {bubbles:true})); input.focus();
});
$('preview-blog')?.addEventListener('click', () => {
  const root = $('article-preview'), form = $('blog-form'); root.hidden = false; root.replaceChildren();
  const heading = document.createElement('h2'); heading.textContent = form.elements.title.value;
  const excerpt = document.createElement('p'); excerpt.textContent = form.elements.excerpt.value;
  const body = document.createElement('div'); body.className = 'article-body'; FhgmContent.render(body, form.elements.content.value, form.elements.contentFormat.value);
  root.append(heading, excerpt);
  try { const url = new URL(form.elements.featuredImageUrl.value); if (url.protocol === 'https:') { const img = document.createElement('img'); img.src = url.href; img.alt = form.elements.title.value; img.className = 'img-fluid'; root.append(img); } } catch (_) { /* Optional image. */ }
  root.append(body);
});
$('user-form')?.addEventListener('submit', async event => { event.preventDefault(); await perform(event.submitter, async () => { await api('users','POST',Object.fromEntries(new FormData(event.target))); event.target.reset(); pages.users = 1; await loadView(); notice('Team account created.'); }); });
for (const [prefix, view] of [['blog','blogs'],['user','users'],['audit','audit']]) for (const [direction, offset] of [['prev',-1],['next',1]]) $(prefix + '-' + direction)?.addEventListener('click', () => perform($(prefix + '-' + direction), async () => { pages[view] += offset; await loadView(); }));
(async () => { if (!$('workspace')) return; try { me = await api('auth/me'); await showWorkspace(); } catch (error) { if (me) notice(error.message, true); else showLogin(); } })();

