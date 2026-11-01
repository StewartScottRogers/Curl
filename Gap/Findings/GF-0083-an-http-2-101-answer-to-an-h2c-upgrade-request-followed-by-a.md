---
id: GF-0083
title: An 'HTTP/2 101' answer to an h2c upgrade request followed by an HTTP/1.1 200 exits 0 instead of 8
area: behaviour
platform: linux
key: behaviour:h2c-upgrade-http2-status-line-accepted
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0751
closed: 2026-10-19_0804
regression: false
items: [behaviour:test1704]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-2715
tasks: [BL-2326, BL-2715]
---
# GF-0083 - An 'HTTP/2 101' answer to an h2c upgrade request followed by an HTTP/1.1 200 exits 0 instead of 8

## Summary

Curl differs from upstream curl in behaviour on linux: An 'HTTP/2 101' answer to an h2c upgrade request followed by an HTTP/1.1 200 exits 0 instead of 8.

## Evidence

behaviour:test1704 (http://127.0.0.1:<port>/1704 --http2, reply 'HTTP/2 101 OK' then 'HTTP/1.1 200 OK'): expected 'reference curl exits 8; stdout 0 bytes', actual '<verify><errorcode>: expected exit code 8, got 0'. Also a gap on macOS. Excluded on Windows (reference-lacks:h2c). Reproduce on linux: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data $TMPDIR/raw.json 1704

## Suggestion

In Curl.Protocol.Http.UnitLibrary, fail with exit 8 (CURLE_WEIRD_SERVER_REPLY) when a response on an HTTP/1.x connection names HTTP/2 in its status line, including the interim response to an h2c upgrade. Today HttpH2cUpgradeConnection switches only on 'HTTP/1.1 101'. Otherwise it reads on as HTTP/1.1, and HttpStatusLine.Parse takes 'HTTP/2 101' as an assumed interim answer before accepting the 200. Have the 1xx head record its version (Http1VersionSeen.VersionNamed) so that Http1VersionSeen.ThrowIfMismatched, or a direct check, refuses the 'HTTP/2' status line as curl does. Add a focused test in Curl.Protocol.Http.UnitTests (ADR-0482). This applies only on the OpenSSL builds, because the Schannel reference lacks h2c.

## Measurements

- 2026-10-15_0751: 1 of 1 items are gaps.
- 2026-10-15_1657: 1 of 1 items are gaps.
- 2026-10-17_2225: 1 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-15_0751: Opened by gap-behaviour.
- 2026-10-15_0900: Filed BL-2326.
- 2026-10-17_2318: Filed BL-2715.
- 2026-10-19_0804: Closed: run 2026-10-19_0804 measured every item as match or excluded on linux.
