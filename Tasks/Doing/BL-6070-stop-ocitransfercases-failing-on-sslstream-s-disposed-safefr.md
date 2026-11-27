---
id: BL-6070
title: Stop OciTransferCases failing on SslStream's disposed SafeFreeCredential
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage]
requirement: none
created: 2026-11-26
completed:
---
# BL-6070 — Stop OciTransferCases failing on SslStream's disposed SafeFreeCredential

## Goal

The `OciTransferCases` TLS tests, and the other storage cases like them, do not fail when .NET's TLS credential cache disposes a credential during their handshakes.

## Context

Found during BL-6068's full fast-test run on Windows: `OciTransferCases.Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly` threw `ObjectDisposedException` for `System.Net.Security.SafeFreeCredential_SECURITY` from `SslSessionsCache.<CacheCredential>g__ShrinkCredentialCache` inside `SslStream`'s handshake. The class's 6 tests passed when run alone, so it is a race between handshakes in one test process. Start with the loopback TLS server these cases use under `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage`.

## Acceptance criteria

- [ ] The cause is recorded under Notes, and the storage cases' TLS handshakes can no longer fail on the disposed credential (for example by sharing one server certificate context, or by not running those handshakes in parallel).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 970d4d38 3d836d5e.
