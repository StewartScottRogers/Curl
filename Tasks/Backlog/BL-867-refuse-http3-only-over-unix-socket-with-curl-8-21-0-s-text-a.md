---
id: BL-867
title: Refuse --http3-only over --unix-socket with curl 8.21.0's text and exit 96
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-839]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-867 — Refuse --http3-only over --unix-socket with curl 8.21.0's text and exit 96

## Goal

`curl --http3-only --unix-socket <path> https://host/` fails before connecting with exit 96 (`CurlExitCode.QuicConnectError`) and the message `HTTP/3 cannot be used over UNIX domain sockets`, as curl 8.21.0 does.

## Context

- `Documentation/Planning/Decisions/ADR-0187-http-3-stream-resets-follow-curl-8-21-0-and-a-refused-stream-is-retried-on-a-new-connection.md` (BL-839) makes `curl-8_21_0` the release Curl's HTTP/3 follows and lists this row: `Curl_conn_may_http3` in `lib/vquic/vquic.c` at `curl-8_21_0` fails a Unix-socket transport with `failf(data, "HTTP/3 cannot be used over UNIX domain sockets")` and `CURLE_QUIC_CONNECT_ERROR`, before the non-HTTPS check (8.18.0 returned 96 with no message).
- The non-HTTPS refusal (`HTTP/3 requested for non-HTTPS URL`, exit 3) lives in `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` and `HttpTransferMessages.cs`; add this check beside it, in curl's order.
- Read curl's source for what `--http3` (not `-only`) does with `--unix-socket` (whether `Curl_conn_may_http3` is reached or the connect falls back to TCP) and pin that too.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` pins exit 96 and `HTTP/3 cannot be used over UNIX domain sockets` for `--http3-only` with a Unix socket and an `https://` URL, and that no connection is dialled.
- [ ] A test pins the Unix-socket check running before the non-HTTPS check (an `http://` URL with a Unix socket gives exit 96).
- [ ] A test pins what `--http3` with a Unix socket does, as read from `curl-8_21_0`, recorded under Notes.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage.

## Notes

- 2026-09-29 (lane 5): `touches` widened. `HttpProtocolHandler` cannot tell that
  `--unix-socket` is in use: the socket lives only on `TcpConnector.UnixSocket`
  (`Curl.Networking.UnitLibrary`), set by `CurlComposition.UnixSocketOf` in `Curl.Console`;
  neither `ITransferContext`, `ConnectTarget` nor `HttpRequestOptions` carries it, and
  curl refuses before connecting, so `ConnectResult.UnixSocketPath` comes too late. Plan:
  add `bool OverUnixSocket` (init, default false) to `HttpRequestOptions`
  (`Curl.Protocol.Abstractions.UnitLibrary`, with a test there), set it in
  `Curl.Console/HttpRequestOptionsMapping.cs` from `options.UnixSocketPath is not null`
  (test in `Curl.Console.UnitTests`), and in `HttpProtocolHandler` check it first among
  the HTTP/3 refusals. `Curl.Console` is held by BL-650 in Doing, so the task went back to
  Backlog until that finishes.
- What `curl-8_21_0` does, read from the source and to pin in the tests:
  `Curl_conn_may_http3` (`lib/vquic/vquic.c`) checks the Unix transport first
  (`failf` "HTTP/3 cannot be used over UNIX domain sockets", `CURLE_QUIC_CONNECT_ERROR`
  = 96), then the non-HTTPS check (exit 3). `cf-https-connect.c` calls it only when
  HTTP/3 is wanted: for `--http3-only` the failure ends the transfer before any connect
  (so exit 96 for both `https://` and `http://` URLs); for `--http3` the failure only drops
  `h3` from the ALPN list, the connect goes on over TCP (here, the Unix socket), and since
  `failf` already filled the error buffer, a later failure reports that message with its
  own exit code - the same shape as `ExchangeWithoutHttp3Async` for the proxy refusals
  (ADR-0223), but with exit 96 instead of 3 for `--http3-only`. The `-v` line
  `closing connection #-1` follows the refusal as for the other pre-connect refusals.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Console (HttpRequestOptionsMapping) to pass --unix-socket to the HTTP handler; Curl.Console is held by BL-650 in Doing
