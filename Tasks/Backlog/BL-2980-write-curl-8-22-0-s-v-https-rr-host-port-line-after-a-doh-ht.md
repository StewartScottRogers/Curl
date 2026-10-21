---
id: BL-2980
title: Write curl 8.22.0's -v HTTPS-RR host:port line after a DoH HTTPS-RR resolve
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HttpsRecordReportLine.cs, Curl.Networking.UnitTests/HttpsRecordReportLineTests.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpConnectorHttpsRecordReportTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2980 — Write curl 8.22.0's -v HTTPS-RR host:port line after a DoH HTTPS-RR resolve

## Goal

Under plain `-v`, a direct connect to an `https://` origin resolved through `--doh-url` writes curl 8.22.0's `HTTPS-RR <host>:<port>: -`, `HTTPS-RR <host>:<port>: not applicable` or `HTTPS-RR <host>:<port>: <record>` line, in curl's place beside `Host <host>:<port> was resolved.`.

## Context

Found by BL-2954 (ADR-0727's BL-2954 amendment). curl 8.22.0's `lib/vdns/cf-dns.c` `cf_dns_report` (tag `curl-8_22_0`, lines 130-176) writes, for the HTTPS resolve's own DNS filter, `HTTPS-RR %s:%u: -` when there is no record, `not applicable` when `Curl_httpsrr_applicable` (`lib/vdns/httpsrr.c`: target empty or `.`, and no `port` or the origin's port) says no, else the record as `Curl_httpsrr_print` writes it, which `DohTraceLines.FormatHttpsRecord` already does (mandatory flag aside). It is `infof(data, ...)` on the main transfer, so plain `-v` writes it, not only `--trace-config`. `DohDnsResolver.ResolveHttpsOriginAsync` keeps the record per host and port; `TcpConnector` asks for it (`IHttpsOriginDnsResolver`). Where the line falls against `Host ... was resolved.` must be derived from `cf-dns.c` (the HTTPS resolve and the address resolve are separate filters) or measured on an HTTPSRR build (the Windows reference curl has none).

## Acceptance criteria

- [ ] Unit tests pin the three forms of the `HTTPS-RR` line and its place beside `Host <host>:<port> was resolved.`, as derived or measured and recorded in an amendment to ADR-0727.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
