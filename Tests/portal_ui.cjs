const { chromium } = require('playwright');
const assert = require('node:assert/strict');
(async () => {
 const browser = await chromium.launch({headless:true});
 const page = await browser.newPage();
 require('node:fs').mkdirSync('test-results', {recursive:true});
 const base = process.env.FHGM_TEST_URL || 'http://127.0.0.1:5080';
 await page.goto(base + '/account/login');
 await page.locator('[name=email]').fill(process.env.Bootstrap__Email);
 await page.locator('[name=password]').fill(process.env.Bootstrap__Password);
 await page.getByRole('button',{name:/Sign in/}).click();
 await page.waitForURL(base + '/admin/');
 await page.waitForFunction(() => document.querySelector('#dashboard-summary')?.textContent.includes('Articles'));
 await page.goto(base + '/admin/blogs');
 await page.locator('#new-blog').click();
 const title = 'Browser article ' + Date.now();
 await page.locator('#blog-form [name=title]').fill(title);
 await page.locator('#blog-form [name=slug]').fill('browser-' + Date.now());
 await page.locator('#blog-form [name=excerpt]').fill('Browser preview');
 await page.locator('#blog-form [name=contentFormat]').selectOption('Markdown');
 await page.locator('#blog-form [name=content]').fill('## Heading\n**Bold text**\n<script>alert(1)</script>');
 await page.locator('#preview-blog').click();
 assert.equal(await page.frameLocator('#website-preview').locator('#cmsContent strong').textContent(),'Bold text');
 assert.equal(await page.frameLocator('#website-preview').locator('#cmsContent script').count(),0);
 await page.locator('#review-edit').click();
 await page.waitForFunction(() => document.querySelector('#autosave-status').textContent.startsWith('Saved at'));
 let delayed = false;
 await page.route('**/api/blog/*', async route => {
  if (route.request().method() === 'PUT' && !delayed) { delayed = true; await new Promise(resolve => setTimeout(resolve, 4500)); }
  await route.continue();
 });
 await page.locator('#blog-form [name=content]').fill('First edit');
 await page.waitForFunction(() => document.querySelector('#save-blog').disabled);
 await page.locator('#blog-form [name=content]').fill('Latest edit made while saving');
 await page.waitForFunction(() => current && current.content === 'Latest edit made while saving', undefined, {timeout:20000});
 await page.unroute('**/api/blog/*');
 await page.locator('#save-blog').click();
 await page.waitForFunction(() => document.querySelector('#blog-rows').textContent.includes('Browser article'));
 await page.locator('#delete-current-blog').waitFor({state:'visible'});
 assert.equal(await page.locator('#blog-form').isHidden(), true);
 await page.waitForFunction(() => reviewedVersion === current.version);
 assert.equal(await page.frameLocator('#website-preview').locator('#cmsTitle').textContent(), title);
 page.once('dialog', dialog => dialog.accept());
 await page.getByRole('button', {name:'Publish reviewed article', exact:true}).click();
 await page.waitForFunction(() => document.querySelector('#editor-status').textContent.startsWith('Published'));
 // Save a published revision privately and automatically enter review.
 const publishedSlug = await page.locator('#blog-form [name=slug]').inputValue();
 await page.locator('#blog-form [name=content]').fill('Reviewed change to the published article');
 await page.locator('#save-blog').click();
 await page.waitForFunction(() => current?.hasPendingChanges && reviewedVersion === current.version);
 assert.equal(await page.locator('#blog-form').isHidden(), true);
 const beforePublication = await page.request.get(base + '/api/public/blog/' + publishedSlug);
 assert.equal((await beforePublication.json()).content, 'Latest edit made while saving');
 assert.equal(await page.frameLocator('#website-preview').locator('#cmsContent .cms-copy').textContent(), 'Reviewed change to the published article');
 await page.locator('#preview-mobile').click();
 assert.equal(await page.locator('#website-preview').evaluate(el => Math.round(el.getBoundingClientRect().width)), 390);
 await page.locator('#review-edit').click();
 assert.equal(await page.locator('#blog-form [name=content]').isEditable(), true);
 await page.locator('#save-blog').click();
 await page.waitForFunction(() => current?.hasPendingChanges && reviewedVersion === current.version);
 page.once('dialog', dialog => dialog.accept());
 await page.getByRole('button', {name:'Publish reviewed changes', exact:true}).click();
 await page.waitForFunction(() => !current?.hasPendingChanges && current?.status === 'Published');
 const afterPublication = await page.request.get(base + '/api/public/blog/' + publishedSlug);
 assert.equal((await afterPublication.json()).content, 'Reviewed change to the published article');
 // Unpublish and republish without another person's approval.
 page.once('dialog', dialog => dialog.accept());
 await page.locator('#workflow').getByRole('button', {name:'Unpublish', exact:true}).click();
 await page.waitForFunction(() => current?.status === 'Draft');
 await page.locator('#preview-blog').click();
 await page.waitForFunction(() => reviewedVersion === current.version);
 page.once('dialog', dialog => dialog.accept());
 await page.getByRole('button', {name:'Publish reviewed article', exact:true}).click();
 await page.waitForFunction(() => current?.status === 'Published');

 page.once('dialog', dialog => dialog.accept());
 await page.locator('#close-editor').click();
 assert.equal(await page.locator('#blog-workflow-summary').textContent(), 'Save → Preview → Publish');
 const articleRow = page.locator('#blog-rows tr').filter({hasText:title});
 await articleRow.getByRole('button', {name:'Edit article', exact:true}).waitFor({state:'visible'});
 page.once('dialog', dialog => dialog.accept());
 await articleRow.getByRole('button', {name:'Delete blog', exact:true}).click();
 await articleRow.waitFor({state:'detached'});
 await page.goto(base + '/admin/gallery');
 await page.waitForFunction(() => !document.querySelector('#album-new').disabled);
 await page.getByText('Single image', {exact:true}).waitFor({state:'visible'});
 await page.getByText('Bulk images', {exact:true}).waitFor({state:'visible'});
 await page.getByText('2. Album (optional)', {exact:true}).waitFor({state:'visible'});
 assert.equal(await page.locator('#album-select').inputValue(), '');
 await page.locator('#album-new').click();
 await page.locator('#album-form [name=title]').fill('Browser gallery');
 await page.locator('#album-form [name=slug]').fill('browser-gallery-' + Date.now());
 await page.locator('#album-form button[type=submit]').click();
 await page.waitForFunction(() => document.querySelector('#album-select').value !== '' && !document.querySelector('#album-select').disabled);
 const albumId = await page.locator('#album-select').inputValue();
 const uploads = []; page.on('request', request => { if (request.url().endsWith('/api/gallery/uploads') && request.method() === 'POST') uploads.push(request.postDataJSON()); });
 const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64');
 let fail = true, failNextBulk = false, activePuts = 0, peakPuts = 0;
 await page.route('http://127.0.0.1:9002/**', async route => {
  if (route.request().method() === 'PUT' && fail) { fail = false; await route.abort('failed'); }
  else if (route.request().method() === 'PUT' && failNextBulk) { failNextBulk = false; await route.abort('failed'); }
  else if (route.request().method() === 'PUT') { activePuts++; peakPuts = Math.max(peakPuts, activePuts); await new Promise(resolve => setTimeout(resolve, 500)); await route.continue(); activePuts--; }
  else await route.continue();
 });
 await page.locator('#gallery-files').setInputFiles({name:'browser.png',mimeType:'image/png',buffer:png});
 await page.waitForFunction(() => document.querySelector('#gallery-file-previews img')?.naturalWidth > 0);
 assert.equal(await page.locator('#gallery-file-previews article').count(), 1);
 assert.equal(await page.locator('#album-select').getAttribute('required'), null);
 await page.locator('#gallery-upload').click();
 await page.waitForFunction(() => document.querySelector('#upload-queue').textContent.includes('Failed:'));
 await page.waitForFunction(() => document.querySelector('#upload-queue button'));
 assert.match(await page.locator('#gallery-upload-status').textContent(), /1 failed/);
 await page.getByRole('button',{name:'Retry photo'}).click();
 await page.waitForFunction(() => document.querySelector('#upload-queue').textContent.includes('Uploaded · draft'));
 await page.waitForFunction(() => document.querySelectorAll('#gallery-images article').length === 1);
 await page.locator('#select-photos').click(); await page.locator('#publish-photos').click();
 await page.waitForFunction(() => document.querySelector('#gallery-images').textContent.includes('Published photo'));
 await page.getByRole('button',{name:'Use as album cover'}).click();
 await page.waitForFunction(() => document.querySelector('#gallery-images').textContent.includes('Current album cover'));
 // Bulk upload with an album: both files must keep the chosen album ID.
 const fileChooserPromise = page.waitForEvent('filechooser');
 await page.getByRole('button',{name:'Bulk Upload',exact:true}).click();
 const chooser = await fileChooserPromise; assert.equal(chooser.isMultiple(), true);
 await chooser.setFiles([
  {name:'album-a.png',mimeType:'image/png',buffer:png},
  {name:'album-b.png',mimeType:'image/png',buffer:png},
  {name:'album-discard.png',mimeType:'image/png',buffer:png}
 ]);
 await page.waitForFunction(() => document.querySelectorAll('#gallery-file-previews img').length === 3 && Array.from(document.querySelectorAll('#gallery-file-previews img')).every(img => img.naturalWidth > 0));
 await page.getByRole('button',{name:'Remove album-discard.png',exact:true}).click();
 assert.equal(await page.locator('#gallery-file-previews article').count(), 2);
 await page.screenshot({path:'test-results/gallery-bulk-selection.png',fullPage:true});
 failNextBulk = true;
 await page.locator('#gallery-upload').click();
 await page.waitForFunction(() => document.querySelector('#gallery-upload-status').textContent.includes('1 uploaded successfully; 1 failed'));
 const beforeRetry = await page.request.get(base + '/api/gallery/albums/' + albumId + '/images');
 const completedBeforeRetry = (await beforeRetry.json()).items.filter(item => item.isComplete).map(item => item.id);
 assert.equal(completedBeforeRetry.length, 2); // Original image plus the successful batch image.
 const authorizationsBeforeRetry = uploads.length;
 await page.getByRole('button',{name:'Retry photo',exact:true}).click();
 await page.waitForFunction(() => document.querySelectorAll('#gallery-images article').length === 3 && document.querySelector('#gallery-upload-status').textContent.includes('2 uploaded successfully'));
 assert.equal(uploads.length, authorizationsBeforeRetry); // Retry reuses the failed record.
 const afterRetry = await page.request.get(base + '/api/gallery/albums/' + albumId + '/images');
 const completedAfterRetry = (await afterRetry.json()).items.filter(item => item.isComplete).map(item => item.id);
 assert.equal(completedAfterRetry.length, 3);
 assert.ok(completedBeforeRetry.every(id => completedAfterRetry.includes(id)));
 assert.deepEqual(uploads.slice(-2).map(x => x.albumId), [albumId,albumId]);
 assert.ok(!uploads.some(x => x.fileName === 'album-discard.png'));
 await page.locator('#album-select').selectOption('');
 await page.waitForFunction(() => document.querySelector('#album-form').hidden);
 await page.locator('[name=uploadMode][value=single]').check();
 assert.equal(await page.locator('#gallery-files').getAttribute('multiple'), null);
 // A single image succeeds with no album selected.
 await page.locator('#gallery-files').setInputFiles({name:'standalone-single.png',mimeType:'image/png',buffer:png});
 await page.waitForFunction(() => document.querySelector('#gallery-file-previews img')?.naturalWidth > 0);
 await page.screenshot({path:'test-results/gallery-single-selection.png',fullPage:true});
 await page.locator('#gallery-upload').click();
 await page.waitForFunction(() => document.querySelectorAll('#gallery-images article').length === 1);
 assert.equal(uploads.at(-1).albumId, null);
 await page.locator('[name=uploadMode][value=bulk]').check();
 assert.notEqual(await page.locator('#gallery-files').getAttribute('multiple'), null);
 await page.locator('#gallery-files').setInputFiles([
  {name:'single-a.png',mimeType:'image/png',buffer:png},
  {name:'single-b.png',mimeType:'image/png',buffer:png},
  {name:'standalone-discard.png',mimeType:'image/png',buffer:png}
 ]);
 await page.waitForFunction(() => document.querySelectorAll('#gallery-file-previews img').length === 3);
 await page.getByRole('button',{name:'Remove standalone-discard.png',exact:true}).click();
 assert.equal(await page.locator('#gallery-file-previews article').count(), 2);
 await page.locator('#gallery-upload').click();
 await page.waitForFunction(() => document.querySelectorAll('#gallery-images article').length === 3);
 assert.deepEqual(uploads.slice(-2).map(x => x.albumId), [null,null]);
 assert.ok(!uploads.some(x => x.fileName === 'standalone-discard.png'));
 assert.match(await page.locator('#gallery-progress-label').textContent(), /2 of 2 uploaded/);
 assert.equal(await page.locator('#gallery-upload').isDisabled(), true);
 await page.locator('#select-photos').click(); await page.locator('#publish-photos').click();
 await page.waitForFunction(() => Array.from(document.querySelectorAll('#gallery-images .badge')).every(x => x.textContent === 'Published photo'));
 assert.ok(peakPuts >= 2 && peakPuts <= 3, 'Bulk files should upload concurrently within the worker limit');
 // A clicked photo opens its own editor, and saving must not affect its neighbours.
 await page.getByRole('button',{name:'Edit photo: standalone single',exact:true}).click();
 await page.locator('#gallery-image-form [name=altText]').fill('Church worship revised');
 await page.locator('#gallery-image-form [name=caption]').fill('Sunday service caption');
 await page.locator('#gallery-editor-save').click();
 await page.waitForFunction(() => !document.querySelector('#gallery-image-editor').open);
 const publicPhotos = await page.request.get(base + '/api/public/gallery/images');
 const publicItems = (await publicPhotos.json()).items;
 assert.equal(publicItems.filter(item => item.altText === 'Church worship revised' && item.caption === 'Sunday service caption').length, 1);
 assert.equal(publicItems.filter(item => item.altText === 'single a').length, 1);
 assert.equal(publicItems.filter(item => item.altText === 'single b').length, 1);
 // Publish standalone uploads and verify the actual public widget renders them.
 const popup = await browser.newPage();
 await popup.goto(base + '/public/gallery');
 await popup.waitForFunction(() => Array.from(document.querySelectorAll('.fhgm-gallery-card img')).some(img => img.alt === 'Church worship revised'));
 assert.equal(await popup.locator('.fhgm-gallery-card img[alt="single a"]').count(), 1);
 await popup.screenshot({path:'test-results/gallery-public-photos.png',fullPage:true});
 await popup.close();
 await page.goto(base + '/account/security');
 await page.waitForFunction(() => document.querySelector('#security-status').textContent.includes('Authenticator'));
 await browser.close();
 console.log('PASS: dashboard, article preview/autosave, upload failure/retry, bulk publication, cover selection and account security page');
})().catch(error => { console.error(error); process.exit(1); });
