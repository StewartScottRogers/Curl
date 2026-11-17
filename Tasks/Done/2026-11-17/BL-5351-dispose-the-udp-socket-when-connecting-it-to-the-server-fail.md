---
id: BL-5351
title: Dispose the UDP socket when connecting it to the server fails in UdpDatagramChannel
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/UdpDatagramChannel.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5351 — Dispose the UDP socket when connecting it to the server fails in UdpDatagramChannel

## Goal

A UDP channel whose connect to the server fails disposes its socket, whichever constructor opened it.

## Context

Found by the shift-end review of 2026-11-16 (code-reviewer, confirmed by hand) in the BL-5315 change.

`Curl.Networking.UnitLibrary/UdpDatagramChannel.cs:114-121`: the `UdpDatagramChannel(IPEndPoint serverEndPoint, Socket boundSocket, bool connectsToServer)` constructor takes ownership of `boundSocket`, then calls `ConnectIf(connectsToServer)` with no try/catch.

`UdpChannelOpener.OpenFromDeviceAsync` (`UdpChannelOpener.cs:~103`) passes `_connectsToServer`, which is true on Linux and macOS. It disposes the socket only on failures that happen before the `return`.

If `_socket.Connect` throws (`ENETUNREACH`, or `EADDRNOTAVAIL` after an `--interface` bind), nothing owns the socket and it leaks. That matters for long-lived `Curl.Api` and `Libcurl` hosts.

The other constructor (lines 86-101) does this correctly: its catch calls `_socket.Dispose()` and rethrows.

## Acceptance criteria

- [x] The `(serverEndPoint, boundSocket, connectsToServer)` constructor disposes `boundSocket` and rethrows when `ConnectIf` throws.
- [x] A unit test in `Curl.Networking.UnitTests` makes the connect fail, without network traffic, and asserts that the socket is disposed and the exception propagates.
- [x] `Curl.Networking.UnitLibrary` still meets the four quality gates.

## Notes

The bound-socket constructor now disposes the socket and rethrows when ConnectIf throws. Test: an IPv4 socket given an IPv6 server endpoint makes Connect fail with no traffic. Measure-CodeQuality on Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 65b665a8 75fb10e4 5669b010.
- 2026-11-17: Doing -> Done. Socket disposed on connect failure; test added
