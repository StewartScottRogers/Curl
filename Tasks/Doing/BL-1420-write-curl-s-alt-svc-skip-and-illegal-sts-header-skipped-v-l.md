---
id: BL-1420
title: Write curl's Alt-Svc skip and Illegal STS header skipped -v lines before their header line (ADR-0409)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1419]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary/Hsts, Curl.Core.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1420 — Write curl's Alt-Svc skip and Illegal STS header skipped -v lines before their header line (ADR-0409)

## Goal

`curl -v --hsts <file> --alt-svc <file> https://...` writes curl 8.21.0's `* Illegal STS header skipped`, `* Unknown alt-svc port number, ignoring.`, `* Bad alt-svc IPv6 hostname, ignoring.` and `* Bad alt-svc hostname, ignoring.` lines just before the header line that caused them.

## Context

- ADR-0409; contract from BL-1418, Core from BL-1419.
- `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` `StoreAltSvc` (called before the header line is reported) writes `Added alt-svc:`; a per-header HSTS step goes beside it, `https` only.
- `Curl.Console/CurlCommandRunner.cs` builds `HstsTransferPolicy` (`Hsts`) and calls `LearnFrom` after the transfer; `Curl.Console/AltSvcTransferCache.cs` implements `IAltSvcStore`.
- Measured 2026-10-03, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Tls`, `-sv -k --resolve h.test:PORT:127.0.0.1 --hsts <file> --alt-svc <file> https://h.test:PORT/a`.

## Acceptance criteria

- [ ] Reply header `Strict-Transport-Security: max-age=abc` over https to `h.test` gives `* Illegal STS header skipped` immediately followed by `< Strict-Transport-Security: max-age=abc`, pinned by a test.
- [ ] The same to an IP-address host, or over plain `http://`, writes no such line, pinned by tests.
- [ ] `Alt-Svc: h2=":abc"`, `h2="[::1]:99999"` and `h2="host:"` each give `* Unknown alt-svc port number, ignoring.` immediately before the `< Alt-Svc:` line; `h2="[::1:443"` gives `* Bad alt-svc IPv6 hostname, ignoring.`; over `http://` nothing; each pinned by a test.
- [ ] HSTS is learned per header during the transfer through `HttpRequestOptions.HstsStore`; `LearnFrom` has no caller left and is removed; the existing `--hsts` file tests pass unchanged.
- [ ] 100% line and branch coverage (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary,Curl.Console,Curl.Core.UnitLibrary` if Core changed).

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
