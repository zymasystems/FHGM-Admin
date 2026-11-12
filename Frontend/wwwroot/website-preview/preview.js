(() => {
  'use strict';
  function node(tag, text, className) { const el = document.createElement(tag); el.textContent = text || ''; if (className) el.className = className; return el; }
  window.addEventListener('message', event => {
    if (event.source !== parent || event.origin !== location.origin || event.data?.type !== 'fhgm-article-preview') return;
    const post = event.data.post, root = document.getElementById('cmsContent');
    document.getElementById('cmsTitle').textContent = post.title;
    document.getElementById('cmsDescription').textContent = post.excerpt;
    document.getElementById('cmsStatus').textContent = '';
    root.replaceChildren();
    try { const url = new URL(post.featuredImageUrl); if (url.protocol === 'https:') { const image = document.createElement('img'); image.className = 'cms-photo'; image.src = url.href; image.alt = post.title; root.append(image); } } catch (_) { /* Optional image. */ }
    const date = new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium', timeZone: 'Africa/Johannesburg' }).format(new Date(post.publishedAt || Date.now()));
    root.append(node('p', [post.author, date].filter(Boolean).join(' · '), 'cms-meta'));
    const body = node('div', '', 'cms-copy'); FhgmContent.render(body, post.content, post.contentFormat); root.append(body);
    const back = node('a', '← All articles', 'link-gold'); back.href = 'https://hisgraceministries.co.za/blog.html'; root.append(back);
    parent.postMessage({ type: 'fhgm-preview-rendered', key: event.data.key }, location.origin);
  });
  // Preview navigation must never replace the article being reviewed.
  document.addEventListener('click', event => { if (event.target.closest('a')) event.preventDefault(); });
  parent.postMessage({ type: 'fhgm-preview-ready' }, location.origin);
})();
