---
id: BL-2973
title: Write curl 8.22.0's DoH sub-transfer lines: [DoH] prefix, DoH request complete, AAAA probe first
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3011]
touches: [Curl.Output.UnitLibrary/DohSubTransferEvents.cs, Curl.Output.UnitTests/DohSubTransferEventsTests.cs, Curl.Engine.UnitTests/EngineCompositionDohSubTransferTraceTests.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs, Curl.Networking.UnitLibrary/DohDnsResolver.SubTransferTrace.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitTests/DohDnsResolverSubTransferTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.AnswerDecoding.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.ExchangeFailures.cs, Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-22
---
# BL-2973 â€” Write curl 8.22.0's DoH sub-transfer lines: [DoH] prefix, DoH request complete, AAAA probe first

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

- [x] `DohDnsResolverSubTransferTests` and `EngineCompositionDohSubTransferTraceTests` pin the `[DoH] ` prefix, `DoH request complete, N to go` and the AAAA-first order, as measured with curl 8.22.0 and recorded in an ADR or an amendment to ADR-0738.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and the changed libraries keep 100% line and branch coverage and complexity at most 10.

## Notes

- Plan: prefix constant `[DNS] ` -> `[DoH] ` in `DohSubTransferEvents`; completed line `DoH request complete, N to go`; the queries loop runs and reports from the last (AAAA) to the first (A), the results keeping A-then-AAAA order so `AsOneEntry` and the address order are unchanged. Decisions recorded as ADR-0738 amendment 9-13 (Decided by Claude under Stewart's delegation).
- Untraced, the two queries still start in parallel A first (not observable; flipping it broke ~10 answer-queue tests for no visible change); only their outcome lines are reported AAAA first.
- Error lines stay on the DoH trace sink as `[DoH] [DoH] [A|AAAA] error ...`: they already match 8.22.0, so moving them onto `SubTransferEvents` gained nothing.
- `Connection #1 is not open enough, cannot reuse` stays out: ADR-0380 leaves out every sub-transfer poll-timing line, and the acceptance criteria do not pin it.
- Touches widened (no task in Doing on origin/work/dark-factory names them): `DohDnsResolver.cs` (its SubTransferEvents doc comment names the old wording), and `DohDnsResolverTraceTests.AnswerDecoding.cs`, `.ExchangeFailures.cs` and `DohDnsResolverHttpsRecordTraceTests.cs` (they pin the old A-first order and completed wording).
- New test `DohDnsResolverSubTransferTests.ResolveAsync_WithSubTransferEventsAndBothAnswers_SendsAaaaFirstAndEndsEachWithDohRequestComplete`. Build clean with -warnaserror, fast tests green, Measure-CodeQuality for Curl.Networking.UnitLibrary and Curl.Output.UnitLibrary: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 829bc65e 8b2b45e3.
- 2026-10-22: Doing -> Done. DoH sub-transfer lines carry curl 8.22.0's [DoH] prefix, end 'DoH request complete, N to go' and run AAAA first
