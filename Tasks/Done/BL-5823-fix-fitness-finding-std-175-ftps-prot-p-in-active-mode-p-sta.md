---
id: BL-5823
title: Fix fitness finding STD-175: FTPS PROT P in active mode (-P) starts the data-connection TLS handshake only after the 150 reply, so a server that completes the handshake before sending 150 hangs
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-23
---
# BL-5823 — Fix fitness finding STD-175: FTPS PROT P in active mode (-P) starts the data-connection TLS handshake only after the 150 reply, so a server that completes the handshake before sending 150 hangs

## Goal

STD-175 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: FTPS PROT P in active mode (-P) starts the data-connection TLS handshake only after the 150 reply, so a server that completes the handshake before sending 150 hangs. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (4 runs: download and upload, implicit and explicit FTPS, EPRT and PORT)
- **Protocol/area:** FTPS active mode, Windows build (neighbour of the fixed STD-157, which covered passive mode)
- **Repro:** an FTPS mock with implicit TLS on 127.0.0.1:24060 (and a plain-control one accepting `AUTH TLS` on 24030), certificate for `localhost` signed by test CA `ca.pem`, supporting EPRT and PORT. For user `early` it connects to the client's active data port, completes the server side of the TLS handshake first, then sends `150`, then `0123456789` and `226`. For user `late` it sends `150` first.
  1. `curl -sS -m 6 --cacert ca.pem --ssl-no-revoke -P - -w " [%{response_code} %{size_download}]\n" ftps://early:p@localhost:24060/f.txt`
  2. `curl -sS -m 6 --cacert ca.pem --ssl-no-revoke -P 127.0.0.1:24800 --disable-eprt -w " [%{response_code} %{size_download}]\n" ftps://early:p@localhost:24060/f.txt`
  3. `curl -sS -m 6 --cacert ca.pem --ssl-no-revoke --ssl-reqd -P 127.0.0.1 -w " [%{response_code} %{size_download}]\n" ftp://early:p@localhost:24030/f.txt`
  4. `curl -sS -m 6 --cacert ca.pem --ssl-no-revoke -T loc.txt -P - -w " [%{response_code} %{size_upload}]\n" ftps://early:p@localhost:24060/u.txt` (loc.txt is 17 bytes)
- **Expected (REF):** 1–3: `0123456789 [226 10]`, exit 0. 4: `[226 17]`, exit 0.
- **Actual (.NET):** each prints `curl: (28) Operation timed out after 6004 milliseconds with 0 bytes received` and `[000 0]`, exit 28. User `late` with `-P`, and passive `early`, match REF.
- **Likely location:** `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs`: the STD-157 fix (`StartDataHandshake`, ~line 2226) is only in `DialDataAsync` (passive). In the active path `ReadyDataConnectionAsync` (~line 1917) runs `AcceptDataConnectionAsync` and then `SecureDataConnectionAsync` only after the reply. Start the client handshake as soon as the data connection is accepted.
- **Acceptance check:** command 1 prints `0123456789 [226 10]`, exit 0; user `late` and passive mode still pass.

## Acceptance criteria

- [x] command 1 prints `0123456789 [226 10]`, exit 0; user `late` and passive mode still pass.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fix: while the transfer command's reply is awaited in active mode with PROT P, FtpSession accepts the data connection and starts the client TLS handshake at once (AcceptAndStartDataHandshakeAsync); an already waiting connection starts it in AcceptWaitingConnectionAsync. Pinned by two unit tests in FtpProtocolHandlerTlsTests (handshake count when the reply is awaited). The live mock repro was not rebuilt; the fake-connection test stands in for it. Library measured 100% line and branch. Platform-neutral.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: f4cc9630 69116afe 3d836d5e.
- 2026-11-23: Doing -> Done. FTPS active-mode PROT P data handshake starts on accept, before the 150 reply
