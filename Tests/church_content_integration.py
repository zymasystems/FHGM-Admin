"""Includes core/gallery suites, then checks church/developer boundaries and scheduled content."""
import datetime as dt
import time
import gallery_integration
from integration import Client, PASSWORD, PREFIX, admin, anonymous
import integration as checks

def iso(value):
    return value.isoformat()

now = dt.datetime.now(dt.timezone.utc)
church_user = admin.request('/api/users', 'POST', {'email': PREFIX + '-church@example.org', 'password': PASSWORD, 'displayName': 'Church Office', 'role': 'ChurchAdmin'}, expected=201)
comms_user = admin.request('/api/users', 'POST', {'email': PREFIX + '-comms@example.org', 'password': PASSWORD, 'displayName': 'Communications', 'role': 'CommunicationsManager'}, expected=201)
church, comms = Client(), Client()
church.login(church_user['email'], PASSWORD)
comms.login(comms_user['email'], PASSWORD)
for route in ['/admin/gallery', '/admin/blogs', '/admin/events', '/admin/popups']:
    church.request(route)
for route in ['/admin/users', '/admin/monitoring', '/admin/audit', '/api/users', '/api/monitoring', '/api/audit']:
    church.request(route, expected=403)
    comms.request(route, expected=403)
church.request('/api/users', 'POST', {'email': PREFIX + '-escalate@example.org', 'password': PASSWORD, 'displayName': 'Escalation', 'role': 'SuperAdmin'}, expected=403)
anonymous.request('/api/monitoring', expected=401)
monitoring = admin.request('/api/monitoring')
assert monitoring['status'] == 'Healthy' and monitoring['counts']['users'] >= 7
assert monitoring['uptimeSeconds'] >= 0
admin.request('/admin/monitoring')
church.request('/api/gallery/albums')
church.request('/api/blog')
comms.request('/api/events')
comms.request('/api/popups')
comms.request('/api/gallery/albums', expected=403)

event_body = {'title': 'Church gathering', 'slug': PREFIX + '-gathering', 'description': 'A church community event.', 'location': 'Main church hall',
              'startsAt': iso(now + dt.timedelta(days=1)), 'endsAt': iso(now + dt.timedelta(days=1, hours=2)), 'isPublished': False, 'version': 1}
church.request('/api/events', 'POST', {**event_body, 'endsAt': event_body['startsAt']}, expected=400)
event = church.request('/api/events', 'POST', event_body, expected=201)
event_path = '/api/events/' + event['id']
public_path = '/api/public/events/' + event['slug']
anonymous.request(public_path, expected=404)
church.request('/api/events', 'POST', event_body, expected=409)
church.request(event_path, 'PUT', {**event_body, 'version': 999}, expected=409)
event = church.request(event_path, 'PUT', {**event_body, 'isPublished': True, 'version': event['version']})
assert anonymous.request(public_path)['title'] == 'Church gathering'
assert any(x['slug'] == event['slug'] for x in anonymous.request('/api/public/events')['items'])
expired = church.request('/api/events', 'POST', {**event_body, 'slug': PREFIX + '-expired', 'startsAt': iso(now-dt.timedelta(days=2)), 'endsAt': iso(now-dt.timedelta(days=1)), 'isPublished': True}, expected=201)
anonymous.request('/api/public/events/' + expired['slug'], expected=404)
assert any(x['id'] == expired['id'] for x in church.request('/api/events')['items'])
# A published event expires just from the clock moving; no update/job is needed.
near = dt.datetime.now(dt.timezone.utc)
short = church.request('/api/events', 'POST', {**event_body, 'slug': PREFIX + '-short', 'startsAt': iso(near-dt.timedelta(minutes=1)), 'endsAt': iso(near+dt.timedelta(seconds=4)), 'isPublished': True}, expected=201)
anonymous.request('/api/public/events/' + short['slug'])
time.sleep(4.5)
anonymous.request('/api/public/events/' + short['slug'], expected=404)
church.request(event_path, 'DELETE', expected=400)
church.request(event_path + '?version=1', 'DELETE', expected=409)
church.request(event_path + '?version=' + str(event['version']), 'DELETE', expected=204)
anonymous.request(public_path, expected=404)

popup_body = {'title': 'Conference registration', 'message': 'Registration is open.', 'startsAt': iso(now-dt.timedelta(hours=1)), 'endsAt': iso(now+dt.timedelta(days=1)),
              'isPublished': False, 'priority': 10, 'buttonText': 'Register', 'buttonUrl': 'https://hisgraceministries.co.za/events.html', 'version': 1}
for url in ['javascript:alert(1)', '//evil.example/', '/\\evil.example/', 'http://example.org']:
    church.request('/api/popups', 'POST', {**popup_body, 'buttonUrl': url}, expected=400)
church.request('/api/popups', 'POST', {**popup_body, 'buttonText': ''}, expected=400)
popup = church.request('/api/popups', 'POST', popup_body, expected=201)
popup_path = '/api/popups/' + popup['id']
assert not any(x['id'] == popup['id'] for x in anonymous.request('/api/public/popups/active')['items'])
popup = church.request(popup_path, 'PUT', {**popup_body, 'isPublished': True, 'version': popup['version']})
assert anonymous.request('/api/public/popups/active')['items'][0]['id'] == popup['id']
future = comms.request('/api/popups', 'POST', {**popup_body, 'title': 'Future', 'startsAt': iso(now+dt.timedelta(days=1)), 'endsAt': iso(now+dt.timedelta(days=2)), 'isPublished': True}, expected=201)
old = comms.request('/api/popups', 'POST', {**popup_body, 'title': 'Expired', 'startsAt': iso(now-dt.timedelta(days=2)), 'endsAt': iso(now-dt.timedelta(days=1)), 'isPublished': True}, expected=201)
visible = anonymous.request('/api/public/popups/active')['items']
assert all(x['id'] not in [future['id'], old['id']] for x in visible)
popup = church.request(popup_path, 'PUT', {**popup_body, 'isPublished': False, 'version': popup['version']})
assert not any(x['id'] == popup['id'] for x in anonymous.request('/api/public/popups/active')['items'])
church.request(popup_path + '?version=1', 'DELETE', expected=409)
church.request(popup_path + '?version=' + str(popup['version']), 'DELETE', expected=204)
church.request('/public/events')
actions = admin.request('/api/audit')['items']
assert any(x['action'] == 'EventDeleted' for x in actions)
assert any(x['action'] == 'PopupDeleted' for x in actions)
print(f'PASS: {checks.checks} HTTP checks including ChurchAdmin boundaries, developer monitoring, events, expiry and popup schedules.')
