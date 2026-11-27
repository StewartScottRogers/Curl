---
id: BL-6074
title: Number a redirect hop's connect that -m ends, and say Resolving timed out when -m ends its resolve
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6071]
touches: [Curl.Networking.UnitLibrary/TcpConnector.ConnectTimeout.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpTransferDeadline.cs, Curl.Original.Protocol.Http.UnitTests/HttpConnectTimedOutLineTests.cs, Curl.Networking.UnitTests/TcpConnectorTests.ConnectTimeout.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6074 — Number a redirect hop's connect that -m ends, and say Resolving timed out when -m ends its resolve

## Goal

`-m` ending a redirect hop's connect ends `-v` with `closing connection #1`, and ending its name resolve says `Resolving timed out after N milliseconds`, as curl does.

## Context

Found in BL-6071 (ADR-1459). `curl -svSL -m 3` following a 302, answered after 1.5 s, to `http://192.0.2.20:1/` prints `* closing connection #0` in Curl and `#1` in curl 8.22.0 and 8.23.0 (measured with `Record-CurlExchange.ps1`). The connector's limit is the whole `-m`, so `TcpConnector` lets the cancellation escape and gives the connect no number, and `HttpTransferDeadline.TimedOut` returns a result with `ConnectionNumber` 0. The same path always says `Connection timed out`, also when the cancellation arrived during the resolve (`CanceledWhileResolvingException`); measure real curl for that message first. `ConnectTimings.CarriedOnCancellationKey` is how the connector already hands facts to the handler on a cancellation. Widen `touches` if the pooling connector numbers it.

## Acceptance criteria

- [ ] A unit test pins that a connect the caller's token cancels before the connector's limit carries the connection number to the handler's `closing connection #N` line.
- [ ] A unit test pins the message of a connect the caller's token cancels while resolving, matching the measured curl text.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
