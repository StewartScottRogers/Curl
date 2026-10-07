---
id: BL-360
title: Report 'with proxy' when a forward-proxy connection is reused
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-215, BL-336]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions/ADR-0050-connections-are-reused-across-requests-and-urls-through-a-pooling-connector.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-360 — Report 'with proxy' when a forward-proxy connection is reused

## Goal

When a connection to a forward (non-tunnelled) HTTP proxy is reused, `-v` prints `* Reusing existing http: connection with proxy <proxy host>`, as curl 8.21.0 does for `-x http://127.0.0.1:18231 http://a.example/x http://b.example/y` (ADR-0050 measurement table).

## Context

- ADR-0050 (`Documentation/Planning/Decisions/ADR-0050-connections-are-reused-across-requests-and-urls-through-a-pooling-connector.md`), sections "The pool key" and "`%{num_connects}` and `-v`". Filed from BL-215.
- Today `PoolingConnector` (`Curl.Networking.UnitLibrary`, added by BL-215) always reports `ConnectionReusedEvent.IsProxy = false`. For a forward proxy, `HttpProtocolHandler.TargetOf` (`Curl.Protocol.Http.UnitLibrary`) connects with the proxy as `ConnectTarget.Host`/`Port` and leaves `ConnectTarget.Proxy` unset, so the pool cannot tell that the host is a proxy.
- The work:
  1. Give `ConnectTarget` (`Curl.Protocol.Abstractions.UnitLibrary`) a way to say the host is a forward proxy, for example an `init` property `IsForwardProxy` (default `false`). Record the choice in an amendment to ADR-0050 (or a short ADR note), marked "Decided by Claude under Stewart's delegation".
  2. Have `HttpProtocolHandler.TargetOf` set it on the forward-proxy target.
  3. Have `PoolingConnector` report `ConnectionReusedEvent.IsProxy` from it.
- A tunnelled connection (`ConnectTarget.Proxy` set, `-p`/HTTPS through a proxy) should keep `IsProxy = false` and report the origin host name, which is what the pool does today. Before pinning that, measure curl 8.21.0 with `-v -x http://<proxy> -p https://<a>/x https://<b>/y` (two URLs on the same origin, and on different origins) and record the observed reuse line in the ADR-0050 measurement table. If the measurement disagrees, pin what curl prints.
- BL-336 (hand reusable HTTP connections back and report reuse) must be done first, since it is where the handler starts using the pool's reuse reporting.

## Acceptance criteria

- [x] `ConnectTarget` exposes a forward-proxy marker, defaulting to `false`, with a test in `Curl.Protocol.Abstractions.UnitTests` pinning the default.
- [x] Tests in `Curl.Networking.UnitTests` pin `ConnectionReusedEvent.IsProxy` `true` on reuse of a forward-proxy target, and `false` on reuse of a direct target and of a tunnelled target (`ConnectTarget.Proxy` set).
- [x] A test in `Curl.Protocol.Http.UnitTests` pins that `HttpProtocolHandler` marks the target as a forward proxy when `-x` names an HTTP proxy without `-p` for an `http://` URL, and does not mark it for a direct request or a tunnelled one.
- [x] ADR-0050 records the marker's design and the curl 8.21.0 measurement for the tunnelled twice-URL case.
- [x] `dotnet build -warnaserror` is clean for the changed projects; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Networking.UnitLibrary` and `Curl.Protocol.Http.UnitLibrary`.

## Notes

- 2026-09-27, lane 3 (first attempt). **Measured** curl 8.21.0 with `Record-CurlExchange.ps1`,
  extended with `-KeepAlive` (serve request after request on one connection) and
  `-TunnelConnect` (answer CONNECT with `200 Connection established`):
  `-v -x http://127.0.0.1:18231 -p http://a.example/x http://a.example/y` prints
  `* Reusing existing http: connection with proxy 127.0.0.1` - a tunnelled reuse names the
  **proxy**, not the origin, so the Context's expectation is wrong and the measurement is
  pinned instead (IsProxy `true`, HostName and Port the proxy's). Different origins through
  the tunnel: no reuse, `Connection #1`. Forward case re-measured: `with proxy 127.0.0.1`.
- Implementation done and green in the three task projects in that attempt (uncommitted,
  stashed by the shift): `ConnectTarget.IsForwardProxy` (`init`, default `false`) with two
  tests in `ConnectTargetTests`; it joins `ConnectionPoolKey` (so a forward proxy connection
  never serves a direct request to the proxy's host), with a `"forward proxy"` DataRow in
  `ConnectAsync_ForADifferentKey_OpensANewConnection`; `PoolingConnector.Reuse` reports
  `IsProxy = target.IsForwardProxy || target.Proxy is not null`,
  `HostName = target.Proxy?.Host ?? target.Host`, `Port = target.Proxy?.Port ?? target.Port`,
  with two new `PoolingConnectorTests`; `HttpProtocolHandler.TargetOf` sets
  `IsForwardProxy = true` on the forward-proxy target, and the four forward-proxy
  expectations in `HttpProtocolHandlerTests.Proxy.cs` gained `IsForwardProxy = true`
  (direct `HttpProtocolHandlerTests.cs:77` and tunnelled `Proxy.cs:266` pin its absence).
  ADR-0050 got two measurement rows, a key row and an amendment.
- **Why it went back to Backlog:** the full fast run then failed 7 tests in
  `Curl.Console.UnitTests` (`CurlCompositionProxyTests`: the `ForwardProxy` field at line 38
  and the targets at lines 271 and 380 need `IsForwardProxy = true`), which compare forward-proxy
  `ConnectTarget`s by record equality. `Curl.Console.UnitTests` was added to `touches`, but
  BL-244 (in Doing) touches it, so per the lane rules this task waits until BL-244 is done.
  Also added to `touches`: `Record-CurlExchange.ps1` (the measurement needed keep-alive and
  CONNECT) and ADR-0050 (acceptance criterion); no Doing task named either.
- 2026-09-27, lane 2 (second attempt, after BL-244 was done). The first attempt's stashed
  work was not reachable from this lane, so the same design was reimplemented from the notes
  above. The `Record-CurlExchange.ps1` extension was not redone: the measurement above is
  already recorded, and the ADR says the extension was not kept. Also fixed
  `CurlCompositionProxyTests.RunAsync_NoProxyOptionNamingTheHost_ConnectsDirectly`, which
  compared a direct target against the `ForwardProxy` constant only because they used to be
  equal; it now pins the direct target. Direct and tunnelled targets in
  `HttpProtocolHandlerTests.cs:77` and `HttpProtocolHandlerTests.Proxy.cs:266` pin the
  marker's absence by record equality. `Measure-CodeQuality.ps1`: 100/100 line and branch
  for all three libraries; its one flagged member, `SslStreamTlsProvider.VerifyPeer`
  (complexity 12), was there already and is untouched.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Needs Curl.Console.UnitTests (7 forward-proxy ConnectTarget expectations), which BL-244 in Doing touches; resume once BL-244 is done (see Notes)
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A reused forward-proxy or tunnelled connection is reported 'with proxy <proxy host>' under -v, as curl 8.21.0 prints it
