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
