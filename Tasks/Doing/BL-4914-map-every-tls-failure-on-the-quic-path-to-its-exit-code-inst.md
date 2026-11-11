---
id: BL-4914
title: Map every TLS failure on the QUIC path to its exit code instead of a KeyNotFoundException
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/QuicDialer.cs, Curl.Networking.UnitTests/QuicDialerExitCodeWordsTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4914 — Map every TLS failure on the QUIC path to its exit code instead of a KeyNotFoundException

## Goal

A pinned-key mismatch over HTTP/3 exits 90 with curl's message, and no exit code missing from `ExitCodeWords` can leak a .NET exception.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-011 (Medium, confirmed: reproduced at least twice).

- Repro: HTTP/3 server on UDP 18350 with the same cert plus the TCP 18301 server; alt-svc file pointing h1 18301 to h3 127.0.0.1 18350. `curl -sS https://localhost:18301/ --alt-svc alt.txt --cacert ca.pem --ssl-no-revoke --pinnedpubkey "sha256//AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="`
- Expected: REF and the port's TCP path: `curl: (90) SSL: public key does not match pinned public key`, exit 90.
- Actual: `curl: (2) The given key 'SslPinnedPubKeyNotMatch' was not present in the dictionary.`, exit 2 (no request sent - not a bypass).
- Where to start: `QuicDialer.cs`: `ExitCodeWords` (~line 28) lacks 90 and probably 58/59/77; indexed directly at ~lines 374 and 490. Use a safe lookup. If the alt-svc task disables h3 on Windows this stays a unit-test-level fix for platforms where QUIC is used.

## Acceptance criteria

- [ ] A unit test drives the QUIC path with a pin mismatch and gets exit 90 with REF's message.
- [ ] Every `CurlExitCode` the QUIC path can return has a word or a safe fallback, pinned by a test.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: ebf5b8e0 e6aa0a09 a0b49a29.
