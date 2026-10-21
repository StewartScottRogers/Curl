---
id: BL-2954
title: Write the -v and --trace-config doh lines of the DoH HTTPS-RR query as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2954 — Write the -v and --trace-config doh lines of the DoH HTTPS-RR query as curl 8.22.0 does

## Goal

Under `-v --trace-config doh` (and `dns`, `all`), the DoH HTTPS-RR query `DohDnsResolver.ResolveHttpsOriginAsync` sends for an `https://` origin writes the lines curl 8.22.0's HTTPSRR build writes for it, measured, in curl's order beside the A and AAAA lines.

## Context

Found by BL-2920 (2026-10-21). ADR-0727 made `--doh-url` send the HTTPS (type 65) query first for a direct `https://` origin, but it runs on `NoTransferEvents.Instance`: no sub-transfer lines (`DohSubTransferEvents`), no `DoH request complete, N to go`, and nothing from `DohTraceLines`. curl 8.22.0 (`lib/vdns/doh.c`, tag `curl-8_22_0`) writes `[DoH] [HTTPS] error ...` lines, `[HTTPS] got N records` (`CURL_TRC_DNS`), `DoH request complete, %u to go` per resolve (the HTTPS query is its own resolve, so its count is separate from A+AAAA's), `Curl_httpsrr_trace`, and `lib/vdns/cf-dns.c` writes `HTTPS-RR <host>:<port>: -`, `not applicable` or the record. The DoH connection numbering (`_connectionCount`) now counts the HTTPS query's connection first. The reference curl on Windows (Git for Windows 8.22.0) has no HTTPSRR feature, so measure on an HTTPSRR build (the OpenSSL build in CI or curl.se's) or derive from the source and say so in an ADR amendment to ADR-0727.

## Acceptance criteria

- [x] An amendment to ADR-0727 records the lines curl 8.22.0 writes for the HTTPS-RR DoH query and how they were measured or derived.
- [x] Unit tests in `Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs` pin those lines and their order with the A and AAAA lines.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Derived from curl 8.22.0 source (tag `curl-8_22_0`: `lib/vdns/doh.c` `doh_probe_done` and `doh_resp_decode_httpsrr`, `lib/vdns/httpsrr.c`, `lib/curl_trc.c`), not measured: the Windows reference curl has no HTTPSRR feature. Prefix form confirmed against BL-2973's measured A/AAAA lines on the same build. Recorded in ADR-0727's BL-2954 amendment.
- The HTTPS query now runs on `SubTransferEvents` (connection #1), and `DohTraceLines.ReportHttpsRecord` writes its lines verbatim on the doh trace sink before the A and AAAA lines; this also covers `ResolveHttpsRecordAsync` (the `--ech` path), the same resolve in curl.
- Choice: the `[DNS] [HTTPS]` lines are written whenever the doh trace sink is real (Curl's one sink for dns/doh/network/all); 8.22.0's finer gating and the `HTTP status N` error are BL-2979 (after BL-2973). The main-transfer `-v` line `HTTPS-RR host:port: ...` (`cf-dns.c`) is BL-2980.
- Quality: Curl.Networking.UnitLibrary 100% line, 100% branch, 0 failing members (worst CRAP 10) after splitting `FormatHttpsRecord` and making the CURLcode map a table.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: f7fac918 07984293 8cebb2bf.
- 2026-10-21: Doing -> Done. The DoH HTTPS-RR query writes curl 8.22.0's sub-transfer, [DoH] and [DNS] [HTTPS] trace lines before the A and AAAA lines
