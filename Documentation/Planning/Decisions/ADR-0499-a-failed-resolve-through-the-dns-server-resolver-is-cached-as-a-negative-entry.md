# ADR-0499: A failed resolve through the DNS server resolver is cached as a negative entry

- Status: Accepted
- Date: 2026-10-14
- Task: BL-2262
- Decided by Claude under Stewart's delegation

## Context

Upstream case 2104 fetches `http://examplehost.invalid/2104` three times with `CURL_DNS_SERVER`
pointing at a dnsd that answers no records, and expects dnsd to see one A and one AAAA query:
curl 8.22.0's c-ares build keeps a failed resolve as a negative DNS cache entry, so the later
transfers fail with exit 6 without asking again. Curl kept nothing for a failed resolve and asked
three times.

Measured on curl 8.22.0's Schannel build on Windows (threaded system resolver), on 2026-10-14:
`curl -v http://nohost.invalid/a http://nohost.invalid/b` writes `Could not resolve host:` twice
and `Could not resolve: H:P` for both transfers, with no `Negative DNS entry` line, so that build
looks the name up again. `--dns-cache-timeout` is not an option of that build.

## Decision

- `TcpConnector` stores an entry with no addresses for a host and port whose look-up answered none
  when its resolver is `DnsServerResolver`, Curl's counterpart of the c-ares path
  (`--dns-servers`, and `CURL_DNS_SERVER` behind the harness seam), unless the failure was
  `DnsLookupFailure.BadConfiguration` (exit 43, nothing was asked).
- A later connect to that host and port meets the entry as it already meets an empty one: `Negative
  DNS entry`, then `Could not resolve host: H`, `Could not resolve: H:P`, `Could not resolve: H`
  and exit 6 (ADR-0381's negative-entry lines).
- The system resolver and DoH keep nothing for a failed resolve, as measured above.
- The entry lives as long as the run's `DnsCache`; Curl has no cache timeout to expire it, and a
  command line runs well inside curl's 60-second default.

## Consequences

Upstream case 2104 passes. Each `--next` option group sharing the run's cache also meets the
negative entry, as curl's shared DNS cache does.
