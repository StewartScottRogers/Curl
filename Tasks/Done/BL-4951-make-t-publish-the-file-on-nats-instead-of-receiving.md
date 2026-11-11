---
id: BL-4951
title: Make -T publish the file on nats:// instead of receiving
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs, Curl.Extended.Protocol.Nats.UnitTests/NatsUploadTests.cs, Documentation/Conformance/Extended/Nats.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4951 — Make -T publish the file on nats:// instead of receiving

## Goal

`curl -T up.txt nats://host/...` publishes the file's bytes as `-d` does: the server receives PUB foo 8 then filebody. It never subscribes, fetches or consumes.

## Context

Split from BL-4903 (outside fitness review 2026-11-11, finding EXT-008). Today the handler reads only `context.PostData`, so `-T` falls through to the receive path. `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ProtocolHandler.cs` `RequestBodyAsync` shows the pattern: when `PostData` is null and `context.Upload` is set, copy the upload stream into the body and take the publish path.

## Acceptance criteria

- [x] `-T up.txt` (holding `filebody`) on nats:// sends `filebody` as the publish payload, or exits 2 before connecting where an upload has no meaning; a unit test in `Curl.Extended.Protocol.Nats.UnitTests` pins it.
- [x] `Documentation/Conformance/Extended/Nats.md` states what `-T` does.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Stand-ins in PowerShell or C#, never Python.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: c59a2cc8 c3a84bef a0b49a29.
- 2026-11-11: Done. -T publishes the file like -d; -d wins when both given. Build clean, fast tests green, quality 0 failing.
- 2026-11-11: Doing -> Done. -T publishes the file on nats://; tests and quality gate green
