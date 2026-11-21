---
id: BL-5642
title: Make Curl.Api on Linux and macOS find the default CA bundle as the console does, so an untrusted peer gives 60, not 77
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Api/Options/Transport/CurlTlsOptions.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5642 — Make Curl.Api on Linux and macOS find the default CA bundle as the console does, so an untrusted peer gives 60, not 77

## Goal

With no `CertificateAuthorityBlob` or CA file set, `Curl.Api` on Linux and macOS verifies against the same default trust store as the console and fails an untrusted peer with 60 `PeerFailedVerification`, as on Windows.

## Context

From Surly cookbook bug report `2026-11-20T1836-Curl-bug-report.md` (batch 6 of the 1,000-recipe plan, and batch 5's CI on Linux and macOS), finding 3. Build tested: Curl `7d167fa10a` (`curl`, `curl-mcp`, `Curl.Api`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

With no `CertificateAuthorityBlob`, an untrusted HTTPS peer throws 77 `SslCacertBadfile` on Linux and macOS, and 60 `PeerFailedVerification` on Windows. Console curl gives 60 on all three. So `Curl.Api` seems to find no default bundle off Windows: the console's default CA discovery (`CURL_CA_BUNDLE`, `SSL_CERT_FILE`, the build's default bundle path, the system store) is likely done in `Curl.Console` and not reached by `Curl.Api`. Move it where both hosts get it, or call it from `Curl.Api`, and keep `Libcurl` in step. Also check that a trusted public peer (for example a test server whose CA is in the system store) works through `Curl.Api` off Windows. The tests must run on Linux and macOS (CI runs the fast tests on all three).

## Acceptance criteria

- [ ] On Linux and macOS, a `Curl.Api` request with no CA option to an HTTPS server with an untrusted certificate fails with `CurlCode.PeerFailedVerification` (60), pinned by a test that runs there.
- [ ] A `Curl.Api` request with no CA option uses the same default bundle path the console reports, pinned by a test of the resolved trust source.
- [ ] `dotnet build` is clean and the fast tests pass on all three platforms in CI.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1836, finding 3.
- 2026-11-20: Backlog -> Doing. Criteria at claim: e512ca0d 55a901a4 b3b3bfe9.
