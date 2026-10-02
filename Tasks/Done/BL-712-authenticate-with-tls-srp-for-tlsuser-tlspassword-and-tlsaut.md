---
id: BL-712
title: Authenticate with TLS-SRP for --tlsuser, --tlspassword and --tlsauthtype on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-704, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-712 — Authenticate with TLS-SRP for --tlsuser, --tlspassword and --tlsauthtype on every platform

## Goal

`--tlsuser u --tlspassword p` (with `--tlsauthtype SRP`, the only type and the default) authenticates the TLS connection with SRP on every platform, and `--proxy-tlsuser`, `--proxy-tlspassword` and `--proxy-tlsauthtype` do the same for an HTTPS proxy, with curl 8.21.0's messages and exit codes for a wrong password and a server without SRP.

## Context

- Conformance audit 2026-09-28, row 18; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): curl's OpenSSL and GnuTLS builds offer TLS-SRP, so Curl does everywhere. Parsing: BL-618 (and the proxy forms: BL-605 parses the HTTPS-proxy TLS options; if the three proxy SRP options are not among them, parse them here). Routing: BL-617's ADR and BL-708; the exchange: BL-704.
- Measure with an OpenSSL build of curl against `openssl s_server -srpvfile` through `Record-CurlExchange.ps1 -NoServer`: right and wrong password, and a server without SRP; stderr and exit code copied into Notes.

## Acceptance criteria

- [x] Measured first as above; copied into Notes.
- [x] Tests pin a successful SRP transfer against the in-memory SRP server, the measured failures, and the proxy options reaching only the proxy's handshake. (The success is `Curl.Tls.UnitTests`' `Tls12SrpHandshakeTests` against its in-memory SRP server; the failures are `HandBuiltTlsProviderTests.TlsSrp`; the origin's SRP options never reaching the proxy is `ProxyFromCommandLine_TheTenTlsOptionsOfAdr0151_NeverReachTheProxy`. Parsing the three `--proxy-tls*` options moved to BL-1127, see Notes.)
- [x] ~~`curl -V` lists `TLS-SRP` among the features (ADR-0021), with a test.~~ Moved to BL-1127: `CurlVersionText` is in `Curl.Cli.UnitLibrary`, which BL-1099 held.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measurement (2026-10-01), curl 8.18.0 with OpenSSL 3.5.5 in WSL against `openssl s_server` (Record-CurlExchange.ps1 has no SRP server, and its `-NoServer` form runs the Windows Schannel curl, which has no SRP, so the curl and s_server were run directly in WSL):
  - SRP server that does not know the user: exit 35, `curl: (35) TLS connect error: error:0A00045B:SSL routines::tlsv1 alert unknown psk identity`.
  - TLS 1.2 server without SRP: exit 35, `curl: (35) TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure` (curl offers only the `SRP` list below TLS 1.3, so no suite is shared). A TLS 1.3 server completes the handshake without SRP.
  - `--tlsuser alice` with no `--tlspassword`: exit 43, `* Using TLS-SRP username: alice`, then `curl: (43) failed setting SRP password`, after the TCP connect and before any hello.
  - `-v` with both: `* Using TLS-SRP username: alice`, then `* Setting cipher list SRP`, both before the hello.
  - `openssl ciphers -stdname SRP`: `0xc022, 0xc021, 0xc020, 0xc01f, 0xc01e, 0xc01d` after the TLS 1.3 suites.
  - Right and wrong password could not be measured: OpenSSL 3.5.5's `s_server -srpvfile` lookup is broken. With `-www` it answers `unknown_psk_identity` to every user. Without it, it prints `User <garbage> doesn't exist` and does the same (it crashed once with a segfault). The success is pinned in `Curl.Tls.UnitTests` (BL-704). The wrong password is OpenSSL's server `decrypt_error` for a Finished that does not verify, worded from OpenSSL's alert reason table (`tlsv1 alert decrypt error`, 0x41B).
- Decisions: ADR-0328. `TlsClientRouting` routes `TlsUser` to the hand-built client. `TlsSrp` builds the login, reports the two `-v` lines and swaps the TLS 1.2 suites for the SRP list unless `--ciphers` is given. With no password the connect fails with exit 43. Every SRP exit 35 is the OpenSSL build's line, on Windows too. `TlsFailureMessages` gains `unknown_psk_identity`; the `reason(N)` fallback test now uses `ech_required`.
- Split: the three `--proxy-tls*` options are parsed nowhere yet, and `curl -V`'s features line is in `Curl.Cli.UnitLibrary`. BL-1099, in Doing on the shared branch, touches that project, so both went to BL-1127 (depends on BL-712) rather than widening `touches`. Networking needs no change for the proxy: its options take the same route. `Curl.Console` needed no change, because `TlsClientOptionsMapping` already maps the origin options and keeps them off the proxy.
- Results: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Networking.UnitTests 2380 passed). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. `Choose` was split to stay at complexity 10 or below.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --tlsuser/--tlspassword authenticate with TLS-SRP through the hand-built client on every platform, with curl's OpenSSL-build lines and exit codes
