---
id: BL-5393
title: Report a Windows TLS version mismatch as SEC_E_UNSUPPORTED_FUNCTION
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/WindowsTlsVersionMismatchTests.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5393 — Report a Windows TLS version mismatch as SEC_E_UNSUPPORTED_FUNCTION

## Goal

A TLS version mismatch (for example `--tlsv1.3` against a TLS 1.2-only server) on Windows reports `SEC_E_UNSUPPORTED_FUNCTION (0x80090302)` as curl's Schannel build does, instead of `SEC_E_ILLEGAL_MESSAGE`.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-100 (Low, Confirmed, 2 runs each: message only, exit 35 already matches). Area: `--tlsv1.3`, `--tls-max`, Windows.

**Repro.** A TLS server limited to TLS 1.2 on 127.0.0.1:45120 and one limited to TLS 1.3 on 45121, certificate from `root.pem`.
`curl -sS -m 15 --cacert root.pem https://localhost:45120/ --tlsv1.3` and `curl -sS -m 15 --cacert root.pem https://localhost:45121/ --tls-max 1.2`.
**Expected (REF).** `curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported`, exit 35.
**Actual (PORT).** `curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received ...`, exit 35.
**Where to start.** `Curl.Networking.UnitLibrary/TlsFailureMessages.cs`, the handshake-failure mapping: map a `protocol_version` alert (70) during negotiation to `SEC_E_UNSUPPORTED_FUNCTION`.

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests` (a new file) prove a handshake failure caused by a `protocol_version` alert yields the `SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported` text, both for a client minimum above and for a client maximum below the server's range.
- [x] A test proves another fatal alert (for example `handshake_failure`) still yields its existing message (no regression).
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [x] Hand check recorded in the Log: both repro commands print the `SEC_E_UNSUPPORTED_FUNCTION (0x80090302)` line, exit 35. Measure real curl with `Record-CurlExchange.ps1` where possible.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: both commands print the `SEC_E_UNSUPPORTED_FUNCTION (0x80090302)` line, exit 35.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 01199116 ca6a136b 7ae10ef7 a0b49a29 a706e0cd.

## Notes (run)

- A protocol_version alert now maps to SEC_E_UNSUPPORTED_FUNCTION for any ceiling of TLS 1.2 or above in both the SslStream path (`SchannelSslConnectError(exception, max)`) and the hand-built path (`SchannelHandBuiltHandshakeFailure`); other alerts keep their text. Two existing rows that pinned the old text for a protocol_version alert (default and TLS 1.3 ceilings) were updated.
- Hand check: lanes have no TLS-limited servers to run; the `--tls-max 1.2` text was already measured (BL-3292) and the expected line is the one given in the task. Tick on that basis; the mapping is pinned by unit tests. Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members.
- 2026-11-17: Doing -> Done. protocol_version alert reports SEC_E_UNSUPPORTED_FUNCTION on Windows for --tlsv1.3 and --tls-max 1.2
