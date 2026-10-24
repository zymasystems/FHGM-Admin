"""Runs the existing MVC/API suite, then the gallery suite against a local Moto S3 server.
Never supply a production endpoint/bucket. The runner must initialize the disposable S3 bucket.
"""
import base64
import json
import os
import urllib.request
import integration as suite

admin, media, writer, public = suite.admin, suite.media, suite.other, suite.anonymous
admin.login(os.environ["Bootstrap__Email"], os.environ["Bootstrap__Password"])
writer.request("/api/gallery/albums", expected=403)
writer.request("/admin/gallery", expected=403)
assert "Upload images" in media.request("/admin/gallery")
assert media.request("/api/gallery/storage")["configured"] is True
album_body = {"title": "Sunday Service", "slug": suite.PREFIX + "-sunday", "description": "Ministry moments", "isPublished": False}
album = media.request("/api/gallery/albums", "POST", album_body, expected=201)
album_path = "/api/gallery/albums/" + album["id"]
public_path = "/api/public/gallery/albums/" + album["slug"]
public.request(public_path, expected=404)
media.request("/api/gallery/albums", "POST", album_body, expected=409)
media.request(album_path, "PUT", {**album_body, "version": 999}, expected=409)
valid = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=")
upload_body = {"albumId": album["id"], "fileName": "sunday.png", "contentType": "image/png", "size": len(valid), "altText": "Worship at Sunday service"}
writer.request("/api/gallery/uploads", "POST", upload_body, expected=403)
media.request("/api/gallery/uploads", "POST", {**upload_body, "contentType": "image/svg+xml", "fileName": "bad.svg"}, expected=400)
media.request("/api/gallery/uploads", "POST", {**upload_body, "size": 10485761}, expected=400)
media.request("/api/gallery/uploads", "POST", {**upload_body, "fileName": "bad.html"}, expected=400)
upload = media.request("/api/gallery/uploads", "POST", upload_body, expected=201)
assert "X-Amz-Signature=" in upload["uploadUrl"] and "/staging/" in upload["uploadUrl"]
image_path = "/api/gallery/images/" + upload["id"]
media.request(image_path + "/complete", "POST", {}, expected=409)
media.request(image_path, "PUT", {"altText": "Sunday worship", "caption": "", "isPublished": True, "version": 1}, expected=409)
def put(url, data):
    req = urllib.request.Request(url, data=data, method="PUT", headers={"Content-Type": "image/png"})
    with urllib.request.urlopen(req, timeout=20) as response:
        assert response.status == 200
put(upload["uploadUrl"], valid)
result = media.request(image_path + "/complete", "POST", {})
assert result["version"] == 2
# A completed image is idempotent and stays hidden until separately published.
media.request(image_path + "/complete", "POST", {})
assert not any(a["slug"] == album["slug"] for a in public.request("/api/public/gallery/albums")["items"])
album = media.request(album_path, "PUT", {**album_body, "isPublished": True, "version": album["version"]})
assert public.request(public_path)["items"] == []
image = media.request(image_path, "PUT", {"altText": "Sunday worship", "caption": "Our church family", "isPublished": True, "version": 2})
published = public.request(public_path)
assert len(published["items"]) == 1 and published["items"][0]["altText"] == "Sunday worship"
public_url = published["items"][0]["url"]
with urllib.request.urlopen(public_url, timeout=20) as response: assert response.read() == valid
# Reusing a PUT URL can change only the staging object, never the verified gallery object.
put(upload["uploadUrl"], b"not an image".ljust(len(valid), b"!"))
with urllib.request.urlopen(public_url, timeout=20) as response: assert response.read() == valid
assert any(a["slug"] == album["slug"] for a in public.request("/api/public/gallery/albums")["items"])
media.request(image_path, "PUT", {"altText": "Sunday worship", "caption": "", "isPublished": False, "version": 1}, expected=409)
image = media.request(image_path, "PUT", {"altText": "Sunday worship", "caption": "", "isPublished": False, "version": image["version"]})
assert public.request(public_path)["items"] == []
image = media.request(image_path, "PUT", {"altText": "Sunday worship", "caption": "", "isPublished": True, "version": image["version"]})
album = media.request(album_path, "PUT", {**album_body, "isPublished": False, "version": album["version"]})
public.request(public_path, expected=404)
media.request(image_path, "DELETE", {}, expected=202)
assert media.request(album_path + "/images")["total"] == 0
# Claimed image MIME/extension are insufficient: file signature is checked by the backend.
bad = media.request("/api/gallery/uploads", "POST", {**upload_body, "fileName": "fake.png"}, expected=201)
put(bad["uploadUrl"], b"<script>bad()</script>".ljust(len(valid), b" "))
media.request("/api/gallery/images/" + bad["id"] + "/complete", "POST", {}, expected=400)
media.request("/api/gallery/images/" + bad["id"], "DELETE", {}, expected=202)
actions = admin.request("/api/audit")["items"]
assert any(a["action"] == "ImageUploadCompleted" for a in actions)
assert any(a["action"] == "ImageRemoved" for a in actions)
print(f"PASS: {suite.checks} HTTP checks including gallery roles, signed PUTs, file verification, immutable final images, publication, removal and audit.")
