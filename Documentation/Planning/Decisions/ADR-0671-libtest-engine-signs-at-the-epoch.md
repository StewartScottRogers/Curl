# ADR-0671 — The ratchet's libtest engine and `--aws-sigv4` commands sign at the epoch, standing in for Debug's `CURL_FORCETIME`

- **Status:** Accepted
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2675.

## Context

Upstream's AWS SigV4 cases (the libtest cases 1933 to 1938, 1955 to 1959, 1964, 1970 to 1975 and 1978, and the
curl-tool cases 439, 472, 1976, 3222, 5026 and 5027) name the `aws` feature and, all but 1964, `Debug`:
upstream's runtests sets `CURL_FORCETIME`, under which a debug build's `lib/http_aws_sigv4.c` signs at time 0,
so every expected `Authorization` and `X-*-Date` line holds `19700101` and `19700101T000000Z`. Curl has no
debug build, so the ratchet skipped them (`Curl lacks the feature Debug`). The harness already stands in for
Debug for the httpsig cases by signing the curl tool's `--httpsig` commands at the epoch (BL-2183), but the
libtest engine (`ManagedCurlEngine`, ADR-0658) signed with the system clock and offered no seam to change it.

## Decision

- `ManagedCurlEngine`'s internal constructor takes a `signingClock`, kept on `ManagedEngineSharedState.SigningClock`
  (a share handle's state copies the engine's) and given to `EngineComposition.CreateProtocolHandlers` by
  `CreateDispatcher`; `null`, every production engine's, is `TimeProvider.System` as before.
- `UpstreamConformanceTests.CreateEngine` gives every libtest engine a signing clock fixed at the epoch, as
  runtests sets `CURL_FORCETIME` for every case.
- `UpstreamConformanceTests.PlatformFor` reports `Debug` to a case naming the `aws` feature, and `RunCurlAsync`
  signs an `--aws-sigv4` command at the epoch, as it signs `--httpsig` ones.

Only the signing clock is fixed: the engine's caches, timeouts and HSTS clock stay on the system clock, so no
other case's behaviour changes.

## Consequences

20 of the 25 aws cases pass and hold the ratchet. 1933 (URL credentials, BL-2693), 1978 (header order,
BL-2694) and 1972 (the debug build's fixed MIME boundary, BL-2695) fail and are filed; 472 is still skipped for
`Unicode`. The engine's own test is `ManagedCurlEngineTests.PerformAsync_SigningClockGivenAndAwsSigV4_SignsAtThatClocksTime`.
