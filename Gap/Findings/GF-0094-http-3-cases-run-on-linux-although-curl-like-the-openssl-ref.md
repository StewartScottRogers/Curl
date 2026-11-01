---
id: GF-0094
title: HTTP/3 cases run on Linux although Curl, like the OpenSSL reference curl, refuses --http3 and --http3-only there
area: behaviour
platform: linux
key: behaviour:http3-cases-run-though-http3-refused
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-15_1657
closed: 2026-10-17_2225
regression: false
items: [behaviour:test2500, behaviour:test2503]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
task: BL-2370
tasks: [BL-2370]
---
# GF-0094 - HTTP/3 cases run on Linux although Curl, like the OpenSSL reference curl, refuses --http3 and --http3-only there

## Summary

Curl differs from upstream curl in behaviour on linux: HTTP/3 cases run on Linux although Curl, like the OpenSSL reference curl, refuses --http3 and --http3-only there.

## Evidence

behaviour:test2500 (--http3 --resolve localhost:<port>:127.0.0.1 https://localhost:<port>/2500): expected 'upstream test2500 passes', actual '<verify><protocol> differs at byte 0 (line 1): expected "GET https://localhost:9019/2500 HTTP/1.1\r\n", got the end'. behaviour:test2503 (-w '%{header_json}\n' --http3-only ...): expected 'upstream test2503 passes', actual '<verify><stdout> differs at byte 0 (line 1): expected "HTTP/3 200 \r\n", got the end'. No request and no stdout: CommandLineOptionTable.cs:340-341 refuse --http3 and --http3-only with RefusedWithoutQuic on every reference build (BL-2332, GF-0089). The measurement's reference also has no QUIC library ('libcurl/8.22.0 OpenSSL/3.0.13 ... nghttp2/1.59.0', no ngtcp2 or nghttp3). The conformance harness still lists 'http/3' for UpstreamCurlPlatform.Unix and MacOS (UpstreamCurlPlatform.cs:71,74), so it runs these cases instead of skipping them. Windows skips them ('Curl lacks the feature http/3', excluded reference-lacks:http/3). Reproduce on linux: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data $TMPDIR/raw.json 2500 2503

## Suggestion

In Curl.Conformance.UnitLibrary, remove 'http/3' from UpstreamCurlPlatform.Unix and UpstreamCurlPlatform.MacOS. Fix the class remarks that say Curl still serves --http3 off Windows. Upstream's runtests.pl then skips cases needing http/3 on a build without QUIC, as it does for the reference, and they are excluded as on Windows. As an alternative, derive the feature from CommandLineOptions.ActsAsQuicBuild so it follows the CLI's refusal. Update UpstreamCurlPlatformTests. The product needs no change: Curl already matches the reference here. Explained by ADR-0540 (refuse --http3 on every reference build); the harness's feature list predates it (BL-2261).

## Measurements

- 2026-10-15_1657: 2 of 2 items are gaps.
- 2026-10-17_2225: 0 of 2 items are gaps.
- 2026-10-19_0804: 0 of 2 items are gaps.
- 2026-10-19_1329: 0 of 2 items are gaps.
- 2026-10-22_0148: 0 of 2 items are gaps.
- 2026-10-23_1637: 0 of 2 items are gaps.
- 2026-10-25_1542: 0 of 2 items are gaps.
- 2026-10-30_2103: 0 of 2 items are gaps.
- 2026-10-31_1828: 0 of 2 items are gaps.

## Log

- 2026-10-15_1657: Opened by gap-behaviour.
- 2026-10-15_1753: Filed BL-2370.
- 2026-10-17_2225: Closed: run 2026-10-17_2225 measured every item as match or excluded on linux.
