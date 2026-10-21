---
id: BL-2971
title: Write curl 8.22.0's DoH sub-transfer lines: [DoH] prefix, DoH request complete, AAAA probe first
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2970]
touches: [Curl.Output.UnitLibrary/DohSubTransferEvents.cs, Curl.Output.UnitTests/DohSubTransferEventsTests.cs, Curl.Engine.UnitTests/EngineCompositionDohSubTransferTraceTests.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitTests/DohDnsResolverSubTransferTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2971 — Write curl 8.22.0's DoH sub-transfer lines: [DoH] prefix, DoH request complete, AAAA probe first

## Goal

Under `-v --trace-config doh`, Curl writes each DoH sub-transfer's lines as curl 8.22.0 does: prefixed `[DoH] ` (not 8.21.0's `[DNS] `), ending `DoH request complete, N to go` (not `a DoH request is completed, N to go`), the AAAA query sent and traced before the A query, and `Connection #1 is not open enough, cannot reuse` where 8.22.0 writes it.

## Context

Split from BL-2970 (ADR-0738). Measured 2026-10-21 with the local curl 8.22.0 (Schannel) and `Record-CurlExchange.ps1 -NoServer -DohPort 18453 [-DnsResponseCode 3] -CurlArgs -v,--trace-config,doh,--doh-insecure,--doh-url,https://127.0.0.1:18453/dns-query,http://example.test:18454/`. Without `-4` (NXDOMAIN to both):

```
* [DoH] Connection #1 is not open enough, cannot reuse
* [DoH] Connection #1 to host 127.0.0.1:18453 left intact
* [DoH] [DoH] [AAAA] error decoding response: No such name
* [DoH] Connection #2 to host 127.0.0.1:18453 left intact
* [DoH] [DoH] [A] error decoding response: No such name
* Could not resolve host: example.test
```

With `-4` and an A answer: `* [DoH] Connection #1 ... left intact`, `* [DoH] DoH request complete, 0 to go`, `* Host example.test:18454 was resolved.`. Plain `-v` (no `--trace-config doh`) writes none of these. The `[DNS] ` prefix is added by `Curl.Output.UnitLibrary/DohSubTransferEvents.cs`; the completed line and the query order are in `DohDnsResolver.ReportOutcome` and `AddressQueries`. BL-2970 left the error lines as `[DoH] [DoH] [A] error ...` on the DoH trace sink; if the sub-transfer prefix becomes `[DoH] `, consider writing them through `SubTransferEvents` as `[DoH] [A] error ...` instead (8.22.0 writes them in the sub-transfer's context). Measure the full sub-transfer line set (connect, POST head, debug-only lines aside) before pinning.

## Acceptance criteria

- [ ] `DohDnsResolverSubTransferTests` and `EngineCompositionDohSubTransferTraceTests` pin the `[DoH] ` prefix, `DoH request complete, N to go` and the AAAA-first order, as measured with curl 8.22.0 and recorded in an ADR or an amendment to ADR-0738.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and the changed libraries keep 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
