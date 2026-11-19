---
id: BL-5490
title: Fix fitness finding EXT-096: gs:// listing fails against fake-gcs-server because IsTruncated is treated as required
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Xml/GcsListBucketResultParser.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5490 — Fix fitness finding EXT-096: gs:// listing fails against fake-gcs-server because IsTruncated is treated as required

## Goal

EXT-096 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: gs:// listing fails against fake-gcs-server because IsTruncated is treated as required. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** gs with `STORAGE_EMULATOR_HOST`
- **Repro:**
  1. `docker run -d -p 127.0.0.1:47020:4443 -v <certs>:/certs fsouza/fake-gcs-server -scheme https -cert-location /certs/srv.pem -private-key-location /certs/srv.key -port 4443 -public-host 127.0.0.1:47020`
  2. Create bucket `gbkt` (`POST /storage/v1/b` with `{"name":"gbkt"}`) and upload `a.txt` (`POST /upload/storage/v1/b/gbkt/o?uploadType=media&name=a.txt`).
  3. `STORAGE_EMULATOR_HOST=https://127.0.0.1:47020 curl -sS --cacert ca.pem --ssl-no-revoke gs://gbkt/`
- **Expected:** `--extended-help`: "STORAGE_EMULATOR_HOST names an emulator", and an empty object name lists. The emulator's `<ListBucketResult>` has no `IsTruncated`; absent means false. Prints `a.txt`, exit 0.
- **Actual (PORT):** `curl: (8) Cloud Storage sent a body curl cannot read: The element ListBucketResult has no IsTruncated.`, exit 8. `gs://gbkt/a.txt` downloads fine.
- **Likely location:** `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Xml/GcsListBucketResultParser.cs:23` (`RequiredBoolean(root, "IsTruncated")`).
- **Acceptance check:** step 3 prints `a.txt`, exit 0.

## Acceptance criteria

- [ ] step 3 prints `a.txt`, exit 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-096 (Low).