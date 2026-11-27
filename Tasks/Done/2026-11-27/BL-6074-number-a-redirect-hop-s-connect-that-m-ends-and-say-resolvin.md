---
id: BL-6074
title: Number a redirect hop's connect that -m ends, and say Resolving timed out when -m ends its resolve
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6071]
touches: [Curl.Networking.UnitLibrary/TcpConnector.ConnectTimeout.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpTransferDeadline.cs, Curl.Original.Protocol.Http.UnitTests/HttpConnectTimedOutLineTests.cs, Curl.Networking.UnitTests/TcpConnectorTests.ConnectTimeout.cs, Curl.Networking.UnitLibrary/TcpConnector.NameResolution.cs, Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Abstractions.UnitLibrary/ConnectCancellationKeys.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-6074 — Number a redirect hop's connect that -m ends, and say Resolving timed out when -m ends its resolve

## Goal

`-m` ending a redirect hop's connect ends `-v` with `closing connection #1`, and ending its name resolve says `Resolving timed out after N milliseconds`, as curl does.

## Context

Found in BL-6071 (ADR-1460). `curl -svSL -m 3` following a 302, answered after 1.5 s, to `http://192.0.2.20:1/` prints `* closing connection #0` in Curl and `#1` in curl 8.22.0 and 8.23.0 (measured with `Record-CurlExchange.ps1`). The connector's limit is the whole `-m`, so `TcpConnector` lets the cancellation escape and gives the connect no number, and `HttpTransferDeadline.TimedOut` returns a result with `ConnectionNumber` 0. The same path always says `Connection timed out`, also when the cancellation arrived during the resolve (`CanceledWhileResolvingException`); measure real curl for that message first. `ConnectTimings.CarriedOnCancellationKey` is how the connector already hands facts to the handler on a cancellation. Widen `touches` if the pooling connector numbers it.

## Acceptance criteria

- [x] A unit test pins that a connect the caller's token cancels before the connector's limit carries the connection number to the handler's `closing connection #N` line.
- [x] A unit test pins the message of a connect the caller's token cancels while resolving, matching the measured curl text.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan (ADR-1462): `TcpConnector` marks a cancellation that arrived during a resolve, `PoolingConnector` offers a
  connection-number taker on a cancelled TCP connect (both under the new `ConnectCancellationKeys`), and
  `HttpTransferDeadline` uses them for the message and `closing connection #N`, taking no number for an abandoned connect.
- `touches` widened with the four files named above; the only other task in Doing (BL-6073) names none of them.
- Measured, curl 8.22.0 on Windows: `-svSL -m 2 --doh-url http://127.0.0.1:18474/dns` after a 302 answered 1.5 s late, to
  `http://slow.example.test:1/`: `Resolving timed out after 426 milliseconds`, `closing connection #1`, exit 28.
  The built `curl.exe` prints the same lines (DoH over TLS, `-k --doh-insecure`, N=753 at `-m 2.5`) and `#1` for the
  connect case to `http://192.0.2.20:1/`.
- Quality: the all-test run for the changed libraries did not finish in 50 minutes and was stopped. Scored instead from
  `Curl.Networking.UnitTests` and `Curl.Original.Protocol.Http.UnitTests` alone: no changed member fails; the 10 members
  reported are untouched ones that other test projects cover. `ConnectCancellationKeys` holds only constants.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: bff96a23 c6835b0c 3d836d5e.
- 2026-11-27: Doing -> Done. a redirect hop's connect that -m ends closes #1, and one ended while resolving says Resolving timed out
