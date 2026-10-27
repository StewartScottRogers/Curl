# ADR-0857: A DoH name that cannot be put in a query exits 27 "Failed to encode DoH packet"

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3687)
- Relates to: ADR-0152, CGR-158 of the 2026-10-25 curl gap report in `Documentation/Reviews`

## Context

`DnsQueryEncoder` refuses a name with an empty label or a label over 63 bytes, and a query over 272 bytes,
as curl's `doh_req_encode` does. `DohDnsResolver` answered such a name with no address, so the transfer
failed with exit 6 `Could not resolve host: <host>`. Measured 2026-10-26 with curl 8.22.0 Schannel and
nothing listening on the DoH port: `curl -sS --doh-url http://127.0.0.1:21800/dns-query http://<64 a's>.test:21802/x`
and `http://foo..test:21802/x` exit 27 with `curl: (27) Failed to encode DoH packet [1]`, and a 310-byte name
with `[13]`, sending nothing; `https://` origins give the same. The numbers are curl's `DOHcode`
`DOH_DNS_BAD_LABEL` (1) and `DOH_DNS_NAME_TOO_LONG` (13).

## Decision

`DohDnsResolver` implements `IDnsResolverWithFailureReason` and reports such a name, before any query is sent,
as `DnsLookupFailure.DohBadLabel` or `DnsLookupFailure.DohNameTooLong`. `NameResolutionFailure` turns them into
exit 27 (`CurlExitCode.OutOfMemory`, as curl reuses `CURLE_OUT_OF_MEMORY`) with curl's message, in place of
the `Could not resolve` one. The failure travels the existing failure-reason path, so `TcpConnector`,
`UdpDatagramConnector` and the wrapping resolvers need no new code. A direct connect's `https://` origin asks
the resolver through `IHttpsOriginDnsResolver`, which carries no reason; BL-3915 carries it there.

## Consequences

- `http://` origins under `--doh-url` match curl for these names; `https://` ones still exit 6 until BL-3915.
- No new exception type: a resolver failure stays a `DnsResolution`.
