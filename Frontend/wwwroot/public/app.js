'use strict';
const $ = id => document.getElementById(id);
let page = 1;
async function get(path) { const response = await fetch('/api/public/blog' + path); if (!response.ok) throw new Error(response.status === 404 ? 'This article is not available.' : 'We could not load the ministry stories. Please try again later.'); return response.json(); }
async function load() {
  $('public-message').textContent = 'Loading ministry stories…';
  try {
    const data = await get('?page=' + page); $('articles').replaceChildren();
    $('public-message').textContent = data.total ? '' : 'Our next story is on its way. Please visit again soon.';
    for (const post of data.items) {
      const card = document.createElement('article'); card.className = 'article-card';
      const category = document.createElement('p'); category.className = 'eyebrow'; category.textContent = post.category || 'MINISTRY STORY';
      const heading = document.createElement('h2'); heading.textContent = post.title;
      const excerpt = document.createElement('p'); excerpt.className = 'muted'; excerpt.textContent = post.excerpt;
      const meta = document.createElement('p'); meta.className = 'small muted'; meta.textContent = post.author + ' · ' + new Date(post.publishedAt).toLocaleDateString();
      const link = document.createElement('a'); link.href = '/public/?article=' + encodeURIComponent(post.slug); link.textContent = 'Read story →';
      card.append(category, heading, excerpt, meta, link); $('articles').append(card);
    }
    $('page').textContent = `Page ${page}`; $('previous').disabled = page === 1; $('next').disabled = page * 12 >= data.total;
  } catch (error) { $('public-message').textContent = error.message; }
}
$('previous').addEventListener('click', async () => { page--; await load(); });
$('next').addEventListener('click', async () => { page++; await load(); });
(async () => {
  const slug = new URLSearchParams(location.search).get('article');
  if (!slug) { await load(); return; }
  try {
    const post = await get('/' + encodeURIComponent(slug)); $('listing').hidden = true; $('article').hidden = false;
    $('article-title').textContent = post.title; $('article-excerpt').textContent = post.excerpt;
    // Article content is always plain text, never inserted as HTML.
    $('article-content').textContent = post.content; $('article-meta').textContent = (post.category || 'MINISTRY STORY') + ' · ' + post.author;
    document.title = (post.seoTitle || post.title) + ' · FHGM';
    document.querySelector('meta[name="description"]').content = post.seoDescription || post.excerpt;
  } catch (error) { $('public-message').textContent = error.message; $('articles').replaceChildren(); $('previous').hidden = true; $('next').hidden = true; }
})();
