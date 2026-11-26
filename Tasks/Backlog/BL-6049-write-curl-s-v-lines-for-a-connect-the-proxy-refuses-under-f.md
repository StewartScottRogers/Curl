---
id: BL-6049
title: Write curl's -v lines for a CONNECT the proxy refuses under -f
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6024]
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpConnectorProxyAuthProblemTests.cs, Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.Connecting.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerProxyConnectFailTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6049 — Write curl's -v lines for a CONNECT the proxy refuses under -f

## Goal

`curl -v -f` through a proxy that refuses the CONNECT writes the same `*` lines after the reply head as real curl.

## Context

Found while measuring BL-6024 (its Notes hold the cases). Reference curl 8.22.0 with `-sS -v -f` writes
`* The requested URL returned error: N` after a refused CONNECT's reply head, or no such line when it wrote
`Basic authentication problem, ignoring.`, and never `* CONNECT tunnel failed, response N`.
`TcpConnector.TunnelFailure` reports `CONNECT tunnel failed, response N` whatever the fail mode (read from the
code, not run). The connector is not told the fail mode, so the likely seam is a flag on `ConnectTarget`.

## Acceptance criteria

- [ ] For a 403 and for a 407 with no challenge under `-v -f`, the `*` lines after the reply head match reference curl, measured with `Record-CurlExchange.ps1`.
- [ ] For a 407 with `Proxy-Authenticate: Basic` to `--proxy-user` under `-v -f`, no `CONNECT tunnel failed` line is written.
- [ ] Without `-f`, `-v` still writes `CONNECT tunnel failed, response N`.
- [ ] A focused unit test in each changed library's own `.UnitTests` project pins the behaviour, and `dotnet build` is clean and the fast tests pass.

## Notes

`touches` includes the `ConnectTarget` contract file; widen it with that contract's test file if a test is added there.

## Log

- 2026-11-26: Created.
