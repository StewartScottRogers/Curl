---
id: BL-3436
title: Close the client side when the target ends in the differential ProxyTunnel relay
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/Protocols/Proxy/ProxyTunnel.cs, Curl.Conformance.Differential.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3436 — Close the client side when the target ends in the differential ProxyTunnel relay

## Goal

`ProxyTunnel.RelayAsync` ends as soon as either side ends, as its doc comment says. When the target closes, the client sees end-of-stream at once.

## Context

Found by the shift-end review of 2026-10-23 (ADR-0710). The code reviewer for the conformance harness found it in the diff `79ec91e50...7f4970f66`.

The problem is in `Curl.Conformance.Differential.UnitLibrary/Protocols/Proxy/ProxyTunnel.cs:22-26`:

- After `Task.WhenAny(toRemote, toClient)`, only `remote` is disposed. `connection` is never closed.
- When the target ends first, `toRemote` stays blocked reading from the client. `Task.WhenAll` then waits until the client closes.
- A client that reads the tunnelled response to end-of-stream (an HTTP/1.0 response with no `Content-Length`, or `Connection: close`) never gets a FIN. Client and responder sit until timeout.
- The differential run then reports a hang for both curl and Curl. That looks like a behaviour difference, but the responder caused it.
- SOCKS4, SOCKS4a, SOCKS5 and HTTP CONNECT all relay through this method.
- proxy_server.py's `tunnel`, which this ports, ends the relay when either side ends.

## Acceptance criteria

- [ ] When the target side ends first, `RelayAsync` closes the client connection (shutdown or dispose) and returns.
- [ ] When the client side ends first, `RelayAsync` closes the target and returns, as it does today.
- [ ] Unit tests in `Curl.Conformance.Differential.UnitTests` cover both directions over in-memory streams or a loopback pair. Each shows `RelayAsync` completes and the other side reads end-of-stream, with no network beyond loopback.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: ca289d49 908f0847 a3bf4675 3d836d5e.
