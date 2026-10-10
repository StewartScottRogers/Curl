---
id: BL-1976
title: Close GF-0046: SOCKS4/5 proxies and --interface are never used in process: the request goes straight to the server and their exit codes (97, 7, 45) never arise
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1976 — Close GF-0046: SOCKS4/5 proxies and --interface are never used in process: the request goes straight to the server and their exit codes (97, 7, 45) never arise

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0046 (SOCKS4/5 proxies and --interface are never used in process: the request goes straight to the server and their exit codes (97, 7, 45) never arise), so a later gap analysis measures each of `behaviour:test702`, `behaviour:test703`, `behaviour:test704`, `behaviour:test705`, `behaviour:test716`, `behaviour:test728`, `behaviour:test729`, `behaviour:test713`, `behaviour:test714`, `behaviour:test715`, `behaviour:test1084`, `behaviour:test1085` as `match`.

## Context

- Finding: GF-0046, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test702`, `behaviour:test703`, `behaviour:test704`, `behaviour:test705`, `behaviour:test716`, `behaviour:test728`, `behaviour:test729`, `behaviour:test713`, `behaviour:test714`, `behaviour:test715`, `behaviour:test1084`, `behaviour:test1085`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test702/703: '<verify><errorcode>: expected exit code 97, got 7'. test704/705: expected exit code 7, got 52. test716/729: expected exit code 97, got 52. test728 (SOCKS5h with -L): expected the end, got 'GET / HTTP/1.1'. test1084/1085 (--interface with a non-existing name): expected exit code 45, got 7. test713/714/715 (FTP through SOCKS5, an HTTP tunnel or both, with --connect-to) do not finish within 20 seconds. SOCKS (Curl.Networking.UnitLibrary's SOCKS handshakes) and the --interface bind live in TcpConnector, which the gap tool's InProcessCurl call never builds. 702, 703, 704, 705, 716, 728, 729, 1084 and 1085 are listed as passing in Curl.Conformance.UnitTests/PassingUpstreamCases.txt; 713, 714 and 715 are not. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 702,704,716,728,1084,713

Suggestion, copied from the finding:

Re-measure after the InProcessCurl rewiring of behaviour:in-process-runner-bypasses-tcp-connector. Then fix in Curl.Networking.UnitLibrary (TcpConnector and its SOCKS5 and HTTP-tunnel chain) whatever still keeps an ftp:// transfer through --proxy socks5://, --proxytunnel or --preproxy with --connect-to (test713-715) from finishing. Include its passive data connection, which must take the same proxy path. Pin it in Curl.Networking.UnitTests.

## Acceptance criteria

- [ ] `behaviour:test702`: Curl answers what curl 8.21.0 answers, `upstream test702 passes`, so the item measures `match`.
- [ ] `behaviour:test703`: Curl answers what curl 8.21.0 answers, `upstream test703 passes`, so the item measures `match`.
- [ ] `behaviour:test704`: Curl answers what curl 8.21.0 answers, `upstream test704 passes`, so the item measures `match`.
- [ ] `behaviour:test705`: Curl answers what curl 8.21.0 answers, `upstream test705 passes`, so the item measures `match`.
- [ ] `behaviour:test716`: Curl answers what curl 8.21.0 answers, `upstream test716 passes`, so the item measures `match`.
- [ ] `behaviour:test728`: Curl answers what curl 8.21.0 answers, `upstream test728 passes`, so the item measures `match`.
- [ ] `behaviour:test729`: Curl answers what curl 8.21.0 answers, `upstream test729 passes`, so the item measures `match`.
- [ ] `behaviour:test713`: Curl answers what curl 8.21.0 answers, `upstream test713 passes`, so the item measures `match`.
- [ ] `behaviour:test714`: Curl answers what curl 8.21.0 answers, `upstream test714 passes`, so the item measures `match`.
- [ ] `behaviour:test715`: Curl answers what curl 8.21.0 answers, `upstream test715 passes`, so the item measures `match`.
- [ ] `behaviour:test1084`: Curl answers what curl 8.21.0 answers, `upstream test1084 passes`, so the item measures `match`.
- [ ] `behaviour:test1085`: Curl answers what curl 8.21.0 answers, `upstream test1085 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
