---
id: BL-4080
title: Fail a proxied transfer whose host holds a slash with exit 27 as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4079]
touches: [Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerAbsoluteFormHostTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4080 â€” Fail a proxied transfer whose host holds a slash with exit 27 as curl does

## Goal

A transfer through an HTTP proxy to a host whose IDNA conversion holds a slash (`http://aâ„€b.test/` with `-x`) fails with exit 27 and `curl: (27) Out of memory` before sending a byte, as curl 8.18.0 with libidn2 2.3.8 does.

## Context

Measured in BL-4077's run (WSL Ubuntu, `LC_ALL=C.utf8`): with a socat fake proxy listening, `curl -x http://127.0.0.1:PORT http://aâ„€b.test/` exits 27 for U+2100, U+2101, U+2105 and U+2106, and the proxy receives nothing. With the proxy port closed, curl exits 7 (`Failed to connect to aa/cb.test port 80 via 127.0.0.1`), so the failure comes after the proxy connection opens, when curl builds the absolute-form request target. Direct transfers send `Host: aa/cb.test` (BL-4079). Find where Curl builds the proxy request target (start at `TransferPerformer.Setup.cs`, `HttpProtocolHandler.cs`) and narrow `touches` to the right file.

## Acceptance criteria

- [x] A test pins exit 27 with `curl: (27) Out of memory` and no bytes sent for a proxied transfer to `http://aâ„€b.test/`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-4077's run.
- 2026-10-29 lane 3: requeued before any edit. Two tasks carry BL-4079: the Done one (separate Curl's extensions, ADR-0999) satisfied depends-on, but the prerequisite this task needs is the other, Map U+2100 U+2101 U+2105 U+2106 to their slash text (Tasks/Doing on another lane, touches CurlUrlHostNameMapping.cs). Until it lands Curl refuses http://a℀b.test/ at URL parse (CurlUrlHostNameMappingStd3Tests pins the refusal), so no proxied transfer can reach the request target. Claim again only once that mapping task is in Done; then add the exit-27 check where the proxy absolute-form target is built.
- 2026-10-29 lane 2: BL-4081 (the mapping, renumbered from the clashing BL-4079) is Done. The target is built in the HTTP handler, not the Engine, and it must fail after the proxy connection opens (exit 7 when the proxy is closed), so the check is in `HttpProtocolHandler.ExchangeOnConnectionAsync`. Added `HttpProtocolHandler.cs`, `HttpUrlText.cs` (`WireHost` made internal) and the new test file to touches; no task in Doing on origin/work/dark-factory names them. `TransferPerformer.Setup.cs` was not changed. Decision in ADR-1001: refuse curl's whole `hostname_check` set, not only the slash; IPv6 literals exempt; CONNECT tunnels left as they are (not measured).
- On Windows `CurlUrlHost` reads a non-ASCII host as Windows-1252 bytes (`a℀b.test` gives `xn--ab-pia8903akua.test`, no slash), so the end-to-end test is `[OSCondition(Exclude, Windows)]`; the predicate `RefusesAbsoluteFormHost(string)` is tested on every platform. Measure-CodeQuality on Windows (`-Library Curl.Protocol.Http.UnitLibrary`) reports `AbsoluteFormTargetUnbuilt` at 0% and `ExchangeOnConnectionAsync` lines 1087-1088 uncovered for that reason, complexity 10 (within the gate); filed BL-4090 to cover it on Windows. The change is platform-neutral otherwise; Linux and macOS CI run the excluded test.
- Build clean; fast tests green (Curl.Protocol.Http.UnitTests 2621 passed, 45 skipped; whole solution no failures).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 68b26c89 779790c0.
- 2026-10-29: Doing -> Backlog. Waits on BL-4079 'Map U+2100 U+2101 U+2105 U+2106 to their slash text' (in Doing on another lane; its ID clashes with the Done BL-4079 extensions task): Curl still refuses http://a℀b.test/ at URL parse
- 2026-10-29: Backlog -> Doing. Criteria at claim: 869e9e21 779790c0.
- 2026-10-29: Doing -> Done. A proxied transfer whose wire host holds a character curl_url_set refuses (a℀b.test is aa/cb.test) fails with exit 27 Out of memory before sending a byte; Windows coverage left to BL-4090
