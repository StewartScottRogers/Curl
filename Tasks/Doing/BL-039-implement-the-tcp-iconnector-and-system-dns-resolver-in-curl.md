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
completed:
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

- [ ] `TcpConnector : IConnector`, `SystemDnsResolver : IDnsResolver` and a
      `NetworkStream`-backed `IConnection` exist in `Curl.Networking.UnitLibrary`, which
      references only `Curl.Protocol.Abstractions.UnitLibrary`.
- [ ] A test asserts a resolver returning no addresses gives
      `ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: nonexistent.invalid")`
      for host `nonexistent.invalid`, and the dialer is never called.
- [ ] A test asserts every address failing to dial gives `CurlExitCode.CouldntConnect`
      with `Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server`,
      where a fake `TimeProvider` advances 2013 ms during the dial.
- [ ] Tests assert addresses are tried in the resolver's order and the first success is
      returned; that `UseTls` true passes the connection and host to `ITlsProvider` and
      returns its result; that `UseTls` false never calls it; and that cancellation
      surfaces as `OperationCanceledException`, not as a failure result.
- [ ] `Curl.Networking.UnitLibrary/CLAUDE.md` states which types may construct a
      `Socket` and that everything else takes the Abstractions contracts.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green; the only `Integration` test is the loopback test described in `Context`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
