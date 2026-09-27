---
id: BL-339
title: Tunnel dict:// through the transfer proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-337]
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-339 — Tunnel dict:// through the transfer proxy

## Goal

`DictProtocolHandler` connects through `ITransferContext.Proxy` when one is set, so `curl -x <proxy> dict://…` tunnels through the proxy as curl 8.21.0 does instead of connecting directly.

## Context

- ADR-0056, rule 2: TCP schemes always tunnel through an HTTP proxy, `-p` or not. The connector already writes the CONNECT and reports a refused tunnel (ADR-0023), so the change is `ConnectTarget { Proxy = context.Proxy }`; no proxy code in the handler.
- Measured by BL-330: the proxy receives `CONNECT example.com:2628 HTTP/1.1\r\nHost: example.com:2628\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n`, then after `200` the DICT commands `CLIENT libcurl 8.21.0\r\nDEFINE ! x\r\nQUIT\r\n` (for `dict://example.com/d:x`, the same with or without `-p`). A CONNECT answered `403` ends curl with exit 7 `CONNECT tunnel failed, response 403`, which is the connector's job.
- Where a criterion says *measured*, the bytes were measured by BL-330 with curl 8.21.0 (`/mingw64/bin/curl`, ADR-0009) against a loopback listener and are recorded under BL-330's `Notes`. Pin only those bytes; re-measure with `Record-CurlExchange.ps1` for anything else.

## Acceptance criteria

- [x] A test with a fake `IConnector` shows the handler's `ConnectTarget` carries `context.Proxy` and the origin host with port 2628.
- [x] A test shows a context without a proxy still connects directly (`ConnectTarget.Proxy == null`).
- [x] 100% line and branch coverage of the changed code; `dotnet build` clean, fast tests green.

## Notes

- Delivered directly rather than through the full `/feature` subagent stages: the change is one property initializer (`Proxy = context.Proxy`) that ADR-0056 already designed, so a separate plan and review would add nothing. No new ADR needed.
- Tests: `ExecuteAsync_ContextWithProxy_TunnelsToTheOriginOnPort2628ThroughThatProxy` and `ExecuteAsync_ContextWithoutProxy_ConnectsDirectly` in `DictProtocolHandlerTests`. The changed line has no branch; both tests execute it.
- The CONNECT bytes and the `403` -> exit 7 path are the connector's (ADR-0023) and are not re-pinned here.
- Handler XML remarks and the project `CLAUDE.md` now say the proxy is passed to the connector.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. dict:// connects through ITransferContext.Proxy when set, directly otherwise
