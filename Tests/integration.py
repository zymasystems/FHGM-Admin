"""Run only against a disposable initialized database; creates users and articles.
Uses Python's standard library. Environment credentials must match the bootstrap account.
"""
import http.cookiejar
import json
import os
import uuid
import urllib.error
import urllib.request

BASE = os.environ.get("FHGM_TEST_URL", "http://127.0.0.1:5080")
PREFIX = uuid.uuid4().hex[:12]
PASSWORD = "Integration-Only-739!"
checks = 0


class Client:
    def __init__(self, base=BASE):
        self.base = base
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.token = None

    def request(self, path, method="GET", body=None, expected=200, csrf=True):
        global checks
        headers = {"Accept": "application/json"}
        if method != "GET":
            headers["Content-Type"] = "application/json"
            if csrf:
                if not self.token:
                    self.token = self.request("/api/auth/csrf")["token"]
                headers["X-CSRF-TOKEN"] = self.token
        req = urllib.request.Request(self.base + path, method=method, headers=headers,
                                     data=None if body is None else json.dumps(body).encode())
        try:
            response = self.opener.open(req, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        raw = response.read().decode()
        assert response.code == expected, f"{method} {path}: expected {expected}, got {response.code}: {raw[:800]}"
        checks += 1
        return json.loads(raw) if raw and "json" in response.headers.get("Content-Type", "") else raw

    def login(self, email, password):
        self.token = self.request("/api/auth/csrf")["token"]
        result = self.request("/api/auth/login", "POST", {"email": email, "password": password})
        self.token = self.request("/api/auth/csrf")["token"]
        return result


def create_user(admin, role, suffix):
    return admin.request("/api/users", "POST", {
        "email": f"{PREFIX}-{suffix}@example.org", "password": PASSWORD,
        "displayName": suffix, "role": role,
    }, expected=201)


anonymous, admin, writer, other, editor, media = [Client() for _ in range(6)]
anonymous.request("/health/ready")
assert "Welcome back" in anonymous.request("/admin/")
assert "Stories of faith" in anonymous.request("/public/")
anonymous.request("/api/blog", expected=401)
anonymous.request("/api/users", expected=401)
anonymous.request("/api/auth/login", "POST", {"email": "nobody@example.org", "password": PASSWORD}, expected=400, csrf=False)
admin.login(os.environ["Bootstrap__Email"], os.environ["Bootstrap__Password"])
w = create_user(admin, "Writer", "writer")
o = create_user(admin, "Writer", "other")
e = create_user(admin, "BlogEditor", "editor")
m = create_user(admin, "MediaManager", "media")
for client, user in [(writer, w), (other, o), (editor, e), (media, m)]:
    client.login(user["email"], PASSWORD)
assert "Blog articles" in writer.request("/admin/blogs")
assert "Team members" in admin.request("/admin/users")
assert "Activity log" in admin.request("/admin/audit")
writer.request("/admin/users", expected=403)
writer.request("/admin/audit", expected=403)
media.request("/admin/blogs", expected=403)
assert "Portal development" in media.request("/admin/")
# Typed HttpClient must never share a cookie jar between MVC users.
assert writer.request("/api/auth/me")["id"] == w["id"]
assert admin.request("/api/auth/me")["email"] == os.environ["Bootstrap__Email"]
assert "Stories of faith" in anonymous.request("/public/")
backend = Client(os.environ.get("FHGM_BACKEND_TEST_URL", "http://127.0.0.1:5081"))
backend.request("/", expected=404)
backend.request("/api/blog", expected=401)
writer.request("/api/users", expected=403)
media.request("/api/blog", expected=403)
body = {"title": "A story of grace", "slug": PREFIX + "-grace", "excerpt": "A brief reflection.",
        "content": "First paragraph.\n\n<script>alert('text only')</script>", "category": "Faith", "version": 1}
post = writer.request("/api/blog", "POST", body, expected=201)
assert "author" not in post and "passwordHash" not in json.dumps(post)
path = "/api/blog/" + post["id"]
public_path = "/api/public/blog/" + body["slug"]
anonymous.request(public_path, expected=404)
other.request(path, expected=404)
other.request(path, "PUT", body, expected=404)
assert all(item["id"] != post["id"] for item in other.request("/api/blog")["items"])
writer.request(path + "/workflow", "POST", {"action": "publish", "version": post["version"]}, expected=403)
editor.request(path + "/workflow", "POST", {"action": "publish", "version": post["version"]}, expected=409)
writer.request(path, "PUT", {**body, "version": 999}, expected=409)
writer.request("/api/blog", "POST", body, expected=409)
post = writer.request(path + "/workflow", "POST", {"action": "submit", "version": post["version"]})
writer.request(path, "PUT", {**body, "version": post["version"]}, expected=409)
editor.request(path + "/workflow", "POST", {"action": "request-changes", "version": post["version"]}, expected=400)
post = editor.request(path + "/workflow", "POST", {"action": "request-changes", "version": post["version"], "note": "Please add more context."})
post = writer.request(path, "PUT", {**body, "version": post["version"], "content": "Revised article, ready for review."})
post = writer.request(path + "/workflow", "POST", {"action": "submit", "version": post["version"]})
post = editor.request(path + "/workflow", "POST", {"action": "approve", "version": post["version"]})
anonymous.request(public_path, expected=404)
writer.request(path + "/workflow", "POST", {"action": "publish", "version": post["version"]}, expected=403)
post = editor.request(path + "/workflow", "POST", {"action": "publish", "version": post["version"]})
published = anonymous.request(public_path)
assert published["content"] == "Revised article, ready for review."
assert "authorId" not in published and "reviewNote" not in published
assert any(item["slug"] == body["slug"] for item in anonymous.request("/api/public/blog")["items"])
editor.request(path, "PUT", {**body, "version": post["version"]}, expected=409)
post = editor.request(path + "/workflow", "POST", {"action": "archive", "version": post["version"]})
anonymous.request(public_path, expected=404)
post = editor.request(path + "/workflow", "POST", {"action": "restore", "version": post["version"]})
assert post["status"] == "Draft" and post["approvedAt"] is None and post["publishedAt"] is None
admin.request("/api/users/" + w["id"] + "/active", "PUT", {"isActive": False}, expected=204)
writer.request("/api/blog", expected=401)
assert "Welcome back" in writer.request("/admin/blogs")
writer.token = writer.request("/api/auth/csrf")["token"]
writer.request("/api/auth/login", "POST", {"email": w["email"], "password": PASSWORD}, expected=401)
events = admin.request("/api/audit")["items"]
assert any(item["action"] == "Blog:publish" and item["entityId"] == post["id"] for item in events)
assert any(item["action"] == "UserDisabled" and item["entityId"] == w["id"] for item in events)
editor.request("/api/audit", expected=403)
admin.request("/api/auth/logout", "POST", {}, expected=204)
admin.request("/api/users", expected=401)
print(f"PASS: {checks} HTTP checks plus MVC access, session isolation, content, workflow, ownership and audit assertions.")
