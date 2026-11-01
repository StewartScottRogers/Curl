# ADR-0611 — The prerequest and resolver-start callbacks run at the engine's connect and look-up seams

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2419.

## Context

`Curl.Api`'s `EasyCallbackOptions` carries `CURLOPT_PREREQFUNCTION` (`PrerequestCallback`),
`CURLOPT_RESOLVER_START_FUNCTION` (`ResolverStartCallback`) and `CURLOPT_TRAILERFUNCTION` (`TrailerCallback`).
Nothing called them, and `Libcurl` answered `CURLE_NOT_BUILT_IN` for all six options.

In curl 8.22.0 the multi state machine calls the prerequest callback as each request enters its `DO` state, once
its connection (new or reused) is ready and before anything is sent, with the peer's and the local IP address and
port; any answer but `CURL_PREREQFUNC_OK` fails the transfer with `CURLE_ABORTED_BY_CALLBACK` and
`operation aborted by pre-request callback`. `Curl_resolv` calls the resolver-start callback before a name
resolution its DNS cache did not answer; a non-zero answer resolves nothing, so the transfer fails with
`CURLE_ABORTED_BY_CALLBACK` (upstream's test655 checks that code) under the plain `Could not resolve host` message, and the failure is not cached. The trailer
callback fills the trailers of a chunked upload's last chunk.

The managed engine's handlers ask an `IConnector` for a connection once per request, a followed redirect again,
and `Curl.Networking`'s `TcpConnector` asks its `IDnsResolver` only when its DNS cache (with the `--resolve`
entries) misses. The HTTP handler has no way to send request trailers.

## Decision

1. **Prerequest at the connector.** `PrerequestConnector` wraps the pool every TCP handler connects through (not
   FTP's data connector, which is no request) and the connect-only connector, when the callback is set. After each
   connect that gives a connection it calls the callback with the connection's `RemoteEndPoint` and
   `LocalEndPoint` (an empty address and port 0 for an end point that is not an IP one); `Abort` closes the
   connection and fails the connect with exit 42 and upstream's text. A QUIC session the handler opens through
   `ConnectMultiplexedSessionAsync` is asked too; a bare `ConnectMultiplexedAsync` passes through.
2. **Resolver start beneath the DNS cache.** `EngineComposition.CreateTransports` takes a `wrapLookUps` function
   that wraps the resolver the TCP and UDP connectors ask (the overload without it wraps nothing, so `Curl.Console`
   is unchanged), and the managed engine wraps it in `ResolverStartDnsResolver` when the callback is set. The
   callback is given 0 for the resolver's state (no c-ares channel to hand out); a non-zero answer gives no address
   and the failure reason `DnsLookupFailure.AbortedByCallback`, so `TcpConnector` fails with exit 42 and the plain `Could not resolve host: <h>`
   (`proxy` for a proxy) and caches nothing (BL-2627 amendment: this said exit 6, which test655 refutes). A resolver's failure reason passes through.
3. **Trailers are split off.** Sending request trailers needs a seam in `Curl.Protocol.Abstractions` and work in
   `Curl.Original.Protocol.Http`, outside this task; BL-2475 does it and adds the `TRAILERFUNCTION` rows. Until then
   `TRAILERFUNCTION` and `TRAILERDATA` keep answering `CURLE_NOT_BUILT_IN`, so no option is accepted and ignored.
4. `Libcurl/Callbacks/Phases` sets `PREREQFUNCTION` and `RESOLVER_START_FUNCTION` (their delegate or `null`, else
   `CURLE_BAD_FUNCTION_ARGUMENT`); `PREREQDATA` and `RESOLVER_START_DATA` take any value and change nothing, the
   delegate's closure being its user data.

## Consequences

- The callbacks run at the same points as libcurl's for every TCP and UDP scheme, with no change to a transfer
  that sets neither.
- A `file` URL never connects, so the prerequest callback is not called for it, where libcurl calls it with empty
  addresses; and with the resolver wrapped, `TcpConnector`'s checks for a `DohDnsResolver` (one fewer `-v` line on
  failure) and a `DnsServerResolver` (negative caching) no longer see through the wrapper. Each is a corner of a
  corner; a task is filed when a test or application needs one.
- `Curl.Engine.UnitTests` pins each call point and abort through `PrerequestConnectorTests`,
  `ResolverStartDnsResolverTests` and `ManagedEasyTransferTests.Phases.cs`.
