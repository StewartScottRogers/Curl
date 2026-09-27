---
id: BL-358
title: Report 'with proxy' when a forward-proxy connection is reused
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-215, BL-336]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-358 — Report 'with proxy' when a forward-proxy connection is reused

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

- [ ] `ConnectTarget` exposes a forward-proxy marker, defaulting to `false`, with a test in `Curl.Protocol.Abstractions.UnitTests` pinning the default.
- [ ] Tests in `Curl.Networking.UnitTests` pin `ConnectionReusedEvent.IsProxy` `true` on reuse of a forward-proxy target, and `false` on reuse of a direct target and of a tunnelled target (`ConnectTarget.Proxy` set).
- [ ] A test in `Curl.Protocol.Http.UnitTests` pins that `HttpProtocolHandler` marks the target as a forward proxy when `-x` names an HTTP proxy without `-p` for an `http://` URL, and does not mark it for a direct request or a tunnelled one.
- [ ] ADR-0050 records the marker's design and the curl 8.21.0 measurement for the tunnelled twice-URL case.
- [ ] `dotnet build -warnaserror` is clean for the changed projects; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Networking.UnitLibrary` and `Curl.Protocol.Http.UnitLibrary`.

## Notes

## Log

- 2026-09-27: Created.
