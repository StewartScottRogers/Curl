---
id: BL-6049
title: Write curl's -v lines for a CONNECT the proxy refuses under -f
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6024]
touches: [Curl.Networking.UnitLibrary/TcpConnector.HttpProxyTunnel.cs, Curl.Networking.UnitLibrary/TcpConnector.ProxyChallenge.cs, Curl.Networking.UnitLibrary/TcpConnector.ProxyTunnelFailure.cs, Curl.Networking.UnitTests/TcpConnectorProxyAuthProblemTests.cs, Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.Connecting.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectTargets.cs, Curl.Protocol.Abstractions.UnitTests/ConnectTargetFailsOnHttpErrorStatusTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-27
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

- [x] For a 403 and for a 407 with no challenge under `-v -f`, the `*` lines after the reply head match reference curl, measured with `Record-CurlExchange.ps1`.
- [x] For a 407 with `Proxy-Authenticate: Basic` to `--proxy-user` under `-v -f`, no `CONNECT tunnel failed` line is written.
- [x] Without `-f`, `-v` still writes `CONNECT tunnel failed, response N`.
- [x] A focused unit test in each changed library's own `.UnitTests` project pins the behaviour, and `dotnet build` is clean and the fast tests pass.

## Notes

`touches` includes the `ConnectTarget` contract file; widen it with that contract's test file if a test is added there.

- 2026-11-27 (lane 3): `ConnectTarget.FailsOnHttpErrorStatus`, set by the HTTP handler for `-f` only, picks the line in
  `TcpConnector.TunnelFailure` (ADR-1466). `touches` widened to `HttpProtocolHandler.ConnectTargets.cs` (where the target is
  built) and the contract's new test file; the only other task in Doing (BL-6028) names neither.
- Measured with `Record-CurlExchange.ps1` as a proxy, reference curl 8.22.0 and Curl, `-sS -v -x http://127.0.0.1:P
  https://example.test/x`; the `*` and `curl:` lines are identical in all ten cases. Under `-f`: 403 (with and without a
  body), 407 with no challenge (user, `Connection: close`), 407 Basic with no user: `* The requested URL returned error: N`.
  407 Basic to `-U a:b`, and `--proxy-anyauth` refused a second time: neither line. 302 under `-f`, 403 without `-f` and 403
  under `--fail-with-body`: `* CONNECT tunnel failed, response N`.
- The `ftp://` `-p -f` case differs from curl (exit 7, not 22): filed as BL-6083.
- Build clean, fast tests green. `Measure-CodeQuality.ps1` over the three libraries would run all 151 test projects
  (66 done after 60 minutes), past the deadline, so it was stopped and the three libraries' own test projects were measured
  and scored with `-SkipTestRun`: every changed member passes line, branch, complexity and CRAP; the 12 members listed
  failing are untouched ones whose tests live elsewhere.
- Platform-neutral: no paths, OS error text or certificates.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: f472d574 b40ab58a 966999ad 2ff1f16e.
- 2026-11-27: Doing -> Done. curl -v -f writes curl's lines after a refused CONNECT's reply head
