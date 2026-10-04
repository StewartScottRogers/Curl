---
id: BL-1420
title: Write curl's Alt-Svc skip and Illegal STS header skipped -v lines before their header line (ADR-0409)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1419]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary/Hsts, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
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

- [x] Reply header `Strict-Transport-Security: max-age=abc` over https to `h.test` gives `* Illegal STS header skipped` immediately followed by `< Strict-Transport-Security: max-age=abc`, pinned by a test.
- [x] The same to an IP-address host, or over plain `http://`, writes no such line, pinned by tests.
- [x] `Alt-Svc: h2=":abc"`, `h2="[::1]:99999"` and `h2="host:"` each give `* Unknown alt-svc port number, ignoring.` immediately before the `< Alt-Svc:` line; `h2="[::1:443"` gives `* Bad alt-svc IPv6 hostname, ignoring.`; over `http://` nothing; each pinned by a test.
- [x] HSTS is learned per header during the transfer through `HttpRequestOptions.HstsStore`; `LearnFrom` has no caller left and is removed; the existing `--hsts` file tests pass unchanged.
- [x] 100% line and branch coverage (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary,Curl.Console,Curl.Core.UnitLibrary` if Core changed).

## Notes

- Handler: `StoreAltSvc` now writes one line per outcome in header order (`Added alt-svc:` or the `lib/altsvc.c` skip text); new `StoreHsts` hands each `Strict-Transport-Security` header of an `https` response to `options.HstsStore` and writes `Illegal STS header skipped` on `false`, both before the `< ` header line. Tests: `HttpProtocolHandlerTests.SkippedAltSvcAndHsts.cs`; the IP-host case is the store's answer (`true`), pinned by `HstsTransferPolicyTests.StoreFromResponse_IllegalHeaderOnAnIpAddressHost_ReturnsTrueAndStoresNothing`.
- Console: `AltSvcTransferCache.StoreFromResponse` returns `AltSvcCache.ApplyHeader`'s outcomes as they are. `TransferContextFactory.HstsStore` is set by the runner to its `Hsts` policy right after the diagnostic log opens, and every context's `HttpRequestOptions` carries it; redirect hops keep it through `with`. As before (ADR-0218) the cache is on with or without `--hsts`.
- `touches` widened to `Curl.Core.UnitLibrary/RedirectFollower.cs`: it was `LearnFrom`'s only production caller. No other task was in Doing on `origin/work/dark-factory`. `RedirectFollowerHstsTests`' scripted handler now learns through the hop's `HstsStore`, as the HTTP handler does.
- Coverage (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary,Curl.Console,Curl.Core.UnitLibrary`): 100/100 line/branch on all three, 0 failing members.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. curl -v writes Illegal STS header skipped and the Alt-Svc skip lines just before their header line; HSTS is learned per header
