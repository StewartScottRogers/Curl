# ADR-0738 — DoH answers decode RCODE 3 as NXDOMAIN and cap TTLs at one day, as curl 8.22.0 does; CNAME data and the 8.22.0 trace lines follow in BL-2970

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2969
- Decided by Claude under Stewart's delegation.

## Context

`DnsAnswerDecoder` followed curl 8.21.0's `doh_resp_decode` (ADR-0152). Porting upstream's
unit1650 (BL-2923) showed curl 8.22.0's (`lib/vdns/doh.c`, tag `curl-8_22_0`, lines 581-768)
differs in three ways: RCODE 3 returns `DOH_DNS_NXDOMAIN` ("No such name" in `doh_code_str`), not
`DOH_DNS_BAD_RCODE`; each answer TTL is capped at `MAX_DNS_TTL`, 86400, before the smallest is
taken; and `doh_rdata` stores no CNAME or DNAME data and there is no "no content" result.

Reading 8.22.0's resolver half (`doh_probe_done`, `Curl_doh_take_result`) showed a fourth change
the task did not name: the A and AAAA `--trace-config doh` lines are no longer 8.21.0's
`[DNS] DoH: <failure> type A for <host>` and decoded entry (`[DNS] hostname:`, `[DoH] TTL:`,
`[DoH] A:`, `CNAME:`) but per-probe `[DoH] [A] error ...` and `DoH request complete, N to go` lines.
Skipping CNAME data and dropping `NoContent` changes those entry lines, so the two belong together.

## Decision

1. **This task makes the two small decoder changes.** `DnsAnswerDecoder` returns the new
   `DnsMessageFailure.NxDomain` for RCODE 3 (`DnsMessageFailureText`: "No such name"), and
   `BadRcode` for every other non-zero RCODE; each answer TTL is capped at 86400 seconds before the
   minimum. `NxDomain` goes at the end of the enumeration so the text table stays indexed by value.
2. **The exit code does not change.** 8.22.0's `Curl_doh_take_result` fails with
   `CURLE_COULDNT_RESOLVE_HOST` (6) when no probe gave an address, NXDOMAIN or not; NXDOMAIN only
   decides whether the failure is cached as a negative entry. `DohDnsResolver` already ends every
   DoH failure in exit 6 (ADR-0152). Curl keeps no negative cache entry for a DoH failure, which
   BL-2970 decides with the rest of 8.22.0's resolver.
3. **Until BL-2970 lands, the trace keeps 8.21.0's line form** with the new failure text:
   `[DNS] DoH: No such name type A for <host>`. Derived from the source, not measured: the line
   form is BL-2970's to measure and replace, so pinning 8.21.0's form against an 8.22.0 measurement
   would be measured twice.
4. **CNAME and NoContent go to BL-2970**, with the trace lines, measured with the local curl
   8.22.0 through `Record-CurlExchange.ps1 -Tls`. `DnsServerQuery` (the c-ares path) reads RCODE
   itself before decoding, so this task's changes do not reach it.

## Consequences

- unit1650's response rows 3 (NXDOMAIN) and 13 (TTL `0x00ffff37` gives 86400) decode as 8.22.0's do;
  row 11 (the self-pointing CNAME) still fails with `LabelLoop` until BL-2970.
- An NXDOMAIN DoH answer's trace reads `No such name` where it read `Bad RCODE`.

## Amendment 2026-10-21 (BL-3012): measured DoH decode and trace behaviour

Decided by Claude under Stewart's delegation. This amendment turns decision 4 into measured
facts and decisions. It records intent: BL-3011 applies the code changes, so until it lands the
code still follows decisions 1-3 above and the 8.21.0 trace lines.

### Measured

Measured with the local curl 8.22.0 (tag `curl-8_22_0`, `lib/vdns/doh.c`; Schannel, debug build)
through `Record-CurlExchange.ps1 -NoServer -DohPort 18453` (`-DnsResponseCode 3` for NXDOMAIN,
`-DnsAnswerAddress ::1` for an A answer with no A record) and, for the CNAME-only answer
(44 bytes, `example.test CNAME example.test`, TTL 60) and the HTTP 500, `-Tls -Response` as the DoH
server. Arguments: `-v --trace-config doh --doh-insecure --doh-url https://127.0.0.1:P/dns-query -4
http://example.test:18454/`. After `* [DoH] Connection #1 to host 127.0.0.1:P left intact`:

| DoH answer | Lines | Exit |
| --- | --- | --- |
| A answer | `* [DoH] DoH request complete, 0 to go`, `* Host example.test:18454 was resolved.` | 7 (at connect) |
| NXDOMAIN | `* [DoH] [DoH] [A] error decoding response: No such name` | 6 |
| CNAME-only answer | `* [DoH] DoH request complete, 0 to go` | 6 (`Could not resolve host`) |
| Empty answer | `* [DoH] DoH request complete, 0 to go` | 6 (`Could not resolve host`) |
| HTTP 500, empty body | `* [DoH] [DoH] [A] error: HTTP status 500` | 6 |

Without `-4` the AAAA probe is queried first: `Connection #1 is not open enough, cannot reuse`,
then the `[AAAA]` error, then connection #2 and the `[A]` error. Plain `-v` writes no DoH line.

### Decided

5. The decoder skips CNAME and DNAME data unread and never returns `NoContent`. The enum value
   stays, documented as unused, so `DnsMessageFailureText`'s value-indexed table keeps its places.
6. `DnsAnswer.CanonicalNames` and `DohQueryResult`'s CNAME room (and its NoContent rule) are
   removed; the shared 24-address room stays. The decoded-entry trace lines are gone.
7. `DohTraceLines.FailureLine` writes the decode error (`[DoH] [DoH] [A|AAAA] error decoding
   response: <doh_strerror>`) or the transfer error (`error: <curl_easy_strerror>`) on the DoH
   trace sink, in place of the completed line.
8. Left to other tasks: the `[DoH] ` sub-transfer prefix, the `DoH request complete` wording and
   the AAAA-first order are BL-2973; the non-2xx `HTTP status N` failure is BL-2972; BL-3011 applies
   decisions 5-7 in code and tests.
