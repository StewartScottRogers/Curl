---
id: BL-5682
title: Check --aws-sigv4 over HTTP/2 against curl: sign the host as curl does when the request goes out as :authority
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/AwsSigV4Signer.cs, Curl.Authentication.UnitLibrary/AwsSigV4Headers.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5682 — Check --aws-sigv4 over HTTP/2 against curl: sign the host as curl does when the request goes out as :authority

## Goal

A `--aws-sigv4` request sent over HTTP/2 carries the same signed headers and signature as curl 8.22.0's HTTP/2 request, so a server that accepts curl's accepts the port's.

## Context

From Surly cookbook bug report `2026-11-21T0547-Curl-bug-report.md` (batch 9 of the 1,000-recipe plan), finding 3 (check). Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

`curl --cacert server.pem --aws-sigv4 aws:amz:us-east-1:s3 -u K:S https://127.0.0.1:8443/bucket/x` gets 401 from Surl over HTTP/2 and 200 with `--http1.1`. Possibly the signed `host` header against the `:authority` pseudo-header: over HTTP/2 there is no `Host` header on the wire, but curl still signs `host:<authority>` in the canonical request. The same report went to Surl (its item 1), so the fault may be on Surl's side. Measure curl 8.22.0's OpenSSL build with nghttp2 on Linux (the Windows Schannel reference has no HTTP/2) with `--trace` against a recording server over HTTP/2: the headers sent, the `Authorization` value and the canonical request. Compare the port's with the same fixed date (`x-amz-date`) and credentials. If they match, the fault is Surl's: say so in the Notes and change nothing.

## Acceptance criteria

- [ ] The Notes record curl 8.22.0 nghttp2's HTTP/2 headers and `Authorization` for the repro with a fixed date, and the port's.
- [ ] The port's signed headers and signature over HTTP/2 equal curl's, pinned by a unit test of the canonical request; if they already did, the Notes say the 401 is Surl's.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-21 (from the Surly session): Surl found the cause on its side and filed it High (Surl BL-1547..1549). Upstream curl signs `host` from the Host line it builds, then sends it as `:authority` over HTTP/2 and drops the Host header; Surl's signature check never fed `:authority` in as `host`. So the expected outcome is "the port matches upstream": run the comparison in the acceptance criteria to confirm, and close with only the pinning test if the canonical requests agree.

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0547, finding 3 (check).
