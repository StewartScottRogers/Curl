---
id: BL-2972
title: Fail a DoH query on a non-2xx status as curl 8.22.0 does: [DoH] [A] error: HTTP status N
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3011]
touches: [Curl.Networking.UnitLibrary/DohResponseReader.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs, Curl.Networking.UnitLibrary/DohTraceLines.ExchangeFailures.cs, Curl.Networking.UnitTests/DohResponseReaderTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.ExchangeFailures.cs, Curl.Networking.UnitLibrary/DohQueryResult.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2972 â€” Fail a DoH query on a non-2xx status as curl 8.22.0 does: [DoH] [A] error: HTTP status N

## Goal

A DoH response with a non-2xx status fails that query as curl 8.22.0's `DOH_HTTP_FAILED` does, without decoding the body, and under `-v --trace-config doh` writes `[DoH] [DoH] [A] error: HTTP status <n>` (`[AAAA]` for the AAAA query).

## Context

Split from BL-2970 (ADR-0738). Measured 2026-10-21 with the local curl 8.22.0 (Schannel): `Record-CurlExchange.ps1 -Tls -Port 18456 -Response 'HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n' -CurlArgs -v,--trace-config,doh,--doh-insecure,--doh-url,https://127.0.0.1:18456/,-4,http://example.test:18454/` wrote `* [DoH] Connection #1 to host 127.0.0.1:18456 left intact`, then `* [DoH] [DoH] [A] error: HTTP status 500`, and exited 6. 8.21.0 ignored the status and decoded the empty body as `Too small`. `DohResponseReader` (BL-641) ignores the status today, so it must return it; `DohTraceLines.FailureLine` writes BL-2970's error lines and gains this one. Check whether a 3xx or 1xx counts as a failure in `lib/vdns/doh.c` (tag `curl-8_22_0`) before pinning.

## Acceptance criteria

- [ ] `DohDnsResolverTraceTests` pins `[DoH] [DoH] [A] error: HTTP status 500` for a 500 with an empty body, with no address resolved, and `DohResponseReaderTests` pins the status the reader returns.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-22 (lane 3, before any code): `lib/vdns/doh.c` at `curl-8_22_0` (`doh_probe_done`, lines 1072-1083) fails a probe with `DOH_HTTP_FAILED` and `infof "[DoH] [%s] error: HTTP status %d"` whenever `httpcode < 200 || httpcode >= 300`, after the body has been read and before `doh_resp_decode`; so 1xx (as a final status) and 3xx fail too, exactly like a failed exchange (`result != 0`, the same `DOH_HTTP_FAILED`). Plan: `DohResponseReader` parses the status code from the status line and returns it with the body (a status line without three digits stays a receive failure); `QueryAsync` writes `Connection #N ... left intact` and then returns a failed `DohQueryResult` (Answer null, so it counts as answered like an exchange failure) carrying the status (new optional `HttpStatus` on `DohQueryResult`); `FailureLine` (and `HttpsRecordLines`, which today dereferences `Answer!` when `ExchangeFailure` is Ok) write `error: HTTP status <n>` through a helper in the new `DohTraceLines.ExchangeFailures.cs` partial.
- Touches widened: the line is built in `DohTraceLines.FailureLine` (`DohTraceLines.cs`), which needs a one-line call into the new partial; `DohQueryResult.cs` must carry the status; `DohTraceLines.HttpsRecord.cs` must not take an HTTP failure for a decoded answer. `DohTraceLines.cs` is in BL-2973's `touches` (Doing on lane 2), so the task goes back to Backlog until BL-2973 lands.

## Log

- 2026-10-21: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: a401cbb2 2a446683.
- 2026-10-22: Doing -> Backlog. Needs Curl.Networking.UnitLibrary/DohTraceLines.cs, which BL-2973 (Doing) touches; requeue once BL-2973 lands
- 2026-10-22: Backlog -> Doing. Criteria at claim: a401cbb2 2a446683.
