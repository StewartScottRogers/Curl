---
id: BL-5680
title: Make a Curl.Api PUT with a body to gs:// upload, as s3:// and curl -T do
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/RequestBodies/UploadSchemes.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5680 — Make a Curl.Api PUT with a body to gs:// upload, as s3:// and curl -T do

## Goal

A `Curl.Api` `PUT` with a body to `gs://bucket/x` uploads the object, as the same request to `s3://` does and as `curl -T` to `gs://` does.

## Context

From Surly cookbook bug report `2026-11-21T0547-Curl-bug-report.md` (batch 9 of the 1,000-recipe plan), finding 1. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

BL-5594 (Done) made a body upload on ftp, smb, smtp, s3, az, tftp and telnet; gs was left out. `new CurlRequest(HttpMethod.Put, "gs://bucket/x") { Body = CurlContent.FromStream(...) }` against a `gs://` listener (as in the cookbook recipe `gs-sign-with-an-hmac-key-in-csharp`) throws `CurlException` 4 `NotBuiltIn`: `gs:// does not support -X PUT; it sends only GET, or PUT with -T`. Add gs to `Curl.Api/RequestBodies/UploadSchemes.cs` (and its ADR from BL-5594), and check every other scheme whose console upload is `-T` (oci, abfs, abfss) at the same time.

## Acceptance criteria

- [ ] A `Curl.Api` `PUT` with a byte body and with a stream body to a gs test server stores the object, pinned by tests; a GET returns the same bytes.
- [ ] Every scheme whose console upload is `-T` is in `UploadSchemes`, or the Notes say why not; BL-5594's ADR lists them.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0547, finding 1.
- 2026-11-21: Backlog -> Doing. Criteria at claim: a201a555 808ef102 3d836d5e.
