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
completed: 2026-11-26
---
# BL-6070 — Stop OciTransferCases failing on SslStream's disposed SafeFreeCredential

## Goal

The `OciTransferCases` TLS tests, and the other storage cases like them, do not fail when .NET's TLS credential cache disposes a credential during their handshakes.

## Context

Found during BL-6068's full fast-test run on Windows: `OciTransferCases.Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly` threw `ObjectDisposedException` for `System.Net.Security.SafeFreeCredential_SECURITY` from `SslSessionsCache.<CacheCredential>g__ShrinkCredentialCache` inside `SslStream`'s handshake. The class's 6 tests passed when run alone, so it is a race between handshakes in one test process. Start with the loopback TLS server these cases use under `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage`.

## Acceptance criteria

- [x] The cause is recorded under Notes, and the storage cases' TLS handshakes can no longer fail on the disposed credential (for example by sharing one server certificate context, or by not running those handshakes in parallel).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Cause: a race in .NET's own process-wide TLS credential cache, `SslSessionsCache`. A server
credential is cached per certificate thumbprint, and `StorageTransferChecks.RunAsync` made a new
self-signed certificate for every run, so every run added an entry. When the entry count reaches
a multiple of 32, `CacheCredential` shrinks the cache on the inserting thread: it drops its
reference to each credential and takes it again if the credential is still open. A concurrent
handshake that releases a credential's last reference between that check and the new reference
makes the inserting handshake throw `ObjectDisposedException`. This reading of the runtime comes
from the exception's stack in BL-6068's run and knowledge of the runtime source; the race was not
reproduced here, as nothing in the tests can force its timing, so there is no failing test for it.

Fix, both measures:

- `StorageTransferTarget.Certificate`: one certificate per target, so the storage cases stop
  filling the cache and their server handshakes find the credential already there.
- `[DoNotParallelize]` on the six `*TransferCases` classes: MSTest runs them alone, after the
  parallel tests, so no other test's handshake releases a credential during one of theirs. Sharing
  the certificate alone would only make the race rarer, since the first handshake still inserts.

No ADR: this changes only how these tests run, not any behaviour of Curl. Other test folders that
make a certificate per run were left alone (outside `touches`; none has been seen failing this way).

Verified on Windows: `dotnet build` clean; all fast tests passed
(`Curl.Conformance.Extended.UnitTests`: 1737 passed, 1 skipped). Platform-neutral: no path, error
text or certificate behaviour that differs by platform was added.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 970d4d38 3d836d5e.
- 2026-11-26: Doing -> Done. Storage transfer cases share one certificate per target and run outside the parallel set; build clean, fast tests pass
