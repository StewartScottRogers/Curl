# ADR-1095: gs:// URLs download, upload and list over the XML API with a bearer token or HMAC key

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4125
- Decided by Claude under Stewart's delegation.

## Context

The Google Cloud Storage extension (ADR-0999) had its V4 signer and bearer header (BL-4284), its XML
bodies (BL-4285) and its HTTP/1.1 client (BL-4286), but no handler. It is tested against the Cloud
Storage XML API as documented in 2026 (API overview "Request endpoints", "GET Object", "PUT Object",
"List objects" with `list-type=2`, "XML API error responses", "Authentication" with OAuth 2.0 and the
"V4 signing process" with HMAC keys), not against curl, and must never change what curl does.

## Decision

- **Scheme `gs`**, URL `gs://bucket/object`, the form `gsutil` and `gcloud storage` use; no new options.
- **XML API, not the JSON API**: both authentications work against it (the JSON API takes no HMAC
  key), and its paths are the URL's own (`/bucket/object`), so one request shape serves both.
- **Authentication**: `--oauth2-bearer TOKEN` sends `Authorization: Bearer`; otherwise
  `--user ACCESS_ID:SECRET` signs with the HMAC key (`GOOG4-HMAC-SHA256`, region `auto`, over the
  payload's SHA-256); with neither the request goes unauthenticated, for public objects. A bearer token
  wins over `--user` because it is the narrower, explicitly OAuth choice.
- **Endpoint**: `https://storage.googleapis.com`, path-style, so every bucket name fits the one
  certificate. `STORAGE_EMULATOR_HOST` (the variable Google's client libraries read, for
  fake-gcs-server and the like) names another endpoint: an `http`/`https` URL, or a bare `host:port`
  taken as `http`; anything else fails with exit 3.
- **What a URL does**: with `-T`, PUT Object; an empty name or one ending in `/` lists (List objects,
  `list-type=2`, delimiter `/`, every page by continuation token, common prefixes then names, one per
  line); otherwise GET Object to the output. An upload to an empty name or a prefix fails with exit 3.
- **Uploads go in one PUT**: Cloud Storage accepts objects up to 5 TiB in one request, so no multipart
  or resumable upload. The upload is read into memory first so its SHA-256 can be signed and its
  `Content-Length` sent, which also allows an upload from a pipe.
- **Exit codes**: a status of 300 or more is exit 22 with `Cloud Storage returned error <status>:
  <Code>: <Message>` (or the status alone without an `Error` body); a reply the client or the XML
  readers cannot read is exit 8; a connect failure keeps the connector's code and message.
- One connection per request; TLS, proxy and events come from the connector through `ConnectTarget`.

## Left out, and why

- Resumable uploads and XML multipart uploads: one PUT covers every object size Cloud Storage allows;
  the cost is that an upload must fit in memory.
- Application Default Credentials, service-account key files and the metadata server: they need a
  JWT signed with RSA and a token exchange, which `--oauth2-bearer` with `gcloud auth print-access-token`
  already gives.
- Deleting objects, creating buckets and other operations: the task asks for read, write and listing.
- Connection reuse between listing pages: one connection per request keeps the handler simple.
