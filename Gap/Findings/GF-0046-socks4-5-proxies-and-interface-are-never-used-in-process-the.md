---
id: GF-0046
title: SOCKS4/5 proxies and --interface are never used in process: the request goes straight to the server and their exit codes (97, 7, 45) never arise
area: behaviour
key: behaviour:in-process-runner-bypasses-socks-and-interface-binding
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test702, behaviour:test703, behaviour:test704, behaviour:test705, behaviour:test716, behaviour:test728, behaviour:test729, behaviour:test713, behaviour:test714, behaviour:test715, behaviour:test1084, behaviour:test1085]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
task:
tasks: []
---
# GF-0046 - SOCKS4/5 proxies and --interface are never used in process: the request goes straight to the server and their exit codes (97, 7, 45) never arise

## Summary

Curl differs from upstream curl in behaviour: SOCKS4/5 proxies and --interface are never used in process: the request goes straight to the server and their exit codes (97, 7, 45) never arise.

## Evidence

Every item expects 'upstream test<N> passes'. test702/703: '<verify><errorcode>: expected exit code 97, got 7'. test704/705: expected exit code 7, got 52. test716/729: expected exit code 97, got 52. test728 (SOCKS5h with -L): expected the end, got 'GET / HTTP/1.1'. test1084/1085 (--interface with a non-existing name): expected exit code 45, got 7. test713/714/715 (FTP through SOCKS5, an HTTP tunnel or both, with --connect-to) do not finish within 20 seconds. SOCKS (Curl.Networking.UnitLibrary's SOCKS handshakes) and the --interface bind live in TcpConnector, which the gap tool's InProcessCurl call never builds. 702, 703, 704, 705, 716, 728, 729, 1084 and 1085 are listed as passing in Curl.Conformance.UnitTests/PassingUpstreamCases.txt; 713, 714 and 715 are not. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 702,704,716,728,1084,713

## Suggestion

Re-measure after the InProcessCurl rewiring of behaviour:in-process-runner-bypasses-tcp-connector. Then fix in Curl.Networking.UnitLibrary (TcpConnector and its SOCKS5 and HTTP-tunnel chain) whatever still keeps an ftp:// transfer through --proxy socks5://, --proxytunnel or --preproxy with --connect-to (test713-715) from finishing. Include its passive data connection, which must take the same proxy path. Pin it in Curl.Networking.UnitTests.

## Measurements

- 2026-10-10_0657: 12 of 12 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
