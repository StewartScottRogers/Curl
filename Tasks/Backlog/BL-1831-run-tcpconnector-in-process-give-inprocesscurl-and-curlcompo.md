---
id: BL-1831
title: Run TcpConnector in process: give InProcessCurl and CurlComposition.CreateRunner a TCP-dialer and name-resolver seam instead of a finished IConnector
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1831 — Run TcpConnector in process: give InProcessCurl and CurlComposition.CreateRunner a TCP-dialer and name-resolver seam instead of a finished IConnector

## Goal

`InProcessCurl.RunAsync` runs the production `Curl.Networking.UnitLibrary` `TcpConnector`, built by `CurlComposition.CreateTcpConnector` from the run's options exactly as the executable builds it, with only the TCP dial and the name resolver injected, so a CONNECT tunnel, a HAProxy PROXY line, the `.onion` refusal (exit 6), an unresolvable name (exit 6) and a bad `--resolve`/`--connect-to` entry (exit 49) reach an in-process server as they reach a real one.

## Context

- Split out of BL-1794 (GF-0001), which was too large for one lane run; BL-1794 depends on this task and then switches `UpstreamConformanceTests.RunCurlAsync` to the new path and checks the 28 upstream cases.
- Today `Curl.Console/InProcessCurl.cs` passes the caller's `IConnector` straight to `CurlComposition.CreateRunner`, so the test server's connector receives every `ConnectTarget`, proxy included, and connects directly.
- `CurlComposition.CreateTransports` (around line 690) builds `TcpDialer`, `CreateDnsResolver`, `CreateProxyTunnelOptions`, `CreateTcpConnector` and `CreatePoolingConnector`; the seam is to let a caller replace the `TcpDialer` and `IDnsResolver` (e.g. a dial that maps every endpoint to the in-process server's stream) and keep the rest.
- Keep the existing `RunAsync(…, IConnector, IDatagramConnector)` overload working for callers that need it, or migrate them, whichever leaves fewer paths.

## Acceptance criteria

- [ ] `InProcessCurl` has an overload taking a TCP-dial seam and a name-resolver seam, and through it `TcpConnector` (proxy tunnel, HAProxy header, `.onion` refusal, `--resolve`/`--connect-to`) runs in process.
- [ ] Unit tests in `Curl.Console.UnitTests` show, through that overload: `-p -x` sends `CONNECT host:port HTTP/1.1`; `--haproxy-protocol` sends a `PROXY` line first; `http://x.onion/` exits 6 with `curl: (6) Not resolving .onion address (RFC 7686)`; an unresolvable name exits 6; a malformed `--resolve` exits 49.
- [ ] `Curl.Console` stays at 100% line and branch coverage, complexity at most 10.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-08: Created.
