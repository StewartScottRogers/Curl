---
id: BL-342
title: Tunnel mqtt:// through the transfer proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-337]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-342 — Tunnel mqtt:// through the transfer proxy

## Goal

`MqttProtocolHandler` connects through `ITransferContext.Proxy` when one is set, so `curl -x <proxy> mqtt://…` tunnels through the proxy as curl 8.21.0 does instead of connecting directly.

## Context

- ADR-0056, rule 2: TCP schemes always tunnel through an HTTP proxy, `-p` or not. The connector already writes the CONNECT and reports a refused tunnel (ADR-0023), so the change is `ConnectTarget { Proxy = context.Proxy }`; no proxy code in the handler.
- Measured by BL-330: the proxy receives `CONNECT example.com:1883 HTTP/1.1\r\nHost: example.com:1883\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n`, then after `200` the MQTT CONNECT packet (for `mqtt://example.com/t`). A CONNECT answered `403` ends curl with exit 7 `CONNECT tunnel failed, response 403`, which is the connector's job.
- Where a criterion says *measured*, the bytes were measured by BL-330 with curl 8.21.0 (`/mingw64/bin/curl`, ADR-0009) against a loopback listener and are recorded under BL-330's `Notes`. Pin only those bytes; re-measure with `Record-CurlExchange.ps1` for anything else.

## Acceptance criteria

- [x] A test with a fake `IConnector` shows the handler's `ConnectTarget` carries `context.Proxy` and the origin host with port 1883.
- [x] A test shows a context without a proxy still connects directly (`ConnectTarget.Proxy == null`).
- [x] 100% line and branch coverage of the changed code; `dotnet build` clean, fast tests green.

## Notes

- `MqttProtocolHandler.CreateTarget` now takes `context.Proxy` and sets `ConnectTarget.Proxy`; the connector writes the CONNECT (ADR-0056), so the handler holds no proxy code. `mqtts://` gets the same `Proxy` with `UseTls` true, matching the pattern of the other TCP schemes.
- Tests: `ExecuteAsync_ContextWithProxy_TunnelsToTheOriginOnPort1883ThroughThatProxy`, `ExecuteAsync_ContextWithoutProxy_ConnectsDirectly`. Curl.Protocol.Mqtt.UnitLibrary at 100% line and branch coverage; 72 MQTT tests green, full fast suite green.
- Library `CLAUDE.md` updated to say the proxy flows into `ConnectTarget`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. mqtt:// and mqtts:// tunnel through ITransferContext.Proxy via the connector
