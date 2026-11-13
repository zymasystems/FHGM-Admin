'use strict';
const $ = id => document.getElementById(id);
let editorGeneration = 0, blogRevision = 0, reviewedVersion = null, reviewedSnapshot = null, previewPayload = null;
const isBlogAdmin = () => me.roles.some(r => ["SuperAdmin", "ChurchAdmin"].includes(r));
let savingBlog = false, autosaveTimer = null, autosaveBlocked = false;
let csrf = '', me = null, current = null, activeView = document.body.dataset.view || 'overview';
const pages = { blogs: 1, users: 1, audit: 1 };
const isEditor = () => me.roles.some(r => ['SuperAdmin', 'ChurchAdmin', 'BlogEditor'].includes(r));
const isBlogTeam = () => me.roles.some(r => ['SuperAdmin', 'ChurchAdmin', 'BlogEditor', 'Writer'].includes(r));
const blogStatus = value => value === 'Submitted' ? 'Pending Review' : label(value);
const date = value => value ? new Date(value).toLocaleString() : '—';
const label = value => value === 'SuperAdmin' ? 'Developer Super Admin' : value.replace(/([a-z])([A-Z])/g, '$1 $2');
function notice(message, error = false) {
  const root = $('notice'); if (!root) return; root.replaceChildren(); root.className = error ? 'error' : ''; root.setAttribute('role',error ? 'alert' : 'status');
  if (!message) return; const text = document.createElement('span'); text.textContent = message; const dismiss = document.createElement('button'); dismiss.type = 'button'; dismiss.className = 'icon-button notice-close'; dismiss.setAttribute('aria-label','Dismiss notification'); dismiss.append(PortalUI.icon('close')); dismiss.addEventListener('click', () => root.replaceChildren());
  root.append(PortalUI.icon(error ? 'alert' : 'check'),text,dismiss);
}
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
  if (me.mustChangePassword && !location.pathname.startsWith('/account/security')) { location.assign('/account/security'); return; }
  if ($('identity')) $('identity').textContent = me.displayName + '\n' + me.roles.map(label).join(', ');
  document.querySelectorAll('.nav-item').forEach(link => link.classList.toggle('selected', link.pathname === location.pathname));
  await token();
  if (['blogs','users','audit'].includes(activeView)) await loadView();
}
function cell(row, value) { const td = document.createElement('td'); td.textContent = value ?? ''; row.append(td); return td; }
function button(text, handler) { const b = document.createElement('button'); b.type = 'button'; b.className = 'btn btn-outline-secondary outline'; b.textContent = text; PortalUI.decorateButton(b); b.addEventListener('click', () => perform(b, handler)); return b; }
async function perform(target, action) { target.disabled = true; target.dataset.busy = 'true'; target.setAttribute('aria-busy','true'); try { await action(); } catch (error) { notice(error.message, true); } finally { target.disabled = false; delete target.dataset.busy; target.removeAttribute('aria-busy'); } }
function pagination(prefix, data, size) { $(prefix + '-page').textContent = `Page ${data.page} · ${data.total} records`; $(prefix + '-prev').disabled = data.page <= 1; $(prefix + '-next').disabled = data.page * size >= data.total; }
async function loadView() {
  if (activeView === 'blogs') {
    $('blog-workflow-summary').textContent = isBlogAdmin() ? 'Save → Preview → Publish' : 'Draft → Review → Publish';
    $('blog-workflow-help').textContent = isBlogAdmin() ? 'Save your article, preview it, then publish. No separate approval is needed. Use Edit article or Delete blog beside each article.' : 'Open an article to write or review it. Writers submit drafts for review.';
    const data = await api('blog?page=' + pages.blogs); $('blog-total').textContent = data.total; $('blog-rows').replaceChildren(); $('blog-empty').hidden = data.total !== 0;
    for (const post of data.items) {
      const tr = document.createElement('tr'); cell(tr, post.title);
      const badge = document.createElement('span'); badge.className = 'badge ' + post.status; badge.textContent = blogStatus(post.status) + (post.hasPendingChanges ? ' · changes pending' : ''); cell(tr, '').append(badge);
      cell(tr, post.author); cell(tr, date(post.updatedAt)); const actions = cell(tr, ''); actions.className = 'blog-actions';
      actions.append(button('Preview', async () => { await openEditor(post.id); showBlogPreview(); }), button('Edit article', () => openEditor(post.id)));
      if (isBlogAdmin()) {
        if (post.status !== 'Published') actions.append(button('Publish', async () => { await openEditor(post.id); showBlogPreview(); $('editor-error').textContent = 'Review the website preview, then choose Publish reviewed article below.'; }));
        if (post.status === 'Published') actions.append(button('Unpublish', () => listTransition(post, 'unpublish')));
        if (post.status !== 'Archived') actions.append(button('Archive', () => listTransition(post, 'archive')));
        const remove = button('Delete blog', async () => { if (!await PortalUI.confirm(`Permanently delete “${post.title}”? This cannot be undone and removes it from the website.`)) return; await api('blog/' + post.id + '?version=' + post.version, 'DELETE'); await loadView(); notice('Article deleted.'); });
        remove.classList.add('blog-delete'); actions.append(remove);
      }
      if (post.status === 'Published') { const view = document.createElement('a'); view.href = 'https://hisgraceministries.co.za/blog-post.html?slug=' + encodeURIComponent(post.slug); view.target = '_blank'; view.rel = 'noopener'; view.textContent = ' View on website'; actions.append(view); } $('blog-rows').append(tr);
    }
    pagination('blog', data, 25);
  } else if (activeView === 'users') {
    const data = await api('users?page=' + pages.users); $('user-rows').replaceChildren();
    for (const user of data.items) {
      const tr = document.createElement('tr'); cell(tr, user.displayName); cell(tr, user.email); cell(tr, user.roles.map(label).join(', ')); cell(tr, user.isActive ? 'Active' : 'Disabled'); const td = cell(tr, '');
      if (user.id !== me.id && !user.roles.includes('SuperAdmin')) td.append(button(user.isActive ? 'Disable' : 'Enable', async () => { if (!await PortalUI.confirm(`${user.isActive ? 'Disable' : 'Enable'} access for ${user.displayName}?`)) return; await api('users/' + encodeURIComponent(user.id) + '/active', 'PUT', { isActive: !user.isActive }); await loadView(); }));
      $('user-rows').append(tr);
    }
    pagination('user', data, 25);
  } else {
    const data = await api('audit?page=' + pages.audit); $('audit-rows').replaceChildren();
    for (const entry of data.items) { const tr = document.createElement('tr'); cell(tr, entry.action); cell(tr, entry.entity + ' · ' + entry.entityId); cell(tr, entry.userName); cell(tr, date(entry.createdAt)); $('audit-rows').append(tr); }
    pagination('audit', data, 50);
  }
}
async function listTransition(post, action) {
  if (!await PortalUI.confirm(`${action === 'unpublish' ? 'Unpublish' : 'Archive'} “${post.title}”? It will no longer appear on the public website.`)) return;
  await api('blog/' + post.id + '/workflow', 'POST', {action, version:post.version});
  await loadView(); notice(action === 'unpublish' ? 'Article unpublished and saved as a draft.' : 'Article archived.');
}
const fields = ['title','slug','excerpt','content','category','tags','seoTitle','seoDescription','contentFormat','featuredImageUrl'];
async function openEditor(id) {
  editorGeneration++; blogRevision = 0; reviewedVersion = null; reviewedSnapshot = null; previewPayload = null;
  clearTimeout(autosaveTimer); autosaveBlocked = false; $('article-preview').hidden = true; $('blog-form').hidden = false; $('blog-feedback').textContent = '';
  current = id ? await api('blog/' + id) : null;
  $('blog-form').reset(); $('editor-error').textContent = '';
  for (const key of fields) $('blog-form').elements[key].value = current?.[key] || (key === 'contentFormat' ? 'PlainText' : '');
  $('delete-current-blog').hidden = !current || !isBlogAdmin();
  $('editor-heading').textContent = current ? 'Edit article' : 'New article';
  $('editor-status').textContent = current ? blogStatus(current.status) + ' · Version ' + current.version : 'Draft · Your changes will be saved when you select Save draft.';
  $('review-note').hidden = !current?.reviewNote; $('review-note').textContent = current?.reviewNote || '';
  const editable = !current || isBlogAdmin() || (['Draft','ChangesRequested','Rejected','Submitted'].includes(current.status) && (isEditor() || current.status !== 'Submitted'));
  for (const key of fields) $('blog-form').elements[key].disabled = !editable;
  $('save-blog').hidden = !editable; $('save-blog').textContent = isBlogAdmin() ? 'Save & review' : current ? 'Save changes' : 'Save draft';
  $('blog-save-help').textContent = current?.status === 'Published' ? 'Save a private revision for review. The currently published article stays live until you confirm publication.' : 'Save your changes, preview the article, then choose a publishing action.'; $('workflow').replaceChildren();
  if (current) {
    const actions = { Draft:[['submit','Submit for review']], ChangesRequested:[['submit','Resubmit for review']], Rejected:[['submit','Resubmit for review']], Submitted:[['request-changes','Request changes'],['reject','Reject'],['approve','Approve article']], Approved:[['publish','Publish article'],['archive','Archive']], Published:[['archive','Archive / unpublish']], Archived:[['restore','Restore to draft']] };
    if (isBlogAdmin() && ['Draft','ChangesRequested','Rejected','Submitted'].includes(current.status)) actions[current.status] = [...(current.status === 'Submitted' ? [['request-changes','Request changes'],['reject','Reject']] : []), ['publish', 'Publish reviewed article']];
    if (isBlogAdmin()) {
      if (current.status === 'Published') actions.Published = [...(current.hasPendingChanges ? [['publish','Publish reviewed changes']] : []), ['unpublish','Unpublish'],['archive','Archive']];
      if (current.status === 'Approved') actions.Approved = [['publish','Publish reviewed article'],['archive','Archive']];
      if (current.status === 'Archived') actions.Archived = [['restore','Restore to draft'],['publish','Publish reviewed article']];
    }
    for (const [action, text] of actions[current.status] || []) {
      if (!isEditor() && action !== 'submit') continue;
      $('workflow').append(button(text, async () => {
        if (savingBlog) { $('editor-error').textContent = 'Wait for the draft to finish saving.'; return; }
        clearTimeout(autosaveTimer);
        const data = new FormData($('blog-form'));
        if (editable && fields.some(key => (data.get(key) || '') !== (current[key] || (key === 'contentFormat' ? 'PlainText' : '')))) { if (isBlogAdmin() && action === 'publish') { await saveBlog(); return; } $('editor-error').textContent = 'Save your changes before continuing the workflow.'; return; }
        let note = '';
        if (['request-changes','reject'].includes(action)) { note = prompt('Add a review note for the writer:'); if (note === null) return; }
        if (action === 'publish' && isBlogAdmin() && reviewedVersion !== current.version) { showBlogPreview(); return; }
        if (['unpublish','archive'].includes(action) && !await PortalUI.confirm('Remove this article from the public website?')) return;
        if (action === 'publish' && !await PortalUI.confirm('Publish this article on the public website?')) return;
        try { await api('blog/' + current.id + '/workflow', 'POST', { action, version: current.version, note, reviewed: reviewedVersion === current.version }); const articleId = current.id; await openEditor(articleId); await loadView(); if (action === 'publish') { $('blog-feedback').textContent = 'Article published successfully. The reviewed version is now visible on the website.'; notice('Article published successfully.'); } } catch (error) { $('editor-error').textContent = error.message; }
      }));
    }
  }
  if (!$('editor').open) $('editor').showModal();
}
$('login-form')?.addEventListener('submit', async event => {
  event.preventDefault(); const submit = event.submitter; submit.disabled = true; submit.dataset.busy = 'true'; $('login-error').textContent = '';
  try { await token(); me = await api('auth/login', 'POST', Object.fromEntries(new FormData(event.target))); location.assign(me.mustChangePassword ? '/account/security' : event.target.dataset.returnUrl || '/admin/'); } catch (error) { $('login-error').textContent = error.message; } finally { submit.disabled = false; delete submit.dataset.busy; }
});
$('logout')?.addEventListener('click', () => perform($('logout'), async () => { await api('auth/logout','POST',{}); me = null; showLogin(); }));
$('delete-current-blog')?.addEventListener('click', () => perform($('delete-current-blog'), async () => {
  if (savingBlog) throw new Error('Wait for the draft to finish saving.');
  if (!current || !await PortalUI.confirm(`Permanently delete “${current.title}”? This also removes it from the public website.`)) return;
  clearTimeout(autosaveTimer);
  await api('blog/' + current.id + '?version=' + current.version, 'DELETE');
  editorGeneration++; current = null; $('editor').close(); await loadView(); notice('Article deleted.');
}));
$('new-blog')?.addEventListener('click', () => perform($('new-blog'), () => openEditor()));
$('close-editor')?.addEventListener('click', async () => { if (await PortalUI.confirm('Close the editor? Unsaved changes will be lost.')) $('editor').close(); });
$('editor')?.addEventListener('cancel', async event => { if ($('blog-form').hidden) return; event.preventDefault(); if (await PortalUI.confirm('Close the editor? Unsaved changes will be lost.')) $('editor').close(); });
async function saveBlog(automatic = false) {
  const form = $('blog-form');
  if (savingBlog || form.elements.title.disabled || !form.checkValidity() || automatic && autosaveBlocked) return;
  if (automatic && current?.status === 'Published') return;
  const generation = editorGeneration, revision = blogRevision;
  let reviewAfterSave = false;
  savingBlog = true; $('autosave-status').textContent = 'Saving article…'; $('save-blog').disabled = true; $('editor-error').textContent = '';
  try {
    const body = Object.fromEntries(new FormData(form)); body.version = current?.version || 1; body.reviewed = reviewedSnapshot === blogSnapshot();
    const saved = await api(current ? 'blog/' + current.id : 'blog', current ? 'PUT' : 'POST', body);
    if (generation !== editorGeneration) return;
    saved.author = current?.author || me.displayName;
    reviewedVersion = null; reviewedSnapshot = null; current = saved; $('autosave-status').textContent = `Saved at ${new Date().toLocaleTimeString()} · Version ${saved.version}`;
    if (!automatic) { if (revision === blogRevision) { await openEditor(saved.id); reviewAfterSave = isBlogAdmin(); } await loadView(); }
  } catch (error) { autosaveBlocked = true; $('editor-error').textContent = error.message + ' Autosave paused; reopen the article to resolve changes.'; }
  finally {
    savingBlog = false; $('save-blog').disabled = false;
    if (reviewAfterSave && $('editor').open) showBlogPreview();
    if (generation === editorGeneration && revision !== blogRevision && !autosaveBlocked && $('editor').open) {
      clearTimeout(autosaveTimer); autosaveTimer = setTimeout(() => saveBlog(true), 3000);
    }
  }
}
$('blog-form')?.addEventListener('submit', async event => { event.preventDefault(); clearTimeout(autosaveTimer); await saveBlog(); });
$('blog-form')?.addEventListener('input', () => {
  reviewedVersion = null; reviewedSnapshot = null; blogRevision++;
  clearTimeout(autosaveTimer);
  if (!$('blog-form').elements.title.disabled && current?.status !== 'Published') autosaveTimer = setTimeout(() => { if ($('editor').open) saveBlog(true); }, 3000);
});
$('format-toolbar')?.addEventListener('click', event => {
  const format = event.target.dataset.format; if (!format) return;
  const form = $('blog-form'), input = form.elements.content; if (input.disabled) return;
  const selected = input.value.slice(input.selectionStart, input.selectionEnd) || 'Text';
  let value = {heading: '## ' + selected, bold: '**' + selected + '**', italic: '*' + selected + '*', list: '- ' + selected}[format];
  if (format === 'link') { const url = prompt('HTTPS link'); if (!url) return; try { if (new URL(url).protocol !== 'https:') return; } catch { return; } value = '[' + selected + '](' + url + ')'; }
  form.elements.contentFormat.value = 'Markdown'; input.setRangeText(value, input.selectionStart, input.selectionEnd, 'end'); input.dispatchEvent(new Event('input', {bubbles:true})); input.focus();
});
function blogSnapshot() { return JSON.stringify(fields.map(key => $('blog-form').elements[key].value)); }
function sendBlogPreview() {
  if (previewPayload) $('website-preview').contentWindow.postMessage(previewPayload, location.origin);
}
function showBlogPreview() {
  const form = $('blog-form');
  // The preview tracks this exact form content; saving or editing invalidates it.
  reviewedVersion = null; reviewedSnapshot = null;
  previewPayload = {type:'fhgm-article-preview', key:blogSnapshot(), post:{...Object.fromEntries(fields.map(key => [key, form.elements[key].value])), author:current?.author || me.displayName, publishedAt:current?.publishedAt}};
  $('review-details').textContent = ['Category: ' + (form.elements.category.value || 'None'), 'SEO title: ' + (form.elements.seoTitle.value || form.elements.title.value), 'SEO description: ' + (form.elements.seoDescription.value || form.elements.excerpt.value)].join(' · '); $('review-status').textContent = 'Loading article preview…'; $('article-preview').hidden = false; if (isBlogAdmin()) $('blog-form').hidden = true; sendBlogPreview(); $('editor').scrollTop = 0;
}
window.addEventListener('message', event => {
  if (event.origin !== location.origin || event.source !== $('website-preview')?.contentWindow) return;
  if (event.data?.type === 'fhgm-preview-ready') { sendBlogPreview(); return; }
  if (event.data?.type !== 'fhgm-preview-rendered' || !previewPayload || event.data.key !== previewPayload.key || event.data.key !== blogSnapshot() || savingBlog) return;
  $('review-status').textContent = 'Ready for review. Your article is not published until you confirm.';
  reviewedSnapshot = event.data.key;
  const form = $('blog-form');
  reviewedVersion = current && fields.every(key => (form.elements[key].value || '') === (current[key] || (key === 'contentFormat' ? 'PlainText' : ''))) ? current.version : null;
  if (isBlogAdmin()) $('editor-error').textContent = current?.status === 'Published' ? 'Your changes are saved privately. Review them, then choose Publish reviewed changes.' : reviewedVersion === null ? 'Save your changes, then preview again before publishing.' : 'Review the preview, then choose Publish reviewed article.';
});
$('review-edit')?.addEventListener('click', () => { $('blog-form').hidden = false; $('article-preview').hidden = true; $('editor').scrollTop = 0; });
$('review-cancel')?.addEventListener('click', () => { $('editor').close(); });
$('preview-blog')?.addEventListener('click', showBlogPreview);
$('preview-desktop')?.addEventListener('click', () => { $('website-preview').classList.remove('mobile-preview'); });
$('preview-mobile')?.addEventListener('click', () => { $('website-preview').classList.add('mobile-preview'); });
$('user-form')?.addEventListener('submit', async event => { event.preventDefault(); await perform(event.submitter, async () => { await api('users','POST',Object.fromEntries(new FormData(event.target))); event.target.reset(); pages.users = 1; await loadView(); notice('Team account created.'); }); });
for (const [prefix, view] of [['blog','blogs'],['user','users'],['audit','audit']]) for (const [direction, offset] of [['prev',-1],['next',1]]) $(prefix + '-' + direction)?.addEventListener('click', () => perform($(prefix + '-' + direction), async () => { pages[view] += offset; await loadView(); }));
(async () => { if (!$('workspace')) return; try { me = await api('auth/me'); await showWorkspace(); } catch (error) { if (me) notice(error.message, true); else showLogin(); } })();

