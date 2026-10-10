---
id: BL-1975
title: Close GF-0045: Through -p/-x the proxy server receives nothing in process: every <verify><proxy> case records no CONNECT
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2010, BL-2011]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1975 — Close GF-0045: Through -p/-x the proxy server receives nothing in process: every <verify><proxy> case records no CONNECT

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0045 (Through -p/-x the proxy server receives nothing in process: every <verify><proxy> case records no CONNECT), so a later gap analysis measures each of `behaviour:test80`, `behaviour:test83`, `behaviour:test95`, `behaviour:test275`, `behaviour:test744`, `behaviour:test1078`, `behaviour:test1184`, `behaviour:test1288`, `behaviour:test1297`, `behaviour:test1428`, `behaviour:test1904`, `behaviour:test1319`, `behaviour:test1320`, `behaviour:test1321`, `behaviour:test2107` as `match`.

## Context

- Finding: GF-0045, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test80`, `behaviour:test83`, `behaviour:test95`, `behaviour:test275`, `behaviour:test744`, `behaviour:test1078`, `behaviour:test1184`, `behaviour:test1288`, `behaviour:test1297`, `behaviour:test1428`, `behaviour:test1904`, `behaviour:test1319`, `behaviour:test1320`, `behaviour:test1321`, `behaviour:test2107`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test80: '<verify><proxy> differs at byte 0 (line 1): expected "CONNECT test.80:8990 HTTP/1.0\r\n", got the end'. test1078 expected 'CONNECT 127.0.0.1:8990 HTTP/1.0', got the end. test1319/1320/1321 (pop3/smtp/imap with -p -x) expected 'CONNECT pop.1319:8999 HTTP/1.1' (and smtp., imap.), got the end. test2107 (-p -x to a proxy that answers badly): expected exit code 8, got 0. The same cause as behaviour:in-process-runner-bypasses-tcp-connector: the IConnector overload sends the proxied request straight to the origin server, so the harness's proxy server log stays empty. Listed as passing in Curl.Conformance.UnitTests/PassingUpstreamCases.txt: 80, 83, 95, 275, 744, 1078, 1184, 1297, 1428, 1904. Not listed: 1288, 1319, 1320, 1321, 2107. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 80,1078,1288,1319,2107

Suggestion, copied from the finding:

Re-measure after the InProcessCurl rewiring of behaviour:in-process-runner-bypasses-tcp-connector. Then fix whatever still differs in Curl.Networking.UnitLibrary's HttpProxyTunnel. The cases are --suppress-connect-headers with -D - -i and %{http_connect} (test1288), a CONNECT tunnel for pop3, smtp and imap URLs (test1319-1321), and exit 8 for a proxy's malformed CONNECT reply (test2107). Pin each in Curl.Networking.UnitTests.

## Acceptance criteria

- [ ] `behaviour:test80`: Curl answers what curl 8.21.0 answers, `upstream test80 passes`, so the item measures `match`.
- [ ] `behaviour:test83`: Curl answers what curl 8.21.0 answers, `upstream test83 passes`, so the item measures `match`.
- [ ] `behaviour:test95`: Curl answers what curl 8.21.0 answers, `upstream test95 passes`, so the item measures `match`.
- [ ] `behaviour:test275`: Curl answers what curl 8.21.0 answers, `upstream test275 passes`, so the item measures `match`.
- [ ] `behaviour:test744`: Curl answers what curl 8.21.0 answers, `upstream test744 passes`, so the item measures `match`.
- [ ] `behaviour:test1078`: Curl answers what curl 8.21.0 answers, `upstream test1078 passes`, so the item measures `match`.
- [ ] `behaviour:test1184`: Curl answers what curl 8.21.0 answers, `upstream test1184 passes`, so the item measures `match`.
- [ ] `behaviour:test1288`: Curl answers what curl 8.21.0 answers, `upstream test1288 passes`, so the item measures `match`.
- [ ] `behaviour:test1297`: Curl answers what curl 8.21.0 answers, `upstream test1297 passes`, so the item measures `match`.
- [ ] `behaviour:test1428`: Curl answers what curl 8.21.0 answers, `upstream test1428 passes`, so the item measures `match`.
- [ ] `behaviour:test1904`: Curl answers what curl 8.21.0 answers, `upstream test1904 passes`, so the item measures `match`.
- [ ] `behaviour:test1319`: Curl answers what curl 8.21.0 answers, `upstream test1319 passes`, so the item measures `match`.
- [ ] `behaviour:test1320`: Curl answers what curl 8.21.0 answers, `upstream test1320 passes`, so the item measures `match`.
- [ ] `behaviour:test1321`: Curl answers what curl 8.21.0 answers, `upstream test1321 passes`, so the item measures `match`.
- [ ] `behaviour:test2107`: Curl answers what curl 8.21.0 answers, `upstream test2107 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-10 (lane 1), measured with `Curl.Conformance.UnitTests`' `UpstreamCase_RunThroughCurl_HoldsTheRatchet`, which runs in process over the production `TcpConnector` (BL-1831): test80, 83, 95, 275, 744, 1078, 1184, 1297, 1428 and 1904 already pass there and are listed. Their gap entries come from the gap office's `Gap/Tools/Measure-UpstreamCases.cs` measuring through `InProcessCurl.RunAsync`'s `IConnector` overload, which skips `TcpConnector`; switching that tool to the `ITcpDialer`/`IDnsResolver` overload is interactive-only work (Gap/ is closed to lanes and the board refuses a lane filing it), so an interactive session should do it before the next gap run, or BL-1997 should.
- test2107 fixed here: `HttpProxyTunnel.ReadReplyAsync` fails a reply header line holding a NUL byte with exit 8 `Nul byte in header` (curl 8.21.0's `lib/http.c` `verify_header` text), the head being the lines before it, as curl refuses the line before `-v` shows it. The upstream case expects only exit 8; the message and the head cut are taken from curl's source, not measured (no reference proxy that sends a NUL byte is at hand). 2107 added to `PassingUpstreamCases.txt`; `Curl.Conformance.UnitTests` added to touches for that (no task in Doing named it).
- test1288 left: `%{size_header}` lacks the CONNECT reply head's 61 bytes (231 expected, 170 got); needs `Curl.Protocol.Abstractions` and `Curl.Protocol.Http` (the latter held by BL-1959). Filed as BL-2010.
- test1319-1321 left: pop3/smtp/imap never put the proxy on their connect target, so nothing is sent; needs the mail protocol libraries (held by BL-1989). Filed as BL-2011.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Backlog. test2107 fixed; waits on BL-2010 (test1288, size_header needs Curl.Protocol.Http held by BL-1959) and BL-2011 (test1319-1321, mail libraries held by BL-1989)
- 2026-10-10: Backlog -> Doing.
