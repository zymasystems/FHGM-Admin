(async () => {
  const root = document.getElementById('dashboard-summary'); if (!root) return;
  try {
    const data = await api('dashboard'); root.replaceChildren();
    for (const [key, title, href, detail, icon] of [
      ['drafts','Articles to finish','/admin/blogs','Drafts and requested changes','✎'],
      ['awaitingReview','Awaiting review','/admin/blogs','Submitted articles','◷'],
      ['unpublishedPhotos','Photos to publish','/admin/gallery','Completed gallery uploads','▧'],
      ['incompleteUploads','Incomplete uploads','/admin/gallery','Uploads that need attention','↥']
    ]) {
      if (data[key] == null) continue;
      const card = document.createElement('a'); card.className = 'dashboard-stat-card'; card.href = href;
      const iconNode = document.createElement('span'); iconNode.className = 'dashboard-stat-icon'; iconNode.setAttribute('aria-hidden', 'true'); iconNode.textContent = icon;
      const count = document.createElement('strong'); count.className = 'dashboard-stat-count'; count.textContent = data[key];
      const label = document.createElement('span'); label.className = 'dashboard-stat-label'; label.textContent = title;
      const caption = document.createElement('span'); caption.className = 'dashboard-stat-caption'; caption.textContent = detail;
      const arrow = document.createElement('span'); arrow.className = 'dashboard-stat-arrow'; arrow.setAttribute('aria-hidden', 'true'); arrow.textContent = '→';
      card.append(iconNode, count, label, caption, arrow); root.append(card);
    }
    if (!root.children.length && !data.upcomingEvents) {
      const empty = document.createElement('div'); empty.className = 'dashboard-empty'; empty.textContent = 'Your workspace summary will appear here when content tools are available.'; root.append(empty);
    }
    if (data.upcomingEvents) {
      const section = document.createElement('section'); section.className = 'dashboard-events';
      const heading = document.createElement('div'); heading.className = 'dashboard-events-heading';
      const title = document.createElement('div'); const eyebrow = document.createElement('p'); eyebrow.className = 'eyebrow'; eyebrow.textContent = 'COMING UP';
      const headingText = document.createElement('h2'); headingText.textContent = 'Upcoming events'; title.append(eyebrow, headingText);
      const link = document.createElement('a'); link.href = '/admin/events'; link.textContent = 'Manage events →';
      heading.append(title, link); section.append(heading);
      if (data.upcomingEvents.length) {
        const list = document.createElement('div'); list.className = 'dashboard-event-list';
        for (const event of data.upcomingEvents) {
          const item = document.createElement('div'); item.className = 'dashboard-event';
          const date = document.createElement('time'); date.className = 'dashboard-event-date';
          const eventDate = new Date(event.startsAt);
          date.dateTime = eventDate.toISOString();
          date.textContent = eventDate.toLocaleDateString('en-ZA', {day:'2-digit', month:'short', timeZone:'Africa/Johannesburg'});
          const details = document.createElement('div'); details.className = 'dashboard-event-details';
          const name = document.createElement('strong'); name.textContent = event.title;
          const time = document.createElement('span'); time.textContent = eventDate.toLocaleTimeString('en-ZA', {hour:'2-digit', minute:'2-digit', timeZone:'Africa/Johannesburg'}) + ' SAST';
          const status = document.createElement('span'); status.className = `dashboard-event-status ${event.isPublished ? 'is-published' : ''}`; status.textContent = event.isPublished ? 'Published' : 'Draft';
          details.append(name, time); item.append(date, details, status); list.append(item);
        }
        section.append(list);
      } else {
        const empty = document.createElement('p'); empty.className = 'dashboard-events-empty'; empty.textContent = 'No upcoming events. When you schedule one, it will appear here.'; section.append(empty);
      }
      root.append(section);
    }
  } catch(e) {
    root.replaceChildren();
    const error = document.createElement('div'); error.className = 'dashboard-error'; error.textContent = `Unable to load your content summary. ${e.message}`; root.append(error);
  }
})();
