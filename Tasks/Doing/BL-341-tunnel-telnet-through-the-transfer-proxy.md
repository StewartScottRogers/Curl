---
id: BL-341
title: Tunnel telnet:// through the transfer proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-337]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-341 — Tunnel telnet:// through the transfer proxy

## Goal

`TelnetProtocolHandler` connects through `ITransferContext.Proxy` when one is set, so `curl -x <proxy> telnet://…` tunnels through the proxy as curl 8.21.0 does instead of connecting directly.

## Context

- ADR-0056, rule 2: TCP schemes always tunnel through an HTTP proxy, `-p` or not. The connector already writes the CONNECT and reports a refused tunnel (ADR-0023), so the change is `ConnectTarget { Proxy = context.Proxy }`; no proxy code in the handler.
- Measured by BL-330: the proxy receives `CONNECT example.com:23 HTTP/1.1\r\nHost: example.com:23\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n` (for `telnet://example.com/`). A CONNECT answered `403` ends curl with exit 7 `CONNECT tunnel failed, response 403`, which is the connector's job.
- Where a criterion says *measured*, the bytes were measured by BL-330 with curl 8.21.0 (`/mingw64/bin/curl`, ADR-0009) against a loopback listener and are recorded under BL-330's `Notes`. Pin only those bytes; re-measure with `Record-CurlExchange.ps1` for anything else.

## Acceptance criteria

- [ ] A test with a fake `IConnector` shows the handler's `ConnectTarget` carries `context.Proxy` and the origin host with port 23.
- [ ] A test shows a context without a proxy still connects directly (`ConnectTarget.Proxy == null`).
- [ ] 100% line and branch coverage of the changed code; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
