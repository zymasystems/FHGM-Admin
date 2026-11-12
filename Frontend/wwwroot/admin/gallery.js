(() => {
  'use strict';
  // optional-album-previews-v2: used by deployment smoke checks.
  const el = id => document.getElementById(id);
  const standalone = { id: null, title: 'Standalone photos', slug: '', description: '', isPublished: true };
  let albums = [], selected = standalone, albumPage = 1, imagePage = 1, configured = false, search = "", imageItems = [], selectedIds = new Set(), queue = [], uploading = false;
  let editingImage = null, savingImage = false;
  const imageDialog = el("gallery-image-editor"), imageForm = el("gallery-image-form");
  const form = el('album-form');
  const uploadForm = el('gallery-upload-form');
  let chosenFiles = [];
  const previewUrls = new Map();
  const uploadMode = () => uploadForm.elements.uploadMode.value;
  function uploadLabels() {
    const bulk = uploadMode() === 'bulk';
    el('gallery-files').multiple = bulk;
    el('gallery-picker-help').textContent = bulk ? 'Choose up to 20 images in one selection. Use Ctrl to select individual files, Shift to select a range, or drag a group of photos here.' : 'Choose one image, or select Bulk Upload to choose several files together.';
    el('gallery-file-prompt').textContent = bulk ? '3. Choose multiple images' : '3. Choose your image';
    el('gallery-upload').disabled = uploading || !configured || !chosenFiles.length;
    el('gallery-upload').textContent = bulk ? `Bulk Upload ${chosenFiles.length || 'selected'} images` : 'Upload single image';
    el('upload-destination').textContent = selected ? 'Uploading to: ' + selected.title : 'Create or choose an album before uploading.';
  }

  function syncFileInput() {
    const data = new DataTransfer(); chosenFiles.forEach(file => data.items.add(file)); el('gallery-files').files = data.files;
  }
  function drawFilePreviews() {
    for (const [file, url] of previewUrls) if (!chosenFiles.includes(file)) { URL.revokeObjectURL(url); previewUrls.delete(file); }
    el('gallery-file-previews').replaceChildren();
    el('gallery-file-count').textContent = chosenFiles.length ? `${chosenFiles.length} image${chosenFiles.length === 1 ? '' : 's'} selected` : 'No images selected';
    el('gallery-preview-empty').hidden = chosenFiles.length > 0; el('gallery-clear-files').hidden = !chosenFiles.length;
    for (const file of chosenFiles) {
      if (!previewUrls.has(file)) previewUrls.set(file, URL.createObjectURL(file));
      const card = document.createElement('article'); card.className = 'gallery-file-preview';
      const image = document.createElement('img'); image.src = previewUrls.get(file); image.alt = 'Selected image: ' + file.name;
      const name = document.createElement('p'); name.textContent = file.name;
      const size = document.createElement('small'); size.textContent = (file.size / 1024 / 1024).toFixed(2) + ' MB';
      const remove = button('Remove image', async () => { if (uploading) return; chosenFiles = chosenFiles.filter(item => item !== file); syncFileInput(); drawFilePreviews(); });
      remove.setAttribute('aria-label', 'Remove ' + file.name); remove.disabled = uploading;
      card.append(image, name, size, remove); el('gallery-file-previews').append(card);
    }
    uploadLabels();
  }
  function chooseFiles(files) {
    if (uploading) return;
    const candidates = Array.from(files), limit = uploadMode() === 'single' ? 1 : 20;
    const errors = [];
    if (candidates.length > limit) errors.push(`Choose no more than ${limit} image${limit === 1 ? '' : 's'} in this mode.`);
    for (const file of candidates) if (!['image/jpeg','image/png','image/webp'].includes(file.type) || file.size < 16 || file.size > 10485760) errors.push(`${file.name}: choose a JPEG, PNG or WebP image up to 10 MB.`);
    el('gallery-file-error').textContent = errors.join(' ');
    // Preserve a valid selection when a new selection is invalid.
    if (errors.length) { syncFileInput(); return; }
    chosenFiles = candidates; syncFileInput(); drawFilePreviews();
  }
  function lockUploadControls(locked) {
    el('gallery-bulk-start').disabled = locked; el('gallery-files').disabled = locked; el('album-select').disabled = locked; el('album-new').disabled = locked;
    el('gallery-clear-files').disabled = locked;
    for (const radio of uploadForm.elements.uploadMode) radio.disabled = locked;
    drawFilePreviews();
  }
  const initialControls = [...form.elements, el('album-new'), el('album-select')];
  initialControls.forEach(control => { control.disabled = true; });
  function status(text) { el('gallery-upload-status').textContent = text; }
  async function clearAlbum() { selected = standalone; uploadLabels(); form.hidden = false; form.reset(); el('album-select').value = ''; el('album-form-title').textContent = 'New album'; el('gallery-upload').disabled = !configured || uploading || !chosenFiles.length; await loadImages(); }
  async function loadAlbums(preferredId = selected?.id) {
    const data = await api('gallery/albums?page=' + albumPage + '&search=' + encodeURIComponent(search)); albums = data.items;
    el('album-select').replaceChildren(new Option('No album — standalone gallery photos', ''));
    for (const album of albums) el('album-select').append(new Option(album.title + (album.isPublished ? ' · Published' : ' · Draft'), album.id));
    el('albums-prev').disabled = albumPage <= 1; el('albums-next').disabled = albumPage * 50 >= data.total;
    await selectAlbum(albums.some(a => a.id === preferredId) ? preferredId : "");
  }
  async function selectAlbum(id) {
    selected = id ? albums.find(a => a.id === id) : standalone;
    form.hidden = !id;
    if (!selected) { await clearAlbum(); return; }
    el('album-select').value = selected.id || '';
    uploadLabels();
    form.elements.title.value = selected.title; form.elements.slug.value = selected.slug; form.elements.description.value = selected.description;
    form.elements.isPublished.checked = selected.isPublished; el('album-form-title').textContent = 'Edit album';
    el('gallery-upload').disabled = !configured || uploading || !chosenFiles.length; imagePage = 1; await loadImages();
  }
  function openImageEditor(image) {
    editingImage = {...image}; imageForm.reset(); el('gallery-editor-error').textContent = '';
    el('gallery-editor-name').textContent = image.fileName;
    el('gallery-editor-image').src = image.url; el('gallery-editor-image').alt = image.altText || image.fileName;
    imageForm.elements.altText.value = image.altText; imageForm.elements.caption.value = image.caption;
    imageForm.elements.isPublished.checked = image.isPublished; imageDialog.showModal();
  }
  el('gallery-editor-close').addEventListener('click', () => { if (!savingImage) imageDialog.close(); });
  imageDialog.addEventListener('cancel', event => { if (savingImage) event.preventDefault(); });
  imageForm.addEventListener('submit', event => {
    event.preventDefault(); if (savingImage || !editingImage) return;
    perform(event.submitter, async () => {
      savingImage = true; el('gallery-editor-save').textContent = 'Saving…'; const image = {...editingImage}; el('gallery-editor-error').textContent = '';
      try {
        await api('gallery/images/' + image.id, 'PUT', {altText:imageForm.elements.altText.value,caption:imageForm.elements.caption.value,isPublished:imageForm.elements.isPublished.checked,version:image.version});
        imageDialog.close(); editingImage = null; await loadImages(); notice('Photo description saved.');
      } catch (error) { el('gallery-editor-error').textContent = error.message; }
      finally { savingImage = false; el('gallery-editor-save').textContent = 'Save photo'; }
    });
  });
  async function loadImages() {
    if (!selected) return;
    el('gallery-images-title').textContent = selected.title;
    el('gallery-public-link').hidden = !selected.id || !selected.isPublished; el('gallery-public-link').href = 'https://hisgraceministries.co.za/gallery.html?album=' + encodeURIComponent(selected.slug);
    el('gallery-images-description').textContent = selected.isPublished ? 'Album published. Only published photos appear on the website.' : 'Album is a draft. Its photos are hidden from the website.';
    if (!selected.id) el('gallery-images-description').textContent = 'Photos without an album. Publish each photo or use the bulk actions below.';
    const data = await api((selected.id ? `gallery/albums/${selected.id}/images` : 'gallery/images') + `?page=${imagePage}`);
    imageItems = data.items; selectedIds.clear(); selectionStatus();
    if (selected.id) el('gallery-images-description').textContent += ` ${selected.unpublishedCount || 0} completed photos are still drafts; ${selected.pendingCount || 0} uploads are incomplete.`;
    el('gallery-images').replaceChildren(); el('gallery-empty').hidden = data.total !== 0;
    el('images-page').textContent = `Page ${imagePage} · ${data.total} photos`; el('images-prev').disabled = imagePage <= 1; el('images-next').disabled = imagePage * 24 >= data.total;
    for (const image of data.items) {
      const card = document.createElement('article'); card.className = 'gallery-image-card';
      if (image.isComplete) { const img = document.createElement('img'); img.src = image.url; img.alt = image.altText || image.fileName; img.loading = 'lazy';
        const open = button('', async () => openImageEditor(image)); open.className = 'gallery-photo-open'; open.setAttribute('aria-label','Edit photo: ' + (image.altText || image.fileName)); open.append(img); card.append(open); }
      const checkbox = document.createElement('input'); checkbox.type = 'checkbox'; checkbox.setAttribute('aria-label', 'Select ' + image.fileName);
      checkbox.addEventListener('change', () => { checkbox.checked ? selectedIds.add(image.id) : selectedIds.delete(image.id); selectionStatus(); }); card.append(checkbox);
      const state = document.createElement('p'); state.className = 'badge'; state.textContent = !image.isComplete ? 'Upload incomplete' : image.isPublished ? 'Published photo' : 'Draft photo'; card.append(state);
      const content = document.createElement('div'); content.className = 'p-3'; const name = document.createElement('p'); name.className = 'small fw-semibold'; name.textContent = image.fileName; content.append(name);
      if (image.isComplete) {
        const description = document.createElement('p'); description.className = 'small muted'; description.textContent = image.altText; content.append(description, button('Edit photo', async () => openImageEditor(image)));
        if (image.isPublished && selected.id) content.append(button(selected.coverImageId === image.id ? 'Current album cover' : 'Use as album cover', async () => {
          await api('gallery/albums/' + selected.id, 'PUT', { ...selected, coverImageId: image.id }); await loadAlbums(); notice('Album cover updated.');
        }));
      } else {
        const note = document.createElement('p'); note.className = 'small muted'; note.textContent = 'Upload not completed. If the file finished uploading, retry completion. Otherwise remove it and select the image again.'; content.append(note);
        content.append(button('Finish upload', async () => { await api('gallery/images/' + image.id + '/complete', 'POST', {}); await loadImages(); }));
      }
      const remove = button('Remove photo', async () => { if (!confirm('Remove this photo from the gallery?')) return; await api('gallery/images/' + image.id, 'DELETE', {}); await loadImages(); notice('Photo removed from the gallery.'); }); remove.classList.add('mt-3'); content.append(remove); card.append(content); el('gallery-images').append(card);
    }
  }
  function putFile(url, file, item) {
    return new Promise((resolve, reject) => {
      const request = new XMLHttpRequest(); request.open('PUT', url); request.withCredentials = false; request.timeout = 300000;
      request.setRequestHeader('Content-Type', file.type);
      request.upload.addEventListener('progress', e => { if (e.lengthComputable) { const percent = Math.round(e.loaded / e.total * 100); if (item) item.progress = percent; el('gallery-progress').value = Math.round(queue.reduce((sum,x) => sum + (x.done ? 100 : x.progress || 0),0) / queue.length); if (item) { item.state = 'Uploading ' + percent + '%'; el('gallery-progress-label').textContent = `${queue.filter(x => x.done).length} of ${queue.length} uploaded · ${item.file.name}: ${percent}%`; drawQueue(); } } });
      request.addEventListener('load', () => request.status >= 200 && request.status < 300 ? resolve() : reject(new Error('The image could not be uploaded. Check the storage connection and try again.')));
      request.addEventListener('error', () => reject(new Error('The browser could not reach image storage. Check your connection; ask the developer to check the R2 bucket CORS origin, PUT permissions and browser console. Retry this photo after the cause is fixed.')));
      request.addEventListener('timeout', () => reject(new Error('The upload timed out. Try again on a stronger connection.')));
      request.send(file);
    });
  }
  el('album-new').addEventListener('click', () => perform(el('album-new'), clearAlbum));
  el('album-select').addEventListener('change', event => perform(el('album-select'), () => selectAlbum(event.target.value)));
  form.addEventListener('submit', event => {
    event.preventDefault(); perform(event.submitter, async () => {
      const body = { title: form.elements.title.value, slug: form.elements.slug.value, description: form.elements.description.value, isPublished: form.elements.isPublished.checked, version: selected?.version || 1, coverImageId: selected?.coverImageId || null };
      const result = await api(selected?.id ? 'gallery/albums/' + selected.id : 'gallery/albums', selected?.id ? 'PUT' : 'POST', body);
      await loadAlbums(result.id); notice(result.isPublished ? 'Album saved and published.' : 'Album saved as a draft.');
    });
  });
  function selectionStatus() { el('gallery-selection-status').textContent = `${selectedIds.size} photos selected on this page.`; }
  el('album-search-form').addEventListener('submit', event => { event.preventDefault(); search = el('album-search').value.trim(); albumPage = 1; perform(event.submitter, () => loadAlbums(null)); });
  el('select-photos').addEventListener('click', () => {
    const select = selectedIds.size !== imageItems.length; selectedIds = new Set(select ? imageItems.map(x => x.id) : []);
    el('gallery-images').querySelectorAll('article > input[type=checkbox]').forEach(x => { x.checked = select; }); selectionStatus();
  });
  for (const [id, action] of [['publish-photos', 'publish'], ['unpublish-photos', 'unpublish'], ['remove-photos', 'remove']]) {
    el(id).addEventListener('click', () => perform(el(id), async () => {
      const chosen = imageItems.filter(x => selectedIds.has(x.id));
      if (!chosen.length) throw new Error('Select photos first.');
      if (action === 'remove' && !confirm(`Remove ${chosen.length} selected photos?`)) return;
      let done = 0, failures = [];
      for (const photo of chosen) {
        try {
          if (action === 'remove') await api('gallery/images/' + photo.id, 'DELETE', {});
          else {
            if (!photo.isComplete) throw new Error('Upload is incomplete.');
            await api('gallery/images/' + photo.id, 'PUT', { altText: photo.altText, caption: photo.caption, isPublished: action === 'publish', version: photo.version });
          }
          done++;
        } catch (e) { failures.push(photo.fileName + ': ' + e.message); }
      }
      await loadAlbums(); notice(`${done} updated. ${failures.join(' ')}`, failures.length > 0);
    }));
  }
  function drawQueue() {
    el('upload-queue').replaceChildren();
    for (const item of queue) {
      const row = document.createElement('p'); row.className = item.state.startsWith('Failed') ? 'upload-result failed' : item.done ? 'upload-result success' : 'upload-result'; row.textContent = `${item.file.name}: ${item.state}`;
      if (item.state.startsWith('Failed') && !uploading) row.append(button('Retry photo', () => runQueue([item])));
      el('upload-queue').append(row);
    }
  }
  async function runQueue(items) {
    if (uploading) throw new Error('Wait for the current upload to finish.');
    uploading = true; lockUploadControls(true); el('gallery-upload').disabled = true; el('gallery-progress').hidden = false;
    try {
      const remaining = items.slice();
      async function worker() { while (remaining.length) {
        const item = remaining.shift();
        try {
          item.state = 'Uploading'; item.done = false; item.progress = 0; drawQueue();
          // Reuse the record safely; a completed image is never removed by retry.
          const upload = item.uploadId
            ? await api('gallery/images/' + item.uploadId + '/retry', 'POST', {})
            : await api('gallery/uploads', 'POST', { albumId: item.albumId, fileName: item.file.name, contentType: item.file.type, size: item.file.size, altText: item.file.name.replace(/\.[^.]+$/, '').replace(/[-_]/g, ' ') });
          if (upload.isComplete) { item.state = 'Uploaded · draft'; item.done = true; item.uploadId = null; drawQueue(); continue; }
          item.uploadId = upload.id;
          await putFile(upload.uploadUrl, item.file, item);
          item.state = 'Verifying'; drawQueue();
          await api('gallery/images/' + upload.id + '/complete', 'POST', {});
          item.state = 'Uploaded · draft'; item.done = true; item.uploadId = null;
        } catch (error) { item.state = 'Failed: ' + error.message; item.progress = 0; }
        drawQueue();
        el('gallery-progress').value = Math.round(queue.filter(x => x.done).length / queue.length * 100);
      }
      }
      // Keep uploads bounded so storage/API limits and per-file retries remain useful.
      await Promise.all(Array.from({length:Math.min(3, items.length)}, () => worker()));
      const succeeded = queue.filter(item => item.done).length, failed = queue.filter(item => item.state.startsWith('Failed')).length;
      status(`${succeeded} uploaded successfully${failed ? `; ${failed} failed — use Retry photo below` : ''}. Uploaded photos are drafts; publish them below when ready.`);
      el('gallery-progress-label').textContent = `${succeeded} of ${queue.length} uploaded${failed ? ` · ${failed} failed` : ''}`;
      await loadAlbums();
    } finally { uploading = false; lockUploadControls(false); drawQueue(); el('gallery-progress').hidden = true; }
  }
  el('gallery-upload-form').addEventListener('submit', event => {
    event.preventDefault(); perform(event.submitter, async () => {
      if (!selected || !configured) throw new Error('Choose standalone photos or save an album, and connect image storage before uploading.');
      const files = chosenFiles.slice();
      const limit = uploadMode() === 'single' ? 1 : 20;
      if (!files.length || files.length > limit) throw new Error(`Choose 1–${limit} photos for this upload mode.`);
      for (const file of files) if (!['image/jpeg','image/png','image/webp'].includes(file.type) || file.size < 16 || file.size > 10485760) throw new Error(`${file.name}: choose a JPEG, PNG or WebP image up to 10 MB.`);
      if (queue.some(x => x.state.startsWith('Failed')) && !confirm('Replace the retry queue? Incomplete records can still be removed from the album.')) return;
      queue = files.map(file => ({ file, albumId: selected.id, state: 'Waiting', uploadId: null, done: false }));
      chosenFiles = []; syncFileInput(); drawFilePreviews(); await runQueue(queue);
    }).finally(uploadLabels);
  });
  el('gallery-bulk-start').addEventListener('click', () => {
    if (uploading) return;
    const mode = uploadForm.querySelector('[name=uploadMode][value=bulk]'); mode.checked = true; mode.dispatchEvent(new Event('change'));
    el('gallery-files').click();
  });
  el('gallery-files').addEventListener('change', event => chooseFiles(event.target.files));
  el('gallery-clear-files').addEventListener('click', () => { if (uploading) return; chosenFiles = []; syncFileInput(); el('gallery-file-error').textContent = ''; drawFilePreviews(); });
  for (const radio of uploadForm.elements.uploadMode) radio.addEventListener('change', () => {
    if (uploadMode() === 'single' && chosenFiles.length > 1) { chosenFiles = chosenFiles.slice(0, 1); status('Switched to Single image. Only the first selected image was kept.'); }
    syncFileInput(); el('gallery-file-error').textContent = ''; drawFilePreviews();
  });
  window.addEventListener('pageshow', event => { if (event.persisted) drawFilePreviews(); });
  window.addEventListener('pagehide', () => { for (const url of previewUrls.values()) URL.revokeObjectURL(url); previewUrls.clear(); });
  const drop = el('gallery-upload-form');
  drop.addEventListener('dragover', event => { event.preventDefault(); });
  drop.addEventListener('drop', event => { event.preventDefault(); if (!uploading && event.dataTransfer.files.length) { chooseFiles(event.dataTransfer.files); } });
  el('gallery-refresh').addEventListener('click', () => perform(el('gallery-refresh'), () => loadAlbums()));
  for (const [id, offset] of [['images-prev',-1],['images-next',1]]) el(id).addEventListener('click', () => perform(el(id), async () => { imagePage += offset; await loadImages(); }));
  for (const [id, offset] of [['albums-prev',-1],['albums-next',1]]) el(id).addEventListener('click', () => perform(el(id), async () => { albumPage += offset; await loadAlbums(); }));
  (async () => {
    try { const info = await api('gallery/storage'); configured = info.configured; el('gallery-storage-message').hidden = configured; el('gallery-storage-message').textContent = 'Image uploads are not connected yet. Ask your administrator to enable gallery storage. You can still prepare your albums.'; await loadAlbums(); }
    catch (error) { notice(error.message, true); }
    finally { initialControls.forEach(control => { control.disabled = false; }); }
  })();
})();

