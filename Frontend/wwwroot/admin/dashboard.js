(async () => {
  const root = document.getElementById('dashboard-summary'); if (!root) return;
  try {
    const data = await api('dashboard'); root.replaceChildren();
    for (const [key, title, href] of [['drafts','Articles to finish','/admin/blogs'],['awaitingReview','Articles awaiting review','/admin/blogs'],['unpublishedPhotos','Photos awaiting publication','/admin/gallery'],['incompleteUploads','Incomplete uploads','/admin/gallery']]) {
      if (data[key] == null) continue;
      const card = document.createElement('a'); card.className = 'panel p-4'; card.href = href; card.textContent = `${data[key]} · ${title}`; root.append(card);
    }
    if (data.upcomingEvents) {
      const section = document.createElement('section'); section.className = 'panel p-4'; const h = document.createElement('h2'); h.textContent = 'Next events'; section.append(h);
      for (const event of data.upcomingEvents) { const p = document.createElement('p'); p.textContent = `${event.title} · ${new Date(event.startsAt).toLocaleString('en-ZA', {timeZone:'Africa/Johannesburg'})} SAST · ${event.isPublished ? 'Published' : 'Draft'}`; section.append(p); }
      if (!data.upcomingEvents.length) section.append(document.createTextNode('No upcoming events.'));
      root.append(section);
    }
  } catch(e) { root.textContent = e.message; }
})();
