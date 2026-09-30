---
id: BL-615
title: Authenticate to a SOCKS5 proxy with --socks5-basic and --socks5-gssapi
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612, BL-527, BL-691]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-615 — Authenticate to a SOCKS5 proxy with --socks5-basic and --socks5-gssapi

## Goal

The SOCKS5 greeting offers the methods `--socks5-basic` and `--socks5-gssapi` select (as curl 8.21.0 offers them, including its default), username/password authentication (RFC 1929) works with the proxy credentials, and GSS-API authentication (RFC 1961, with `--socks5-gssapi-service` and `--socks5-gssapi-nec`) works through the Kerberos GSS-API mechanism (BL-691) behind the seam of BL-525 and BL-527, including RFC 1961's per-message protection negotiation, on every platform.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Options: BL-612; seam: BL-525, BL-527; GSS Wrap/Unwrap for the protection-level exchange: BL-691. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): SOCKS5 GSS-API works on every platform, no refusal.
- Code: `Curl.Networking.UnitLibrary/Socks5Handshake.cs` (ADR-0084 follows the Schannel build).

## Acceptance criteria

- [x] Measured first with a scripted SOCKS exchange (as in BL-614): the method list curl offers with neither option, with `--socks5-basic` only and with `--socks5-gssapi` only, and a refused username/password; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Networking.UnitTests` pin each greeting, the RFC 1929 exchange, the GSS-API exchange with a fake token source, and each failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-30 with `Record-CurlExchange.ps1 -Port 41615 -Script <file>` playing the SOCKS5
server (`read 3|4|5`, `send \x05\x00`, `read 10`, `send \x05\x00\x00\x01\x7f\x00\x00\x01\x1f\x90`,
`read`, `send HTTP/1.1 200 OK...`), curl 8.21.0 (Schannel, `/mingw64/bin/curl`), URL
`http://127.0.0.1:8080/` through `--socks5 127.0.0.1:41615`, `-sS`:

- Neither option: greeting `05 02 00 01`; with `-U u:p` `05 03 00 01 02`. Then `05 01 00 01 7f 00 00 01 1f 90`, exit 0, stdout `hi`, stderr empty.
- `--socks5-basic`: `05 01 00`; with `-U u:p` `05 02 00 02`. Exit 0.
- `--socks5-gssapi`: `05 02 00 01`, with `-U u:p` too (the credential is dropped). Exit 0.
- Refused user name and password (default options, `-U u:p`; the proxy picked `05 02`): curl sent `01 01 75 01 70`, the proxy answered `01 01` -> `curl: (97) User was rejected by the SOCKS5 server (1 1).`, exit 97.
- `--socks5-basic`, the proxy picked `05 01`: `curl: (97) SOCKS5 GSSAPI per-message authentication is not enabled.`, nothing more sent.
- `--socks5-gssapi -U u:p`, the proxy picked `05 02`: `curl: (97) BASIC authentication proposed but not enabled.`, nothing more sent.
- `--socks5-gssapi --socks5-gssapi-service foo -v`, the proxy picked `05 01`: stderr `* SSPI error: InitializeSecurityContext failed: SEC_E_TARGET_UNKNOWN (0x80090303) - The specified target is unknown or unreachable`, `* Failed to initialize security context.`, `* Unable to negotiate SOCKS5 GSS-API context.`, `* closing connection #0`, then `curl: (97) SSPI error: ...SEC_E_TARGET_UNKNOWN...`; nothing more sent.
- Linux, curl 8.18.0 (OpenSSL, mit-krb5 1.22.1) in WSL (`-Curl wsl.exe`, `-ListenAddress 172.26.96.1`): the same greetings and mismatch messages; the proxy picking `05 01` with no ticket gives `curl: (97) GSS-API error: gss_init_sec_context failed: No credentials were supplied, or the credentials were unavailable or inaccessible.` + LF + `No Kerberos credentials available (default cache: FILE:/tmp/krb5cc_1000)` (with `-v` also `* Failed to initial GSS-API token.` and `* Unable to negotiate SOCKS5 GSS-API context.`).

No KDC was available, so a successful RFC 1961 exchange could not be recorded; it follows curl's
`socks_sspi.c`/`socks_gssapi.c` and is pinned with `Fakes/ScriptedSecurityContextFactory`.

Decisions (ADR-0274, decided by Claude under Stewart's delegation):
- `Socks5AuthenticationOptions` is a `TcpConnector` constructor argument per option group, built by `Curl.Console`'s `Socks5AuthenticationMapping`, so no Abstractions contract changes.
- GSS-API uses the proxy tunnel's `LateBoundSecurityContextFactory` (ADR-0142's router): Kerberos, default credential, `EncryptAndSign`; service `--socks5-gssapi-service`, else `--proxy-service-name`, else `rcmd` (curl's default); a service with `/` is the whole target, split at the first `/`.
- Protection: curl offers level 0 (wrapped without encryption, bare under `--socks5-gssapi-nec`) and fails a proxy granting more with `SOCKS5 GSS-API protection not yet implemented.`; Curl does the same.
- `--delegation` applies only off Windows (SSPI's SOCKS code asks no delegation).
- Failure texts per platform build; SSPI's `SEC_E_TARGET_UNKNOWN` for no credential and for a refusal (the BCL's SSPI status is coarse); MIT's cache name from `KRB5CCNAME`, else `FILE:/tmp/krb5cc_<uid>` from `/proc/self/status`, 0 where absent. Wrap/unwrap/other status texts are curl's and SSPI's/MIT's standard texts, unmeasured.
- The `-v` lines of the GSS-API path (`Failed to initialize security context.` and so on) are not printed; nor are they for any other SOCKS failure today.

Follow-up filed: BL-1039 (the `-v` lines of a failed GSS-API negotiation).

`touches` gained `Documentation/Planning/Decisions` for ADR-0274 and its index row; the only other
task in Doing (BL-973) touches only the SSH projects.

Quality: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch,
0 failing members, worst CRAP 10; `-Library Curl.Console`: the same. Fast tests all green
(Networking 1749 passed, Console 1903 passed).

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SOCKS5 greetings follow --socks5-basic/--socks5-gssapi as measured, RFC 1929 and RFC 1961 GSS-API (Kerberos, NEC, protection negotiation) run on the security-context seam with each platform build's failure texts
