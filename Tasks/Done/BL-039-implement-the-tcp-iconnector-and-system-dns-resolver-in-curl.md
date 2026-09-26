---
id: BL-039
title: Implement the TCP IConnector and system DNS resolver in Curl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-034]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-039 — Implement the TCP IConnector and system DNS resolver in Curl.Networking

## Goal

`Curl.Networking.UnitLibrary` has the production `IConnector` for TCP - resolving through
`IDnsResolver`, connecting, handing secure targets to `ITlsProvider` - plus a
`System.Net.Dns`-backed `IDnsResolver`, returning exit 6 and exit 7 with curl 8.21.0's
messages.

## Context

The contract is ADR-0005
(`Documentation/Planning/Decisions/ADR-0005-protocol-handlers-acquire-transports-through-connectors.md`)
as implemented by BL-034. `Curl.Networking.UnitLibrary` is empty today.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24), exit codes
per <https://curl.se/libcurl/c/libcurl-errors.html>:

- `curl dict://nonexistent.invalid/d:x` - exit 6 (`CURLE_COULDNT_RESOLVE_HOST`),
  message `Could not resolve host: nonexistent.invalid`.
- `curl dict://127.0.0.1:1/d:x` - exit 7 (`CURLE_COULDNT_CONNECT`), message
  `Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server`. The
  milliseconds are the elapsed connect time; measure them with the injected
  `TimeProvider`, never the wall clock.

Testability: the connector takes `IDnsResolver`, `ITlsProvider`, `TimeProvider` and an
internal dialing seam (for example an `ITcpDialer` returning an `IConnection` for one
address), so the fast tests drive every branch without a socket. Only the dialer and the
`NetworkStream`-backed `IConnection` touch a real socket; their one loopback test is
tagged `[TestCategory("Integration")]`, as ADR-0002 allows for Core's disk test.

When `ConnectTarget.UseTls` is true the plaintext connection goes to
`ITlsProvider.AuthenticateAsClientAsync` with the target host. The production
`ITlsProvider` and the exit codes for TLS failures (35, 60) are not part of this task.

`Curl.Networking.UnitLibrary/CLAUDE.md` carries the protocol boilerplate "Never construct
a `Socket`, `SslStream` or `HttpClient` here", which is wrong for the one project whose
job is to construct them; this task corrects it.

## Acceptance criteria

- [x] `TcpConnector : IConnector`, `SystemDnsResolver : IDnsResolver` and a
      `NetworkStream`-backed `IConnection` exist in `Curl.Networking.UnitLibrary`, which
      references only `Curl.Protocol.Abstractions.UnitLibrary`.
- [x] A test asserts a resolver returning no addresses gives
      `ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: nonexistent.invalid")`
      for host `nonexistent.invalid`, and the dialer is never called.
- [x] A test asserts every address failing to dial gives `CurlExitCode.CouldntConnect`
      with `Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server`,
      where a fake `TimeProvider` advances 2013 ms during the dial.
- [x] Tests assert addresses are tried in the resolver's order and the first success is
      returned; that `UseTls` true passes the connection and host to `ITlsProvider` and
      returns its result; that `UseTls` false never calls it; and that cancellation
      surfaces as `OperationCanceledException`, not as a failure result.
- [x] `Curl.Networking.UnitLibrary/CLAUDE.md` states which types may construct a
      `Socket` and that everything else takes the Abstractions contracts.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green; the only `Integration` test is the loopback test described in `Context`.

## Notes

- Plan: `TcpConnector(IDnsResolver, ITcpDialer, ITlsProvider, TimeProvider)` resolves,
  returns exit 6 on an empty list without dialing, dials each address in order (a
  `SocketException` moves to the next), returns exit 7 with the `TimeProvider`-measured
  milliseconds when all fail, then hands the connection and host to `ITlsProvider` only
  when `UseTls` is set. `OperationCanceledException` is never caught.
- Choice: `ITcpDialer` is public, not internal, so `Curl.Console` can register
  `TcpDialer` with dependency injection and `TcpConnector` keeps one public constructor.
- Choice: the `IConnection` is `StreamConnection` over any `Stream`; `TcpDialer` gives it
  the `NetworkStream` that owns the socket. Taking `Stream` lets every member be covered
  by fast tests over a `MemoryStream`, leaving only `TcpDialer`'s socket body to the one
  `Integration` loopback test (which also checks a dial to the stopped listener throws
  `SocketException`).
- Choice: `SystemDnsResolver` maps the system resolver's `SocketException` to an empty
  list (so the connector reports exit 6); an internal constructor takes the lookup
  delegate so that path is tested without a network (`InternalsVisibleTo` the tests).
- Choice: exit 7 prints the host as given in the target (`<host>:<port>`), matching the
  measured `127.0.0.1:1` case.
- Known gap, by the task's scope: TLS handshake exceptions from `ITlsProvider` propagate
  unchanged; mapping them to exits 35/60 belongs with the production `ITlsProvider`.
- Tests: 19 in `Curl.Networking.UnitTests` (18 fast, 1 Integration), all green.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. TcpConnector resolves, dials in order and hands TLS targets to ITlsProvider, returning curl 8.21.0's exit 6 and 7 messages; SystemDnsResolver and the socket-backed dialer exist
