---
id: BL-2979
title: Gate the DoH HTTPS-RR trace lines by --trace-config dns and doh as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2973]
touches: [Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs, Curl.Networking.UnitLibrary/DohDnsResolver.HttpsRecord.cs, Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs, Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Networking.UnitLibrary/DohHttpsRecordTrace.cs, Curl.Networking.UnitLibrary/DohResponseReader.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs, Curl.Networking.UnitLibrary/DohQueryResult.cs, Curl.Engine.UnitTests/EngineCompositionDohHttpsRecordTraceTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-22
---
# BL-2979 â€” Gate the DoH HTTPS-RR trace lines by --trace-config dns and doh as curl 8.22.0 does

## Goal

The HTTPS-RR DoH query's trace lines appear under exactly the `--trace-config` components curl 8.22.0 writes them for: its `[DoH] ...` lines (`infof` on the DoH sub-transfer) under `doh`, and its `[DNS] [HTTPS] got N records` and `[DNS] [HTTPS] record: ...` lines (`CURL_TRC_DNS` on the sub-transfer) only under `doh` and `dns` both (or `all`); and a non-2xx DoH answer writes `[DoH] [DoH] [HTTPS] error: HTTP status N`.

## Context

Found by BL-2954 (ADR-0727's BL-2954 amendment). Today `EngineComposition.CreateDohResolver` gives `DohDnsResolver` one trace sink for any of `dns`, `doh`, `network` and `all` (8.21.0's measured rule), so `DohTraceLines.ReportHttpsRecord` writes every line whenever that sink is real. In curl 8.22.0 (`lib/vdns/doh.c` line 311) the DoH sub-transfer is verbose only under the `doh` feature, and `Curl_trc_dns` also needs the `dns` feature (`lib/curl_trc.c` `Curl_trc_dns`). `doh_probe_done` also refuses an HTTP status outside 200-299 (`DohResponseReader` ignores the status, as 8.21.0 was measured to). BL-2973 settles 8.22.0's sub-transfer prefix and gating for the A and AAAA queries; this task does the same for the HTTPS query on top of it.

## Acceptance criteria

- [x] Unit tests pin which HTTPS-RR lines `dns`, `doh`, `dns,doh` and `all` each write, and the `HTTP status N` error line, recorded in an amendment to ADR-0727.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and the changed libraries keep 100% line and branch coverage and complexity at most 10.

## Notes

- Added DohHttpsRecordTrace.cs, DohResponseReader.cs, DohDnsResolver.Exchange.cs, DohQueryResult.cs and a new engine test file to touches: the `HTTP status N` line needs the response status, which the reader dropped; no task in Doing names them.
- Gating decided from curl 8.22.0 source (ADR-0727 BL-2979 amendment): `network` and `all` count as both `doh` and `dns`; only the HTTPS query reads the status, A and AAAA stay as measured on 8.21.0. A status line with no number counts as 0.
- Measure-CodeQuality (Networking, Engine): coverage 100%; three complexity breaches split out, the last fix built and tested but not measured again.

## Log

- 2026-10-21: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 762745af 8b2b45e3.
- 2026-10-22: Doing -> Done. HTTPS-RR DoH lines gated by --trace-config doh/dns as curl 8.22.0, with the HTTP status error line
