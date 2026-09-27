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
completed:
---
# BL-329 — Choose the proxy again for each redirect hop

## Goal

Under `-L`, each redirect hop uses the proxy curl 8.21.0 chooses for the hop's own URL (`--noproxy`, `no_proxy` and the scheme's proxy variable), not the first URL's.

## Context

- Filed by BL-238 (2026-09-27). `CurlCommandRunner` chooses the proxy once per command-line URL (`TransferProxySelection`) and puts it in `HttpRequestOptions.ForwardProxy`; `RedirectFollower.HopHttp` copies it to every hop unchanged, so a redirect from `http://a` to `https://b` or to a `--noproxy` host keeps the first choice.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH (ADR-0009) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Measured: `-L -x <proxy> --noproxy b.test http://a.test/` redirected to `http://b.test/` - the second request's route (proxy or direct) is pinned in a test over a fake connector.
- [ ] Measured: `-L` with `http_proxy` set only, redirected from `http://` to `https://` - the second route is pinned.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library this task changes.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
