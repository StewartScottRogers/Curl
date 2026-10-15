# ADR-0492: CURL_DNS_SERVER is honoured only behind the harness's seam

- Status: Accepted
- Date: 2026-10-14
- Task: BL-2248
- Decided by Claude under Stewart's delegation

## Context

Upstream cases 2102, 2103 and 2104 set `CURL_DNS_SERVER=127.0.0.1:%DNSPORT` and need the
`override-dns` feature: curl 8.22.0's debug build, with c-ares, asks that DNS server in place of the
system's. A release build ignores the variable. The conformance harness answers DNS with an in-memory
stand-in (BL-2232) that it reaches through the run's injected `IDatagramConnector`, not a socket.

## Decision

The dialing `CurlComposition.CreateRunner` takes `honoursDnsServerVariable` (off by default). When
it is on and the run's environment reader gives a non-empty `CURL_DNS_SERVER`, every option group
resolves through `CurlComposition.DnsServerVariableResolverOf`: a `DnsServerResolver` with the value
as its server list (parsed as `--dns-servers` is), asking the `-4` or `-6` family's records (AAAA
and A otherwise), whose UDP queries go through the run's `IDatagramConnector` by way of
`DatagramConnectorDnsSocketOpener`. That resolver replaces the one the runner was given. A name the
server does not answer is exit 6 with `Could not resolve host: <name>` and c-ares' reason, as the
c-ares build reports it. A query retried over TCP is refused, as the connector carries datagrams
only.

The shipped executable never turns the seam on, so `curl` stays byte-identical to curl's release
build, which ignores the variable; only the harness does, as it alone gets the httpsig cases'
fixed clock (BL-2183).

## Consequences

- The harness reaches its DNS stand-in with no socket and no change to its `IDnsResolver`.
- `--dns-servers` and the debug variable share one client, so they report failures alike.
- A stand-in that truncates a reply gets an unreachable-server failure rather than a TCP retry.
