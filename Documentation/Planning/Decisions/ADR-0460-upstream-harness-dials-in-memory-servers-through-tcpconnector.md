# ADR-0460: The upstream harness dials its in-memory servers through TcpConnector

- Status: Accepted
- Date: 2026-10-09
- Task: BL-1915
- Decided by Claude under Stewart's delegation.

## Context

BL-1915 was filed on the belief that the in-process upstream case runner bypasses
`TcpConnector`, so that the code in `Curl.Networking.UnitLibrary` that sends `CONNECT`, reads the
tunnel's reply and writes the HAProxy PROXY line is never exercised by upstream cases, and asked
for a seam that keeps that code in the path and replaces only the final socket.

Reading the wiring showed the seam already exists. `UpstreamCaseRunner` hands curl an
`UpstreamCurlInvocation` whose `Connector` is the chain of in-memory stand-ins, and
`UpstreamConformanceTests.RunCurlAsync` builds curl with `CurlComposition.CreateRunner`, the
command's own composition, giving it an `InMemoryServerTcpDialer` (in `Curl.Conformance.UnitTests`)
as its `ITcpDialer` and `LoopbackOnlyDnsResolver` as its resolver. Every connect therefore runs
through `TcpConnector` - name resolution, `--connect-to`, the proxy tunnel (`HttpProxyTunnel`),
SOCKS (`SocksProxyTunnel`), the PROXY line (`HaproxyProtocolHeader`) and TLS - and only
`ITcpDialer.DialAsync`, the one step that opens a socket, is replaced: it calls the stand-in
`IConnector` for the dialled address and port.

At `curl-8_21_0` this already runs 22 CONNECT-tunnel cases that pass (206, 209, 213, 217, 265,
287, 718, 749, 750, 1008, 1021, 1060, 1061, 1297, 1715, 3028 and others with `-p`) and six
`--haproxy-protocol` / `--haproxy-clientip` cases that pass (1455, 1456, 3028, 3201, 3202, 3220).

## Decision

1. `ITcpDialer` is the seam between curl's connection code and the harness's in-memory servers.
   No new seam is added to `Curl.Networking.UnitLibrary`: an injectable socket factory beneath
   `ITcpDialer` would replace the same single step.
2. The adapter stays in `Curl.Conformance.UnitTests`, beside the composition that uses it, since
   `Curl.Conformance.UnitLibrary` does not reference `Curl.Networking.UnitLibrary` or
   `Curl.Console`, and adding either reference would only move it.
3. `InMemoryServerTcpDialerTests` pins the seam: curl composed as the command composes it sends
   its own `CONNECT host:port HTTP/1.1` to the `sws` stand-in's proxy port
   (`ProxyReceivedBytes`), its tunnelled request to `ReceivedBytes`, its PROXY line first on a
   direct connection, and, with both options, its PROXY line inside the opened tunnel, as
   upstream test 3028 verifies.

## Consequences

- A change to curl's tunnelling or PROXY-line code shows up in the upstream cases' verdicts,
  through the ratchet in `PassingUpstreamCases.txt`.
- The `sws` stand-in's `<connect>` handling only answers the `CONNECT` curl sends; it never
  writes one itself.
