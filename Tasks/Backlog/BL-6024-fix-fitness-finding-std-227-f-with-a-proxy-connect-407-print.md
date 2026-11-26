---
id: BL-6024
title: Fix fitness finding STD-227: -f with a proxy CONNECT 407 prints a different error text
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6019]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.Connecting.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerProxyConnectFailTests.cs, Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs, Curl.Protocol.Abstractions.UnitTests/ConnectResultProxyAuthenticationTests.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/NumberedConnectFailure.cs, Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs, Curl.Networking.UnitTests/TcpConnectorProxyAuthProblemTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6024 — Fix fitness finding STD-227: -f with a proxy CONNECT 407 prints a different error text

## Goal

STD-227 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: -f with a proxy CONNECT 407 prints a different error text. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed · **Area:** HTTP proxy / `-f`, Windows build.
- **Mock servers:** an HTTP proxy on `127.0.0.1:24051` that answers every CONNECT with 407; any HTTPS target (e.g. `127.0.0.1:24443`).
- **Repro:** `curl -sSk -f --proxy-user a:b -x http://127.0.0.1:24051 https://127.0.0.1:24443/hello -w "[%{exitcode}]\n"`
- **Expected (REF):** `curl: (22) HTTP response code said error`, `[22]`.
- **Actual (.NET):** `curl: (22) The requested URL returned error: 407`, `[22]`.
- **Acceptance check:** stderr equals REF's line.

## Acceptance criteria

- [ ] stderr equals REF's line.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-26 (lane 13, no code written): reproduces by code reading. `HttpProtocolHandler.FailedConnectResult`
  (STD-125, BL-5443) always gives `The requested URL returned error: N` under `-f`; the connector does not say whether
  curl would have set `authproblem`.
- Measured with `Record-CurlExchange.ps1` (reference curl, `-sS -v -f -x http://127.0.0.1:P`, every reply `Content-Length: 0`):
  - `https://`, `--proxy-user a:b` or `--proxy-user :`, 407 with `Proxy-Authenticate: Basic realm="p"`: `-v` writes
    `Basic authentication problem, ignoring.`, no `returned error` line; stderr `curl: (22) HTTP response code said error`.
  - `https://`, 407 with no challenge (user or no user; `--proxy-anyauth`, `--proxy-ntlm`, `--proxy-digest`), 407 with a
    Basic challenge to no credential, to `--proxy-header Proxy-Authorization` or to `--proxy-digest`, and 403:
    `* The requested URL returned error: N` and the same `curl: (22)` text, as Curl prints now.
  - `http://` through the forward proxy, 407 (even after `Basic authentication problem, ignoring.`):
    `The requested URL returned error: 407`, so the forward proxy path does not change.
  - Not measured: a second 407 after `--proxy-anyauth` or `--proxy-digest` answered a challenge (the recorder did not
    answer the second CONNECT on the same connection; try `-AnswerHeldRequests`).
- So the rule fits: `HTTP response code said error` exactly when the final CONNECT 407 drew
  `<scheme> authentication problem, ignoring.` (`ConnectTunnelVerboseLines.RefusedScheme` / `ReportProblemLines`).
- Plan: a `ConnectResult` flag (curl's `authproblem`), set in `TcpConnector.TunnelFailure`, kept by the two places that
  rebuild a failed result (`TcpConnector.WithTimingsWhenFailedWithout`, `NumberedConnectFailure`), read by
  `FailedConnectResult`; ADR for the choice. `SocketCallbackConnector.Rewrapped` copies successes only.
- Requeued: `TcpConnector.cs` is held by BL-6019 (Doing), so `touches` was widened and BL-6019 added to `depends-on`.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 47fc27c5 69116afe 3d836d5e.
- 2026-11-26: Doing -> Backlog. Needs Curl.Networking.UnitLibrary/TcpConnector.cs, held by BL-6019; no code written, rule measured and recorded in Notes
