"""Run only against the disposable CI database. Includes existing suites."""
import base64
import hashlib
import hmac
import struct
import time
import datetime as dt
import church_content_integration as church_checks
from integration import admin, anonymous, other as writer, media, Client, PASSWORD, PREFIX

church = church_checks.church
comms = church_checks.comms
assert writer.request('/api/dashboard')['unpublishedPhotos'] is None
assert media.request('/api/dashboard')['drafts'] is None
assert admin.request('/api/dashboard')['drafts'] is not None
assert church.request('/api/dashboard')['upcomingEvents'] is not None
media.request('/api/gallery/storage/probe', 'POST', {}, expected=403)
assert 'succeeded' in admin.request('/api/gallery/storage/probe', 'POST', {})['message']

album = media.request('/api/gallery/albums', 'POST', {'title': PREFIX+' Searchable album', 'slug': PREFIX+'-search'}, expected=201)
assert any(x['id'] == album['id'] for x in media.request('/api/gallery/albums?search='+PREFIX)['items'])
media.request('/api/gallery/albums/'+album['id'], 'PUT', {'title':'Album', 'slug':PREFIX+'-search','version':album['version'],'coverImageId':'00000000-0000-0000-0000-000000000001'}, expected=400)

now = dt.datetime.now(dt.timezone.utc)
event = {'title':'Weekly gathering','slug':PREFIX+'-weekly','description':'Join us','location':'Church','startsAt':(now+dt.timedelta(days=1)).isoformat(),'endsAt':(now+dt.timedelta(days=1,hours=1)).isoformat(),'isPublished':True,'occurrences':3,'featuredImageUrl':'https://example.org/photo.jpg'}
first = church.request('/api/events','POST',event,expected=201)
items = church.request('/api/events')['items']
series = [x for x in items if x['slug'].startswith(PREFIX+'-weekly')]
assert len(series) == 3
starts = sorted(dt.datetime.fromisoformat(x['startsAt']) for x in series)
assert starts[1]-starts[0] == starts[2]-starts[1] == dt.timedelta(days=7)
public = anonymous.request('/api/public/events/'+first['slug'])
assert public['featuredImageUrl'] == event['featuredImageUrl']
church.request('/api/events','POST',{**event,'slug':PREFIX+'-invalid','occurrences':53},expected=400)
church.request('/api/events','POST',{**event,'slug':PREFIX+'-unsafe','featuredImageUrl':'javascript:alert(1)'},expected=400)

post = writer.request('/api/blog','POST',{'title':'Formatted','slug':PREFIX+'-formatted','excerpt':'Preview','content':'## Heading\n**Bold**','contentFormat':'Markdown','featuredImageUrl':'https://example.org/photo.jpg'},expected=201)
assert post['contentFormat'] == 'Markdown'
writer.request('/api/blog','POST',{'title':'Unsafe','slug':PREFIX+'-unsafe-blog','excerpt':'Preview','content':'Text','contentFormat':'HTML'},expected=400)

# Existing initial-password helper already verifies backend enforcement and performs a password change.
assert church.request('/api/security')['mustChangePassword'] is False
setup = church.request('/api/security/mfa/setup','POST',{'password':PASSWORD})
def otp(key):
    key = base64.b32decode(key + '=' * ((8-len(key)%8)%8))
    digest = hmac.new(key,struct.pack('>Q',int(time.time())//30),hashlib.sha1).digest()
    offset = digest[-1]&15
    return str((struct.unpack('>I',digest[offset:offset+4])[0]&0x7fffffff)%1000000).zfill(6)
result = church.request('/api/security/mfa/enable','POST',{'password':PASSWORD,'code':otp(setup['key'])})
codes = result['recoveryCodes']; assert len(codes) == 10
church.token = church.request('/api/auth/csrf')['token']
assert church.request('/api/security')['twoFactorEnabled'] is True
church.request('/api/auth/logout','POST',{},expected=204)
# Let the deliberately strict shared login limiter reset before testing authentication failures.
time.sleep(60)
client = Client(); email = church_checks.church_user['email']
client.request('/api/auth/login','POST',{'email':email,'password':PASSWORD},expected=401)
client.request('/api/auth/login','POST',{'email':email,'password':PASSWORD,'twoFactorCode':otp(setup['key'])})
client.token = client.request('/api/auth/csrf')['token']
assert client.request('/api/security')['twoFactorEnabled']
client.request('/api/auth/logout','POST',{},expected=204)
client.token = client.request('/api/auth/csrf')['token']
client.request('/api/auth/login','POST',{'email':email,'password':PASSWORD,'recoveryCode':codes[0]})
client.token = client.request('/api/auth/csrf')['token']
assert client.request('/api/security')['recoveryCodesRemaining'] == 9
client.request('/api/auth/logout','POST',{},expected=204)
client.token = client.request('/api/auth/csrf')['token']
client.request('/api/auth/login','POST',{'email':email,'password':PASSWORD,'recoveryCode':codes[0]},expected=401)
assert anonymous.request('/api/security/forgot','POST',{'email':'unknown@example.org'})['message']
anonymous.request('/api/security/reset','POST',{'email':email,'token':'invalid','newPassword':'Different-Password-739!'},expected=400)
print('PASS: portal summary permissions, storage probe, album search/cover validation, recurring events, formatted article validation, initial password change, MFA and one-use recovery codes.')
