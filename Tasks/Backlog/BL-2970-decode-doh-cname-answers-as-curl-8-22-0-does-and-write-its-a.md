---
id: BL-2970
title: Decode DoH CNAME answers as curl 8.22.0 does and write its A and AAAA --trace-config doh lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitLibrary/DohTraceLines.ExchangeFailures.cs, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohDnsResolver.SubTransferTrace.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.ExchangeFailures.cs, Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs, Curl.Networking.UnitLibrary/DnsAnswer.cs, Curl.Networking.UnitLibrary/DnsMessageFailure.cs, Curl.Networking.UnitLibrary/DohQueryResult.cs, Curl.Networking.UnitLibrary/DohTraceLines.AnswerDecoding.cs, Curl.Networking.UnitLibrary/DnsServerQuery.cs, Curl.Networking.UnitLibrary/DohDnsResolver.AnswerDecoding.cs, Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.AnswerDecoding.cs, Curl.Networking.UnitTests/NetworkingAdversarialTests.cs, Curl.Networking.UnitTests/DohDnsResolverSubTransferTests.cs, Curl.Engine.UnitTests/EngineCompositionDohSubTransferTraceTests.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs, Documentation/Planning/Decisions/ADR-0738-doh-answers-decode-nxdomain-and-cap-ttls-as-curl-8-22-0-does.md]
requirement: none
created: 2026-10-21
completed:
---
# BL-2970 â€” Decode DoH CNAME answers as curl 8.22.0 does and write its A and AAAA --trace-config doh lines

## Goal

`DnsAnswerDecoder.Decode` skips CNAME and DNAME data and never reports `NoContent`, as curl 8.22.0's `doh_resp_decode` does, and `DohDnsResolver` writes the A and AAAA queries' `-v --trace-config doh` lines in curl 8.22.0's form, measured.

## Context

Split from BL-2969 (ADR-0738), which moved RCODE 3 to `NxDomain` ("No such name") and capped answer TTLs at 86400. curl 8.22.0's `lib/vdns/doh.c` (tag `curl-8_22_0`):

