(() => {
  'use strict';
  const el = id => document.getElementById(id);
  let albums = [], selected = null, albumPage = 1, imagePage = 1, configured = false;
  const form = el('album-form');
  const initialControls = [...form.elements, el('album-new'), el('album-select')];
  initialControls.forEach(control => { control.disabled = true; });
  function status(text) { el('gallery-upload-status').textContent = text; }
  function clearAlbum() { selected = null; form.reset(); el('album-select').value = ''; el('album-form-title').textContent = 'New album'; el('gallery-upload').disabled = true; el('gallery-images').replaceChildren(); el('gallery-empty').hidden = false; el('gallery-images-title').textContent = 'Album photos'; }
  async function loadAlbums(preferredId = selected?.id) {
    const data = await api('gallery/albums?page=' + albumPage); albums = data.items;
    el('album-select').replaceChildren(new Option('Select an album', ''));
    for (const album of albums) el('album-select').append(new Option(album.title + (album.isPublished ? ' · Published' : ' · Draft'), album.id));
    el('albums-prev').disabled = albumPage <= 1; el('albums-next').disabled = albumPage * 50 >= data.total;
    const album = albums.find(a => a.id === preferredId) || albums[0];
    if (album) await selectAlbum(album.id); else clearAlbum();
  }
  async function selectAlbum(id) {
    selected = albums.find(a => a.id === id);
    if (!selected) { clearAlbum(); return; }
    el('album-select').value = selected.id;
    form.elements.title.value = selected.title; form.elements.slug.value = selected.slug; form.elements.description.value = selected.description;
    form.elements.isPublished.checked = selected.isPublished; el('album-form-title').textContent = 'Edit album';
    el('gallery-upload').disabled = !configured; imagePage = 1; await loadImages();
  }
  function makeLabel(title, control) { const label = document.createElement('label'); label.className = 'mb-3'; label.textContent = title; label.append(control); return label; }
  async function loadImages() {
    if (!selected) return;
    el('gallery-images-title').textContent = selected.title;
    el('gallery-images-description').textContent = selected.isPublished ? 'Album published. Only published photos appear on the website.' : 'Album is a draft. Its photos are hidden from the website.';
    const data = await api(`gallery/albums/${selected.id}/images?page=${imagePage}`);
    el('gallery-images').replaceChildren(); el('gallery-empty').hidden = data.total !== 0;
    el('images-page').textContent = `Page ${imagePage} · ${data.total} photos`; el('images-prev').disabled = imagePage <= 1; el('images-next').disabled = imagePage * 24 >= data.total;
    for (const image of data.items) {
      const card = document.createElement('article'); card.className = 'gallery-image-card';
      if (image.isComplete) { const img = document.createElement('img'); img.src = image.url; img.alt = image.altText || image.fileName; img.loading = 'lazy'; card.append(img); }
      const content = document.createElement('div'); content.className = 'p-3'; const name = document.createElement('p'); name.className = 'small fw-semibold'; name.textContent = image.fileName; content.append(name);
      if (image.isComplete) {
        const imageForm = document.createElement('form'); const alt = document.createElement('input'); alt.className = 'form-control'; alt.value = image.altText; alt.required = true; alt.maxLength = 300;
        const caption = document.createElement('textarea'); caption.className = 'form-control'; caption.value = image.caption; caption.maxLength = 1000; caption.rows = 2;
        const published = document.createElement('input'); published.type = 'checkbox'; published.checked = image.isPublished;
        const check = makeLabel('Publish photo', published); check.className = 'check-label mb-3';
        const save = document.createElement('button'); save.type = 'submit'; save.className = 'btn btn-primary primary'; save.textContent = 'Save photo';
        imageForm.append(makeLabel('Image description (alt text)', alt), makeLabel('Caption', caption), check, save);
        imageForm.addEventListener('submit', event => { event.preventDefault(); perform(save, async () => { await api('gallery/images/' + image.id, 'PUT', { altText: alt.value, caption: caption.value, isPublished: published.checked, version: image.version }); await loadImages(); notice('Photo saved.'); }); });
        content.append(imageForm);
      } else {
        const note = document.createElement('p'); note.className = 'small muted'; note.textContent = 'Upload not completed. If the file finished uploading, retry completion. Otherwise remove it and select the image again.'; content.append(note);
        content.append(button('Finish upload', async () => { await api('gallery/images/' + image.id + '/complete', 'POST', {}); await loadImages(); }));
      }
      const remove = button('Remove photo', async () => { if (!confirm('Remove this photo from the gallery?')) return; await api('gallery/images/' + image.id, 'DELETE', {}); await loadImages(); notice('Photo removed from the gallery.'); }); remove.classList.add('mt-3'); content.append(remove); card.append(content); el('gallery-images').append(card);
    }
  }
  function putFile(url, file) {
    return new Promise((resolve, reject) => {
      const request = new XMLHttpRequest(); request.open('PUT', url); request.withCredentials = false; request.timeout = 300000;
      request.setRequestHeader('Content-Type', file.type);
      request.upload.addEventListener('progress', e => { if (e.lengthComputable) el('gallery-progress').value = Math.round(e.loaded / e.total * 100); });
      request.addEventListener('load', () => request.status >= 200 && request.status < 300 ? resolve() : reject(new Error('The image could not be uploaded. Check the storage connection and try again.')));
      request.addEventListener('error', () => reject(new Error('Upload connection failed. Your administrator may need to check gallery storage permissions.')));
      request.addEventListener('timeout', () => reject(new Error('The upload timed out. Try again on a stronger connection.')));
      request.send(file);
    });
  }
  el('album-new').addEventListener('click', clearAlbum);
  el('album-select').addEventListener('change', event => perform(el('album-select'), () => selectAlbum(event.target.value)));
  form.addEventListener('submit', event => {
    event.preventDefault(); perform(event.submitter, async () => {
      const body = { title: form.elements.title.value, slug: form.elements.slug.value, description: form.elements.description.value, isPublished: form.elements.isPublished.checked, version: selected?.version || 1 };
      const result = await api(selected ? 'gallery/albums/' + selected.id : 'gallery/albums', selected ? 'PUT' : 'POST', body);
      await loadAlbums(result.id); notice(result.isPublished ? 'Album saved and published.' : 'Album saved as a draft.');
    });
  });
  el('gallery-upload-form').addEventListener('submit', event => {
    event.preventDefault(); perform(event.submitter, async () => {
      if (!selected || !configured) throw new Error('Save an album and connect image storage before uploading.');
      const files = Array.from(el('gallery-files').files);
      if (files.length > 20) throw new Error('Choose up to 20 photos at a time.');
      for (const file of files) if (!['image/jpeg','image/png','image/webp'].includes(file.type) || file.size < 16 || file.size > 10485760) throw new Error(`${file.name}: choose a JPEG, PNG or WebP image up to 10 MB.`);
      el('gallery-progress').hidden = false; let completed = 0;
      try {
        for (const file of files) {
          status(`Uploading ${completed + 1} of ${files.length}: ${file.name}`); el('gallery-progress').value = 0;
          const upload = await api('gallery/uploads', 'POST', { albumId: selected.id, fileName: file.name, contentType: file.type, size: file.size, altText: file.name.replace(/\.[^.]+$/, '').replace(/[-_]/g, ' ') });
          await putFile(upload.uploadUrl, file); await api('gallery/images/' + upload.id + '/complete', 'POST', {}); completed++;
        }
        el('gallery-files').value = ''; status(`${completed} photo${completed === 1 ? '' : 's'} uploaded. Add descriptions and publish the photos when ready.`);
      } catch (error) { status(`${completed} uploaded. ${error.message}`); throw error; }
      finally { el('gallery-progress').hidden = true; await loadImages(); }
    });
  });
  el('gallery-refresh').addEventListener('click', () => perform(el('gallery-refresh'), () => loadAlbums()));
  for (const [id, offset] of [['images-prev',-1],['images-next',1]]) el(id).addEventListener('click', () => perform(el(id), async () => { imagePage += offset; await loadImages(); }));
  for (const [id, offset] of [['albums-prev',-1],['albums-next',1]]) el(id).addEventListener('click', () => perform(el(id), async () => { albumPage += offset; await loadAlbums(); }));
  (async () => {
    try { const info = await api('gallery/storage'); configured = info.configured; el('gallery-storage-message').hidden = configured; el('gallery-storage-message').textContent = 'Image uploads are not connected yet. Ask your administrator to enable gallery storage. You can still prepare your albums.'; await loadAlbums(); }
    catch (error) { notice(error.message, true); }
    finally { initialControls.forEach(control => { control.disabled = false; }); }
  })();
})();
