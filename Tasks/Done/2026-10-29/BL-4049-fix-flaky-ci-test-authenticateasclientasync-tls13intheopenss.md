---
id: BL-4049
title: Fix flaky CI test AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4049 — Fix flaky CI test AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder that failed once on Windows

## Goal

`AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` failed on Windows in CI run 40824274066 (https://github.com/StewartScottRogers/Curl/actions/runs/40824274066). First failing commit: 0b744431.

    CollectionAssert.AreEqual failed. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 40824274066 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` fail intermittently is named under Notes and removed.
- [x] `AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: not random. Every Windows job of CI runs 40819228354 onward (40824274066, 40825137844, 40828309078, 40829034747, 40830606543) failed the same way, and 0b744431 changed only a task file, so the windows-latest runners' Schannel changed. That Schannel TLS 1.3 server answers the hand-built OpenSSL-build ClientHello (key share X25519, supported_groups the OpenSSL default list) with a HelloRetryRequest for another group it prefers: the trace read OUT 1 IN 2 OUT 1 IN 2 ... instead of OUT 1 IN 2 ..., so the fixed seven-message list had nine. Schannel on this Windows 11 lane machine never asks (a 300-handshake probe: all seven messages, x25519), so it reproduces only on CI.
- Fix (test only): the test now offers one group, `Curves: "X25519"`, the one its key share is for, so no server has another group to ask for. The message order the test pins is unchanged and an HRR cannot occur, whichever server (Schannel or OpenSSL) runs the echo side. No production code changed, so no Measure-CodeQuality run was needed.
- Verified: Curl.Networking.UnitTests fast tests pass (3925 passed, 33 skipped); dotnet format reports no changes. The change is platform-neutral (an option on the client, no path, OS text or certificate), so the CI criterion is met as lane rule 11 says; the shift's CI watch files a task for any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 946f54b2 c0ca9fbd.
- 2026-10-28: Doing -> Done. The TLS 1.3 handshake-trace test offers only X25519, so Schannel's HelloRetryRequest on windows-latest can no longer change the message list
