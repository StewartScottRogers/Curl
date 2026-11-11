---
id: BL-4947
title: Exit 6 when the --dns-over-quic resolver is unreachable
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.EncryptedDns.UnitLibrary]
requirement: none
created: 2026-11-11
completed:
---
# BL-4947 — Exit 6 when the --dns-over-quic resolver is unreachable

## Goal

An unreachable DoQ resolver gives `curl: (6) Could not resolve host: <host>`, as `--dns-over-tls` and DoH do.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-025 (Low, confirmed: reproduced at least twice).

- Repro: `curl -sS -m 10 --dns-over-quic 127.0.0.1:18899 http://localhost:18800/plain` (nothing on 18899).
- Expected: `curl: (6) Could not resolve host: localhost`.
- Actual: `curl: (7) Failed to connect to 127.0.0.1:18899 after 2 ms: Could not connect to server`.
- Where to start: The DoQ resolver in `Curl.Extension.EncryptedDns.UnitLibrary`.

## Acceptance criteria

- [ ] The repro exits 6 with `Could not resolve host: localhost`, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 7cef9a0d a0b49a29.