- `doh_rdata` stores only A, AAAA and HTTPS data; a CNAME (and a DNAME) is skipped, so unit1650's self-pointing CNAME row (46 bytes, `\x07\x03any\xc0\x27\x00`) decodes `DOH_OK` where Curl's decoder follows it to `LabelLoop`. There is no "no content" result: an answer with no address is `DOH_OK`. Keep SRV records for the c-ares path (`DnsServerQuery`/`DnsServerResolver`), which 8.22.0's DoH never asks; `DnsServerQuery.Read` must keep its measured c-ares outcomes (`NoData` for an answer without a record of the type asked for).
- The resolver's trace is no longer 8.21.0's `[DNS] DoH: <failure> type A for <host>` plus a `[DNS] hostname:` / `[DoH] TTL:` / `[DoH] A:` / `CNAME:` entry. Per probe 8.22.0 writes `[DoH] [A] error: <curl_easy_strerror>`, `[DoH] [A] error: HTTP status <n>` (a non-2xx status is now a failure, `DOH_HTTP_FAILED`), `[DoH] [A] error decoding response: <doh_strerror>`, or `DoH request complete, <n> to go`. Measure what else it prints (`Curl_dnsc_mk_addr2`, `lib/vdns/cf-dns.c`) with the local curl 8.22.0 and `Record-CurlExchange.ps1 -Tls` before pinning, for: an A answer, an NXDOMAIN answer, a CNAME-only answer, an empty answer, a 500 with an empty body.
- Then drop or rework what only 8.21.0 needed: `DnsAnswer.CanonicalNames`, `DnsMessageFailure.NoContent`, `DohQueryResult.AsOneEntry`'s NoContent rule and CNAME room, `DohTraceLines`' `CNAME:` lines. The exit code stays 6 when no query gave an address (`Curl_doh_take_result`).
- BL-2954 (the HTTPS-RR query's lines) also touches `DohTraceLines` and `DohDnsResolver`: whichever runs second builds on the other's line format.

If too big for one run, split it: the decoder change (CNAME skipped, no NoContent) first, the trace-line format second.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` pins that `DnsAnswerDecoder.Decode` gives `None` for unit1650's self-pointing CNAME row and for an answer with no record.
- [ ] An ADR (or an amendment to ADR-0738) records curl 8.22.0's measured `-v --trace-config doh` lines and exit code for an A answer, an NXDOMAIN answer, a CNAME-only answer and an empty answer, and `DohDnsResolverTraceTests` pins them.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-21, lane 1, first claim. **Measured** with the local curl 8.22.0 (Schannel, debug build;
  its `sending last upload chunk` and `Curl_xfer_send` lines are debug-only) through
  `Record-CurlExchange.ps1 -NoServer -DohPort 18453` (`-DnsResponseCode 3` for NXDOMAIN,
  `-DnsAnswerAddress ::1` for an A answer with no record) and, for the CNAME-only answer
  (44 bytes, `example.test CNAME example.test` TTL 60) and the 500, `-Tls -Response` as the DoH
  server; `-CurlArgs -v,--trace-config,doh,--doh-insecure,--doh-url,https://127.0.0.1:P/dns-query,-4,http://example.test:18454/`.
  After `* [DoH] Connection #1 to host 127.0.0.1:P left intact`:
  A answer: `* [DoH] DoH request complete, 0 to go`, `* Host example.test:18454 was resolved.` (exit 7 at connect);
  NXDOMAIN: `* [DoH] [DoH] [A] error decoding response: No such name`, exit 6;
  CNAME-only and empty answer: `* [DoH] DoH request complete, 0 to go`, exit 6 (`Could not resolve host`);
  500 empty body: `* [DoH] [DoH] [A] error: HTTP status 500`, exit 6.
  Without `-4` (NXDOMAIN): AAAA is queried first (`Connection #1 is not open enough, cannot reuse`,
  then `[AAAA]` error, then connection #2 and the `[A]` error). Plain `-v` writes no DoH line.
- **Decided** (Claude, under Stewart's delegation; to go into ADR-0738 as an amendment): the decoder
  skips CNAME and DNAME data unread and never returns `NoContent` (the enum value stays, documented
  as unused, so `DnsMessageFailureText`'s value-indexed table keeps its places); `DnsAnswer.CanonicalNames`
  and `DohQueryResult`'s CNAME room and NoContent rule are gone (the shared 24-address room stays);
  `DohTraceLines.FailureLine` writes `[DoH] [DoH] [A|AAAA] error decoding response: <doh_strerror>`
  or `error: <curl_easy_strerror>` on the DoH trace sink right after that query (traced) or in query
  order (untraced), in place of the completed line, and the decoded-entry lines are gone.
  Left to new tasks: the `[DoH] ` sub-transfer prefix, `DoH request complete` wording and AAAA-first
  order (BL-2973, in `Curl.Output`'s `DohSubTransferEvents`), and the non-2xx `HTTP status N` failure
  (BL-2972, `DohResponseReader`).
- **Requeued** before finishing: the change breaks the 8.21.0 DoH lines pinned in
  `Curl.Engine.UnitTests/EngineCompositionDohSubTransferTraceTests.cs` (lines 59-60, 77-80) and
  `EngineCompositionDohTests.cs` (lines 140-143), and BL-2921 in Doing touches all of
  `Curl.Engine.UnitTests`, so those files were added to `touches` with
  `DohDnsResolverSubTransferTests.cs` (its cut-short test loses the completed line) and ADR-0738.
  The code (decoder, DnsAnswer, DohQueryResult, DohTraceLines, DohDnsResolver, DnsServerQuery and the
  four Networking test files) is written and left in the shift's stash, not yet built. What is left:
  apply the stash, change those Engine lines to the new form (a decode failure is
  `* [DoH] [DoH] [AAAA] error decoding response: Unexpected TYPE` after its sub-transfer's lines, with
  no `a DoH request is completed` line for that query, and no hostname/TTL/A lines), amend ADR-0738,
  build, run the fast tests, and measure `Curl.Networking.UnitLibrary` once.

- 2026-10-21, lane 8, second claim. The stash did not apply because BL-2981 split `DohDnsResolver`,
  `DohTraceLines` and `DohDnsResolverTraceTests` into partial files, so their post-split files were
  added to `touches` (no task in Doing names them; BL-2980 touches only `HttpsRecordReportLine` and
  `TcpConnector`). **Carried over** into the working tree (left for the shift's stash): the eight
  stash files that applied cleanly (DnsAnswer, DnsAnswerDecoder, DnsMessageFailure, DnsServerQuery,
  DohQueryResult, DnsAnswerDecoderTests, DohDnsResolverSubTransferTests, NetworkingAdversarialTests)
  and the library side re-done on the split files: `DohTraceLines.cs` now holds `FailureLine`,
  `DohTraceLines.AnswerDecoding.cs` only `DecodeFailureLine` (FormatAddress and the decoded-entry
  lines gone), `DohTraceLines.ExchangeFailures.cs` deleted, `ReportOutcome` in
  `DohDnsResolver.SubTransferTrace.cs`, and the `Report` call dropped from `DohDnsResolver.cs`.
  `Curl.Networking.UnitLibrary` builds clean. **Left**: port the stash's
  `DohDnsResolverTraceTests.cs` diff (`git diff 0725a3f9^1 0725a3f9 -- Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs`)
  onto the three split test files (`FormatAddress` tests go), change the Engine tests' 8.21.0 lines
  (see the requeue note above), amend ADR-0738, build, fast tests, one quality run. Requeued only
  because this claim's cost cap ($1.57) could not cover that.

- 2026-10-21, lane 2, third claim. The stash applied cleanly (git diff --stat matches the second claim's 13 files). Requeued at once with nothing new done: this claim's cost cap was $1.02, too small for the trace-test port, the Engine test lines, the ADR-0738 amendment, a build, the fast tests and a quality run. The next claim needs a cap of about $2.50 or more; what is left is unchanged from the second claim's note.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 582ece38 47a1bf8a 2a446683.
- 2026-10-21: Doing -> Backlog. Needs Curl.Engine.UnitTests (EngineCompositionDoh*Tests pin the 8.21.0 DoH lines), which BL-2921 in Doing touches; code is written and stashed, see Notes
- 2026-10-21: Backlog -> Doing. Criteria at claim: 582ece38 47a1bf8a 2a446683.
- 2026-10-21: Doing -> Backlog. Cost cap: library side ported onto BL-2981's split files and builds; trace and Engine tests, ADR amendment and quality run left, see Notes
- 2026-10-21: Backlog -> Doing. Criteria at claim: 582ece38 47a1bf8a 2a446683.
- 2026-10-21: Doing -> Backlog. Cost cap: this claim's $1.02 cap cannot cover the trace and Engine tests, ADR amendment, build, tests and quality run; work stays in the stash, see Notes
