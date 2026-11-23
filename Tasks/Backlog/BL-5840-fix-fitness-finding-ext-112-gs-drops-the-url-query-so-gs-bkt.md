---
id: BL-5840
title: Fix fitness finding EXT-112: gs:// drops the URL query, so gs://bkt/?prefix=d/ lists the whole bucket
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsEndpoint.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsOperations.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5840 — Fix fitness finding EXT-112: gs:// drops the URL query, so gs://bkt/?prefix=d/ lists the whole bucket

## Goal

EXT-112 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: gs:// drops the URL query, so gs://bkt/?prefix=d/ lists the whole bucket. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (silent wrong listing)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** gs (Google Cloud Storage XML API)
- **Repro:**
  1. `docker run -d -p 127.0.0.1:27021:4443 -v <certs>:/certs fsouza/fake-gcs-server -scheme https -cert-location /certs/srv.pem -private-key-location /certs/srv.key -port 4443 -public-host 127.0.0.1:27021`
  2. Create bucket `gbkt` with objects `a.txt` and `d/b.txt` (JSON API `/storage/v1/b` and `/upload/storage/v1/b/gbkt/o?uploadType=media&name=…`).
  3. `STORAGE_EMULATOR_HOST=https://127.0.0.1:27021 curl -sS -v --cacert ca.pem --ssl-no-revoke "gs://gbkt/?prefix=d/"`
- **Expected:** s3 ("a ?query goes to S3 too, ?prefix=logs/ narrows a list") and az ("a ?prefix= … in the URL narrows the list") honour the query, and `Documentation/Conformance/Extended/GoogleCloudStorage.md` lists `prefix` for list. gs should send `prefix=d%2F` and print only `d/b.txt` (or refuse the query).
- **Actual (.NET):** sends `GET /gbkt?list-type=2&delimiter=%2F&prefix=` and prints `d/` and `a.txt`, exit 0.
- **Likely location:** the gs list request builder in `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` (endpoint mapping / `GcsEndpoint.For`).
- **Acceptance check:** step 3 shows `prefix=d%2F` on the request line and prints only `d/b.txt`.

## Acceptance criteria

- [ ] step 3 shows `prefix=d%2F` on the request line and prints only `d/b.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.