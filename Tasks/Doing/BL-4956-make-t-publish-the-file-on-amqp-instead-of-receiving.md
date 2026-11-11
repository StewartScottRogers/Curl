---
id: BL-4956
title: Make -T publish the file on amqp:// instead of receiving
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs, Curl.Extended.Protocol.Amqp0.UnitTests/Amqp0UploadTests.cs, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-4956 — Make -T publish the file on amqp:// instead of receiving

## Goal

`curl -T up.txt amqp://host/...` publishes the file's bytes as `-d` does: the server receives basic.publish with body filebody (never basic.get). It never subscribes, fetches or consumes.

## Context

Split from BL-4903 (outside fitness review 2026-11-11, finding EXT-008). Today the handler reads only `context.PostData`, so `-T` falls through to the receive path. `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ProtocolHandler.cs` `RequestBodyAsync` shows the pattern: when `PostData` is null and `context.Upload` is set, copy the upload stream into the body and take the publish path.

## Acceptance criteria

- [ ] `-T up.txt` (holding `filebody`) on amqp:// sends `filebody` as the publish payload, or exits 2 before connecting where an upload has no meaning; a unit test in `Curl.Extended.Protocol.Amqp0.UnitTests` pins it.
- [ ] `Documentation/Conformance/Extended/Amqp0.md` states what `-T` does.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Stand-ins in PowerShell or C#, never Python.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 426bc5e2 e447e5dc a0b49a29.
