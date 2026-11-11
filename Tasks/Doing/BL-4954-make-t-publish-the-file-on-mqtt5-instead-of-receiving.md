---
id: BL-4954
title: Make -T publish the file on mqtt5:// instead of receiving
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ProtocolHandler.cs, Curl.Extended.Protocol.Mqtt5.UnitLibrary/WebSocket/Mqtt5WebSocketProtocolHandler.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/Mqtt5UploadTests.cs, Documentation/Conformance/Extended/Mqtt5.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-4954 — Make -T publish the file on mqtt5:// instead of receiving

## Goal

`curl -T up.txt mqtt5://host/...` publishes the file's bytes as `-d` does: the server receives a PUBLISH with payload filebody, on both the TCP and WebSocket handlers. It never subscribes, fetches or consumes.

## Context

Split from BL-4903 (outside fitness review 2026-11-11, finding EXT-008). Today the handler reads only `context.PostData`, so `-T` falls through to the receive path. `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ProtocolHandler.cs` `RequestBodyAsync` shows the pattern: when `PostData` is null and `context.Upload` is set, copy the upload stream into the body and take the publish path.

## Acceptance criteria

- [ ] `-T up.txt` (holding `filebody`) on mqtt5:// sends `filebody` as the publish payload, or exits 2 before connecting where an upload has no meaning; a unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` pins it.
- [ ] `Documentation/Conformance/Extended/Mqtt5.md` states what `-T` does.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Stand-ins in PowerShell or C#, never Python.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: d8c092e9 c751cbf9 a0b49a29.
