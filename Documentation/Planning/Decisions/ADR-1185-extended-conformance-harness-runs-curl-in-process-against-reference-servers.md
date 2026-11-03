# ADR-1185: The extended conformance harness runs curl in process against reference servers routed by port

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4507, for BL-4462 and BL-4501)

## Context

The extended protocols (ADR-0999) have no upstream `tests/data` cases, so their conformance is tested against
each protocol's specification by running a real curl command line against a reference server. A unit test may
not open a socket or start a process (ADR-0421), the harness library may not reference `Curl.Console` (which
references it from its tests) or any extension (`VerifyExtensionIsolation`), and the runs must cover TCP, TLS
and UDP protocols alike.

## Decision

- **The curl entry point is a delegate.** `ExtendedConformanceHarness.RunAsync` takes an
  `InProcessCurlRunner`: arguments, standard output, standard error, standard input, an `ITcpDialer`, an
  `IDnsResolver`, an `IDatagramConnector` and an environment reader. The tests pass `Curl.Console`'s
  `InProcessCurl.RunAsync`, so the run goes through the real option parser, engine and handlers, while
  `Curl.Conformance.Extended.UnitLibrary` references neither the console nor any extension.
- **Routing is by port.** Each `ReferenceRoute` is one in-memory server on one port and transport (TCP, TLS
  or UDP). Every host name resolves to 127.0.0.1, so a command line names the server only by its port; a port
  no route answers fails to connect as a closed port does (exit 7).
- **Recording happens at the reference server.** Each connection or datagram channel yields one
  `WireExchange` with the bytes each side sent, captured where the server reads and writes. On a TLS route the
  server terminates TLS with `TlsServerStream` and the route's certificate, so the recorded bytes are the
  plaintext behind the handshake; nothing has to decrypt or parse a capture. A server that fails makes
  `RunAsync` throw its exception; one still waiting when curl ends is cancelled.
- `ScriptedReferenceServer` and `ScriptedDatagramServer` replay fixed bytes; each protocol's own reference
  server goes in its own folder, one file per server, registered nowhere.

Rejected: a real loopback listener and a `curl.exe` process (an Integration test, slower and not counted by
coverage); a capturing `IConnection` wrapper on the client side (TLS would hide the plaintext, and UDP would
need a second recorder); routing by host name (curl's own host handling, such as SNI and `Host`, would then
depend on the route table).

## Consequences

The harness is a unit library held to the 100% coverage gates, every run is deterministic and needs no
network, and a protocol's conformance tests add only a reference server and test methods.
