---
id: BL-1412
title: Ignore a 416's body and leave the connection intact after -C, and keep a real 304 under -z intact, as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-080
created: 2026-10-03
completed:
---
# BL-1412 — Ignore a 416's body and leave the connection intact after -C, and keep a real 304 under -z intact, as curl 8.21.0 does

## Goal

`curl -sv -C 5` against a `416` writes `* setting size while ignoring` before the empty `< ` line and ends `* Connection #0 to host ... left intact`, and a real `304` under `-z` ends `left intact` too, instead of today's `shutting down connection #0` for both.

## Context

- Found by BL-1398 (2026-10-03). `HttpProtocolHandler.DeliveredWhole` counts any delivery but `HttpBodyDelivery.Deliver` as not whole, so `KeepsAlive` is false and `ConnectionEndLine` reports `shutting down connection #0` for a 416 resume and for a real 304 under `-z`. `ReadBodyAsync` reads none of the 416's body, so a 416 with a non-zero Content-Length would leave bytes on the connection: the body must be read and discarded (as `ReportIgnoredBody`'s discarded bodies are) before the connection can be pooled.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1` (BL-1398 Context): `-sv -C 5` against `HTTP/1.1 416 ...\r\nContent-Length: 0\r\n\r\n` writes `* setting size while ignoring`, `< `, `* Connection #0 ... left intact`. Today Curl writes neither the size line nor `left intact`.
- The real 304 under `-z` has not been measured yet: measure it before pinning, and measure a 416 with `Content-Length: 4` and a body.
- BL-1398's simulated cases (`The entire document is already downloaded`, `Simulate an HTTP 304 response`) must keep `shutting down connection #0`: curl `streamclose`s them.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` pins the measured 416 `-v` sequence, including `setting size while ignoring` and `left intact`, and that the connection is reused by a second request.
- [ ] A test pins the measured connection end for a real 304 under `-z`.
- [ ] BL-1398's tests in `HttpProtocolHandlerTests.UndeliveredBodyInfoLines.cs` still pass unchanged.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member in the changed code.

## Notes

## Log

- 2026-10-03: Created.
