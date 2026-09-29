---
id: BL-878
title: Add alt-svc seams: Alt-Used header, Alt-Svc response hook and connecting to an alternative
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-878 — Add alt-svc seams: Alt-Used header, Alt-Svc response hook and connecting to an alternative

## Goal

The HTTP handler and the TCP connector can do everything `--alt-svc` needs from them, so
BL-623 only has to wire `AltSvcCache` (`Curl.Core.UnitLibrary\AltSvc`, ADR-0175) into
`Curl.Console`: a transfer can be told to dial an alternative host and port, the request
then carries `Alt-Used`, and each `Alt-Svc` response header over HTTPS is handed to a
store that answers what it added.

## Context

- Split out of BL-623, whose `touches` (Cli, Console) cannot reach these projects. The
  measurements are in BL-623's notes (curl 8.21.0 mingw Schannel, 2026-09-29).
- Model the store on `ICookieStore`: an `IAltSvcStore` (name to taste) in
  `Curl.Protocol.Abstractions.UnitLibrary`, set on `HttpRequestOptions`, that the handler
  calls with each `Alt-Svc` header value of an HTTPS response and that returns the
  alternatives it added (`host:port` and ALPN), for the verbose lines.
- The alternative to dial rides on the transfer's `ConnectTarget` (or options) and is applied
  by `TcpConnector` the way a `--connect-to` mapping is (`ConnectToMappings`, ADR-0079):
  dial the alternative, keep the origin's `Host` and TLS name. curl uses an alternative only
  when no `--connect-to` mapping matched.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests`: a target carrying an alternative dials its host and port,
      keeps the origin's TLS name, first writes
      `* Alt-svc connecting from [h1]<host>:<port> to [h1]<althost>:<altport>` before
      `* Host <althost>:<altport> was resolved.`, and a refused alternative fails with exit 7
      and `Failed to connect to <host>:<port> via <althost>:<altport> after N ms: Could not connect to server`;
      a matching `--connect-to` mapping wins over the alternative.
- [ ] `Curl.Protocol.Http.UnitTests`: a request to an alternative sends
      `Alt-Used: <althost>:<altport>` where curl 8.21.0 puts it (after `Referer`, before
      `Cookie` and the `-H` headers; not sent when an `-H Alt-Used` is given), pinned to
      bytes measured with `Record-CurlExchange.ps1 -Tls -k` including `-e`, `-b` and `-H`.
- [ ] `Curl.Protocol.Http.UnitTests`: each `Alt-Svc` header of an HTTPS response goes to the
      store, and `-v` writes one `* Added alt-svc: <host>:<port> over <alpn>` line per
      alternative added before the `< Alt-Svc:` header line; over plain `http://` the store is
      not called.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member
      for each library changed.

## Notes

## Log

- 2026-09-29: Created.
