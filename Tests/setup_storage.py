"""Creates ONLY the loopback S3 emulator bucket used by CI/local tests."""
import json
import boto3
s3 = boto3.client("s3", endpoint_url="http://127.0.0.1:9002", aws_access_key_id="test", aws_secret_access_key="test", region_name="us-east-1")
s3.create_bucket(Bucket="fhgm-test")
s3.put_bucket_cors(Bucket="fhgm-test", CORSConfiguration={"CORSRules": [{
    "AllowedOrigins": ["http://127.0.0.1:5080", "http://localhost:5080"], "AllowedMethods": ["PUT", "GET", "HEAD"],
    "AllowedHeaders": ["Content-Type"], "ExposeHeaders": ["ETag"]}]})
s3.put_bucket_policy(Bucket="fhgm-test", Policy=json.dumps({"Version":"2012-10-17", "Statement":[{
    "Effect":"Allow", "Principal":"*", "Action":"s3:GetObject", "Resource":"arn:aws:s3:::fhgm-test/gallery/*"}]}))
print("Disposable S3 emulator bucket ready.")
