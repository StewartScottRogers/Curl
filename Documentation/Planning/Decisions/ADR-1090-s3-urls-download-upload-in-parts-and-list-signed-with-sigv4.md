# ADR-1090: s3:// URLs download, upload in parts and list, signed with SigV4

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4124
- Decided by Claude under Stewart's delegation.

## Context

The S3 extension (ADR-0999) had its SigV4 signer (ADR-1013), its XML bodies and its HTTP/1.1 client
(ADR-1018), but no handler. It is tested against the Amazon S3 REST API reference (API version
2006-03-01: GetObject, PutObject, ListObjectsV2, CreateMultipartUpload, UploadPart,
CompleteMultipartUpload, "Error responses") and the user guide's "Virtual hosting of buckets", not
against curl, and must never change what curl does.

## Decision

- **Scheme `s3`**, URL `s3://bucket/key`; no new options. `--user ACCESS:SECRET` signs; without it
  `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` (with `AWS_SESSION_TOKEN` as
  `x-amz-security-token`) sign; with neither the requests go unsigned, for public buckets. These are
  the variable names the AWS CLI and SDKs read, so a user's environment works unchanged.
- **Region** from `AWS_REGION`, then `AWS_DEFAULT_REGION`, else `us-east-1`.
- **Endpoint**: on AWS, `https://bucket.s3.region.amazonaws.com` (virtual-hosted), or path-style on
  `s3.region.amazonaws.com` when the bucket holds a dot (the wildcard certificate does not cover it).
  `AWS_ENDPOINT_URL_S3`, then `AWS_ENDPOINT_URL`, names another service (MinIO, R2), always addressed
  path-style, `http` or `https`; anything else fails with exit 3.
- **What a URL does**: with `-T`, upload; an empty key or one ending in `/` lists (ListObjectsV2 with
  delimiter `/`, every page by continuation token, common prefixes then keys, one per line); otherwise
  GetObject to the output. An upload to an empty key or a prefix fails with exit 3 rather than send a
  PUT to the bucket, which would be CreateBucket.
- **Multipart**: an upload of 8 MiB or more (the AWS CLI's `multipart_threshold` and
  `multipart_chunksize`) goes as CreateMultipartUpload, 8 MiB UploadParts and CompleteMultipartUpload;
  a shorter one is one PutObject. Each part is read into memory so its SHA-256 can be signed, which also
  allows an upload from a pipe of unknown length. A 200 to CompleteMultipartUpload whose body is an
  `Error` is a failure, as the reference warns.
- **Exit codes**: a status of 300 or more is exit 22 with `S3 returned error <status>: <Code>:
  <Message>` (or the status alone without an `Error` body); a reply the client or the XML readers
  cannot read is exit 8; a connect failure keeps the connector's code and message.
- One connection per request; TLS, proxy and events come from the connector through `ConnectTarget`.

## Left out, and why

- Connection reuse between requests: one connection each keeps the handler simple; a multipart upload's
  extra handshakes cost little next to 8 MiB parts.
- AbortMultipartUpload after a failed part: an abandoned upload is reclaimed by the bucket's lifecycle
  rule, and aborting needs a further request that can fail the same way.
- Streaming SigV4 (`STREAMING-AWS4-HMAC-SHA256-PAYLOAD`) and parallel parts: whole-part hashing is what
  the signer supports, and parallel parts would need several connections at once.
- Bucket creation and deletion, DELETE of objects and other S3 operations: the task asks for read,
  write and listing.
- Credentials from `~/.aws/credentials` and instance metadata: environment variables and `--user` cover
  MinIO and R2; reading the profile files is a later task if wanted.
