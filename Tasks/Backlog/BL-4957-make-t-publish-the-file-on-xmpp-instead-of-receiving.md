---
id: BL-4957
title: Make -T publish the file on xmpp:// instead of receiving
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppProtocolHandler.cs, Curl.Extended.Protocol.Xmpp.UnitTests/XmppUploadTests.cs, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-4957 — Make -T publish the file on xmpp:// instead of receiving

## Goal

`curl -T up.txt xmpp://host/...` publishes the file's bytes as `-d` does: the server receives a message stanza whose body is filebody (or exit 2 before connecting if -T has no sensible XMPP meaning). It never subscribes, fetches or consumes.

## Context

Split from BL-4903 (outside fitness review 2026-11-11, finding EXT-008). Today the handler reads only `context.PostData`, so `-T` falls through to the receive path. `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ProtocolHandler.cs` `RequestBodyAsync` shows the pattern: when `PostData` is null and `context.Upload` is set, copy the upload stream into the body and take the publish path.

## Acceptance criteria

- [ ] `-T up.txt` (holding `filebody`) on xmpp:// sends `filebody` as the publish payload, or exits 2 before connecting where an upload has no meaning; a unit test in `Curl.Extended.Protocol.Xmpp.UnitTests` pins it.
- [ ] `Documentation/Conformance/Extended/Xmpp.md` states what `-T` does.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Stand-ins in PowerShell or C#, never Python.

## Log

- 2026-11-11: Created.
