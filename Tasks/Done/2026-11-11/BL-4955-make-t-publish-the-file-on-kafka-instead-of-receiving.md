---
id: BL-4955
title: Make -T publish the file on kafka:// instead of receiving
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaProtocolHandler.cs, Curl.Extended.Protocol.Kafka.UnitTests/KafkaUploadTests.cs, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4955 — Make -T publish the file on kafka:// instead of receiving

## Goal

`curl -T up.txt kafka://host/...` publishes the file's bytes as `-d` does: the server receives a Produce request whose record value is filebody. It never subscribes, fetches or consumes.

## Context

Split from BL-4903 (outside fitness review 2026-11-11, finding EXT-008). Today the handler reads only `context.PostData`, so `-T` falls through to the receive path. `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ProtocolHandler.cs` `RequestBodyAsync` shows the pattern: when `PostData` is null and `context.Upload` is set, copy the upload stream into the body and take the publish path.

## Acceptance criteria

- [x] `-T up.txt` (holding `filebody`) on kafka:// sends `filebody` as the publish payload, or exits 2 before connecting where an upload has no meaning; a unit test in `Curl.Extended.Protocol.Kafka.UnitTests` pins it.
- [x] `Documentation/Conformance/Extended/Kafka.md` states what `-T` does.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Stand-ins in PowerShell or C#, never Python.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: c6edaed3 16d86d3a a0b49a29.
- 2026-11-11: Doing -> Done. Kafka upload publishes the file; test and docs added
