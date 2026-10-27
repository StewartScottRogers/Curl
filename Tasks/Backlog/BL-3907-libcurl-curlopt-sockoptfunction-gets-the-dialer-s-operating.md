---
id: BL-3907
title: libcurl: CURLOPT_SOCKOPTFUNCTION gets the dialer's operating system socket when no open-socket callback is set
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/SocketCallbackConnector.cs, Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Protocol.Abstractions.UnitLibrary/ITcpSocketOpener.cs, Curl.Engine.UnitTests/SockoptRealSocketTests.cs, Curl.Networking.UnitTests/TcpDialerSockoptTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3907 — libcurl: CURLOPT_SOCKOPTFUNCTION gets the dialer's operating system socket when no open-socket callback is set

## Goal

A libcurl handle that sets `CURLOPT_SOCKOPTFUNCTION` without `CURLOPT_OPENSOCKETFUNCTION` gets the operating system socket the dialer will connect, open and unconnected, as libcurl 8.22.0 does, so `getsockopt`/`setsockopt` on it work.

## Context

Left over from BL-3658 (CGR-129, LMC-15, ADR-0840 point 4). `SocketCallbackConnector.AttemptSocketOpener.Open` runs before `TcpDialer` creates its socket, so without an open-socket callback the socket-option callback gets a `CurlSocketHandles` number. Fix: let the dialer create the socket first and pass its handle to `ITcpSocketOpener` (for example an `Open(remoteEndPoint, nint? dialerSocket)` overload, default delegating to the old one), and have the opener hand it to the socket-option callback and keep it for the close-socket callback (which BL-3658 already gives the real handle after connect). Measure real libcurl first.

## Acceptance criteria

- [ ] With only `CURLOPT_SOCKOPTFUNCTION` (and optionally `CURLOPT_CLOSESOCKETFUNCTION`) set, the socket-option callback's socket answers `getsockopt(SO_TYPE)` as `SOCK_STREAM`, and the close-socket callback gets the same handle; a unit test in `Curl.Engine.UnitTests` over a loopback listener pins it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
