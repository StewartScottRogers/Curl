---
id: BL-4927
title: Send Digest nc=00000001 on the first --anyauth answer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Original.Protocol.Http.UnitTests/AnyAuthDigestNonceCountTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4927 — Send Digest nc=00000001 on the first --anyauth answer

## Goal

`--anyauth` answering a Digest challenge sends `nc=00000001`, as curl 8.22.0 and 8.18 do.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-017 (Low, confirmed: reproduced at least twice).

- Repro: Server on 18200 answering 401 `WWW-Authenticate: Digest realm="r", nonce="n", qop="auth"` without Authorization, else 200 echoing it. `curl -s --anyauth -u user:pass http://127.0.0.1:18200/multi`
- Expected: Output contains `nc=00000001`.
- Actual: `nc=00000002` (hash consistent with nc=2) for every URL; `--digest` alone is right.
- Where to start: `DigestAuthenticator.cs` line 124 and `AnyAuthNonceCount = 2` at line 52 (BL-3897). `AnyAuthDigestNonceCountTests.cs` locks in the wrong value and must change; re-measure real curl first and record why BL-3897 chose 2.

## Acceptance criteria

- [x] The repro's output contains `nc=00000001`.
- [x] `AnyAuthDigestNonceCountTests` pins `nc=00000001`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

Real curl 8.22.0 (Schannel) measured with a PowerShell loopback server: `--anyauth` sends nc=00000001, so BL-3897's 2 was wrong; the special case is removed. Quality run: 0 failing members.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: c1c558a7 df22d4f0 a0b49a29.
