---
id: BL-5330
title: Print Schannel's read-phase alert message on tunnelled and auto-client-cert TLS streams
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/TlsFailureMessagesTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5330 — Print Schannel's read-phase alert message on tunnelled and auto-client-cert TLS streams

## Goal

A TLS alert received after the handshake over a proxy tunnel, or with `--ssl-auto-client-cert`, prints curl's Schannel read-phase text (exit 56 unchanged), not the generic `Failure when receiving data from the peer`.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-081 (Low: message only; Confirmed, 3 runs for `--ssl-auto-client-cert`, 2 per tunnel kind). Area: HTTPS read-phase error text, Windows build.

**Repro.** Serve a localhost certificate signed by `ca.pem` on 127.0.0.1:32123 with a TLS 1.3 server that requires a client certificate and sends an alert after the handshake when none is given (the review used Python's `ssl` for this; use a PowerShell or C# file-based server, never Python). Run an HTTP CONNECT proxy on 32090, an HTTPS CONNECT proxy on 32092 and a SOCKS5 proxy on 32095.
- `curl -sS https://localhost:32123/ --cacert ca.pem -x http://localhost:32090`
- `curl -sS https://localhost:32123/ --cacert ca.pem -x https://localhost:32092 --proxy-cacert ca.pem`
- `curl -sS https://localhost:32123/ --cacert ca.pem -x socks5h://localhost:32095`
- `curl -sS https://localhost:32123/ --cacert ca.pem --ssl-auto-client-cert`

**Expected (curl 8.22.0, Schannel).** The three proxy commands print `curl: (56) schannel: failed to read data from server: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log.`; the last prints `curl: (56) Recv failure: Connection was aborted`. All exit 56.

**Actual (port).** All four print `curl: (56) Failure when receiving data from the peer`, exit 56. (Direct, without `--ssl-auto-client-cert`, the port matches.)

**Where to start.** `Curl.Networking.UnitLibrary/TlsFailureMessages.cs`: a read-phase `SEC_E_ILLEGAL_MESSAGE` (about line 169) has no `schannel: failed to read data from server: ...` text on the tunnel and auto-client-cert stream paths, which fall back to `Curl.Api/Errors/CurlCodeText.cs:73`. Find where the direct path maps the exception and make the tunnel and auto-client-cert read paths use the same mapping.

## Acceptance criteria

- [ ] New tests in `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs` prove the read-phase mapping gives the `schannel: failed to read data from server: SEC_E_ILLEGAL_MESSAGE (0x80090326) - ...` text (exact bytes above) for an alert surfaced through each of the tunnelled-stream paths (stand-in streams throwing the exception the direct path sees), with exit 56.
- [ ] A test pins `Recv failure: Connection was aborted` for the `--ssl-auto-client-cert` case, and that the direct path's text is unchanged.
- [ ] Tests are platform-neutral, or marked `[OSCondition(OperatingSystems.Windows)]` where the text is Schannel-specific.
- [ ] Hand check, recorded in the Log: the four repro commands print the expected text on Windows.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the first command prints the reference `SEC_E_ILLEGAL_MESSAGE` line; the last prints `curl: (56) Recv failure: Connection was aborted`.
- If the fix lies outside `TlsFailureMessages.cs` (the tunnel or stream code), add that file to `touches` before claiming.

## Log

- 2026-11-16: Created.
