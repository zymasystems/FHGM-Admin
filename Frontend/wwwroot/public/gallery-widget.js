(() => {
  'use strict';
  for (const root of document.querySelectorAll('[data-fhgm-gallery]')) {
    const apiBase = (root.dataset.api || '/api/public/gallery').replace(/\/$/, '');
    let page = 1, currentAlbum = null;
    const status = document.createElement('p'); status.setAttribute('role', 'status');
    const heading = document.createElement('h2'), description = document.createElement('p');
    const back = document.createElement('button'); back.type = 'button'; back.className = 'fhgm-gallery-button'; back.textContent = '← All albums'; back.hidden = true;
    const grid = document.createElement('div'); grid.className = 'fhgm-gallery-grid';
    const navigation = document.createElement('div'); navigation.className = 'fhgm-gallery-navigation';
    const previous = document.createElement('button'), next = document.createElement('button'), count = document.createElement('span');
    previous.type = next.type = 'button'; previous.className = next.className = 'fhgm-gallery-button'; previous.textContent = 'Previous'; next.textContent = 'Next'; navigation.append(previous, count, next); root.append(back, heading, description, status, grid, navigation);
    async function load() {
      previous.disabled = next.disabled = true; status.textContent = 'Loading gallery…'; grid.replaceChildren();
      try {
        const response = await fetch(apiBase + '/albums' + (currentAlbum ? '/' + encodeURIComponent(currentAlbum) : '') + '?page=' + page, { credentials: 'omit' });
        if (!response.ok) throw new Error('The gallery is unavailable. Please try again later.');
        const data = await response.json(); heading.textContent = currentAlbum ? data.title : 'Photo albums'; description.textContent = currentAlbum ? data.description : ''; back.hidden = !currentAlbum;
        status.textContent = data.total ? '' : currentAlbum ? 'There are no published photos in this album yet.' : 'New ministry photos will appear here soon.';
        for (const item of data.items) {
          const figure = document.createElement('figure'); figure.className = 'fhgm-gallery-card';
          const img = document.createElement('img'); img.src = currentAlbum ? item.url : item.coverUrl; img.alt = currentAlbum ? item.altText : item.title; img.loading = 'lazy'; img.decoding = 'async';
          const caption = document.createElement('figcaption'); caption.textContent = currentAlbum ? item.caption : item.title + ' · ' + item.imageCount + ' photos';
          if (currentAlbum) { const link = document.createElement('a'); link.href = item.url; link.target = '_blank'; link.rel = 'noopener'; link.append(img); figure.append(link, caption); }
          else { const open = document.createElement('button'); open.type = 'button'; open.className = 'fhgm-gallery-album'; open.append(img, caption); open.addEventListener('click', () => { currentAlbum = item.slug; page = 1; load(); }); figure.append(open); }
          grid.append(figure);
        }
        count.textContent = 'Page ' + page; previous.disabled = page <= 1; next.disabled = page * (currentAlbum ? 24 : 20) >= data.total;
      } catch (error) { status.textContent = error.message; }
    }
    back.addEventListener('click', () => { currentAlbum = null; page = 1; load(); }); previous.addEventListener('click', () => { page--; load(); }); next.addEventListener('click', () => { page++; load(); }); load();
  }
})();
