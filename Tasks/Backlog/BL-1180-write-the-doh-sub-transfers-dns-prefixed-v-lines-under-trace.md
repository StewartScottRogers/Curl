---
id: BL-1180
title: Write the DoH sub-transfers' [DNS]-prefixed -v lines under --trace-config dns
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1157]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1180 — Write the DoH sub-transfers' [DNS]-prefixed -v lines under --trace-config dns

## Goal

Under `-v --trace-config dns` (or `doh`, `all`) with `--doh-url`, each DoH sub-transfer's own `-v` lines reach stderr prefixed `[DNS] `, as curl 8.21.0 writes them; split out of BL-1157.

## Context

- BL-1157 Notes hold curl 8.21.0's measured stderr for a failed and a successful DoH resolve (`Record-CurlExchange.ps1 -Tls -Connections 2`). Per sub-transfer: `[DNS] created DNS filter for 127.0.0.1:P, transport=3, queries=3` (one prefix), `[DNS] [DNS] added`, `[DNS] [DNS] cf_dns_start host ...`, `[DNS]   Trying ...`, `[DNS] [DNS] Curl_conn_connect(block=0) -> 0, done=0`, the Schannel trust lines (`[DNS] schannel: ...`, `[DNS] ALPN: curl offers http/1.1`), `[DNS] ALPN: server did not agree on a protocol. Uses default.`, the connected filter lines, `[DNS] Established connection to ...`, `[DNS] using HTTP/1.x`, the `> POST` head and `< HTTP/1.1` head unprefixed, `} [30 bytes data]`, `[DNS] upload completely sent off: 30 bytes`, `{ [N bytes data]`, `[DNS] Connection #1 to host 127.0.0.1:P left intact`, then `[DNS] a DoH request is completed, 1 to go` (`0 to go` after the second).
- Why it was split: the structured events (`ReportConnectionOpened`, `ReportTlsTrust`, `ReportTlsHandshake`) are worded by `Curl.Output.UnitLibrary` (`TransferEventInfoText`, per TLS backend), so prefixing them needs a prefix in the output writers (like `TraceIdsPrefix`), not only in `Curl.Networking`. `DohDnsResolver.QueryAsync` today connects with `NoTransferEvents` and reports no head or data.
- The two sub-transfers run in parallel and curl interleaves their lines by poll timing; decide (ADR) a deterministic order, e.g. A's lines whole, then AAAA's. `Connection #1 is not open enough, cannot reuse` and the second sub-transfer's `Hostname 127.0.0.1 was found in DNS cache` are artefacts of that parallelism. curl numbers the DoH connections after the transfer's own `#0`.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` pin a failed and a successful DoH resolve's sub-transfer lines under `--trace-config dns` as measured in BL-1157 Notes, in the order the ADR decides, leaving out the poll-timing lines.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
