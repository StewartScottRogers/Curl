---
id: BL-329
title: Choose the proxy again for each redirect hop
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-329 — Choose the proxy again for each redirect hop

## Goal

Under `-L`, each redirect hop uses the proxy curl 8.21.0 chooses for the hop's own URL (`--noproxy`, `no_proxy` and the scheme's proxy variable), not the first URL's.

## Context

- Filed by BL-238 (2026-09-27). `CurlCommandRunner` chooses the proxy once per command-line URL (`TransferProxySelection`) and puts it in `HttpRequestOptions.ForwardProxy`; `RedirectFollower.HopHttp` copies it to every hop unchanged, so a redirect from `http://a` to `https://b` or to a `--noproxy` host keeps the first choice.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH (ADR-0009) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Measured: `-L -x <proxy> --noproxy b.test http://a.test/` redirected to `http://b.test/` - the second request's route (proxy or direct) is pinned in a test over a fake connector.
- [x] Measured: `-L` with `http_proxy` set only, redirected from `http://` to `https://` - the second route is pinned.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library this task changes.

## Notes

Measured 2026-09-27, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Port 18329 -Connections 2`:

1. Response `HTTP/1.1 302 Found\r\nLocation: http://b.test:18329/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n`,
   args `-sS -L --max-redirs 1 --resolve b.test:18329:127.0.0.1 -x http://127.0.0.1:18329 --noproxy b.test http://a.test/`.
   request.bin: `GET http://a.test/ HTTP/1.1\r\nHost: a.test\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n`
   then `GET / HTTP/1.1\r\nHost: b.test:18329\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n` - the second hop goes **direct**
   (exit 47 only because the canned 302 repeats). Pinned by `CurlCompositionProxyTests.RunAsync_RedirectToANoProxyHost_SendsTheSecondRequestDirectly`.
2. Only `http_proxy=http://127.0.0.1:18329` set (no `https_proxy`/`all_proxy`), response 302 to `https://b.test:18329/`,
   args `-sS -L --max-redirs 1 --resolve b.test:18329:127.0.0.1 http://a.test/`. request.bin: the absolute-form
   `GET http://a.test/` to the proxy, then a TLS ClientHello (`16 03 01 ...`, SNI `b.test`) with no CONNECT - the
   https hop goes **direct** (exit 35 because the listener does not speak TLS). Pinned by
   `CurlCompositionProxyTests.RunAsync_RedirectFromHttpToHttpsWithOnlyHttpProxySet_ConnectsToTheHttpsHostDirectly`.

Design (sensible default, no ADR needed: the behaviour is measured, only the seam is chosen): `RedirectFollower`
takes an optional `HopProxySelector` delegate (new file `Curl.Core.UnitLibrary/HopProxySelector.cs`). Each hop
after the first asks it for the hop URL's proxy and sets the result as both `ITransferContext.Proxy` and
`HttpRequestOptions.ForwardProxy`; a selector failure ends the chain with that failure. Without a selector every
hop keeps the first URL's proxy - and now also carries `ITransferContext.Proxy`, which `NextHop` previously dropped.
`CurlCommandRunner` passes a selector that runs `TransferProxySelection.TrySelect` (so `-U` and the `file` rule
apply per hop too). The stop checks moved into `RedirectFollower.StopBeforeHop` to keep `FollowChainAsync` at
complexity 10. `Curl.Console`'s coverage is 100% with `-IncludeIntegration`; without it, the pre-existing
`DiskWriteOutFileOpener.TryOpen` (Integration-only tests, BL-280) shows as uncovered - not touched here.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Under -L each redirect hop chooses its proxy from its own URL (--noproxy, scheme proxy variables), as curl 8.21.0 does
