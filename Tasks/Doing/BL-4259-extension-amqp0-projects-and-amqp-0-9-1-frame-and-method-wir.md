---
id: BL-4259
title: Extension Amqp0: projects and AMQP 0-9-1 frame and method wire format
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4092]
touches: [Curl.Extension.Amqp0.UnitLibrary, Curl.Extension.Amqp0.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4259 — Extension Amqp0: projects and AMQP 0-9-1 frame and method wire format

## Goal

`Curl.Extension.Amqp0.UnitLibrary` encodes and decodes every AMQP 0-9-1 frame, method, content header and field table a publish/get/queue-declare client sends or receives, as pure byte-level code with no I/O.

## Context

Split from BL-4117 (`amqp://`, `amqps://`, extension 16 of 63) for size by its first pass. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Specification: AMQP 0-9-1 (amqp0-9-1.pdf, 2008-11-13, with RabbitMQ's errata and its `amqp0-9-1.extended.xml` class/method numbers). Reference only `Curl.Protocol.Abstractions.UnitLibrary` and the shared layers; never a `Curl.Protocol.*` library or another extension (in particular not `Curl.Extension.Amqp1.*` from BL-4118). Use the `new-project` skill; the slnx line is one single-line `<Folder Name="/Extensions/Amqp0/">` directly under the marker comment (ADR-1003). Pattern to follow: BL-4242 (Mqtt5 wire format). Put the code in `Wire/` (frames, data types, field tables) and `Methods/` (one class per method), so BL-4260 (`Client/`) works in a disjoint folder. Do not register the extension in any catalogue here: BL-4117 does that.

## Acceptance criteria

- [ ] `Curl.Extension.Amqp0.UnitLibrary` and `Curl.Extension.Amqp0.UnitTests` exist in `/Extensions/Amqp0/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [ ] The protocol header `AMQP\x00\x00\x09\x01` (section 4.2.2) and the general frame format (section 4.2.3: type, channel, size, payload, frame-end 0xCE) are written and read; a bad frame-end or a frame larger than the negotiated frame-max is a frame error.
- [ ] The data types (section 4.2.5: octet, short, long, long-long, bit packing, shortstr, longstr, timestamp) and field tables (section 4.2.5.5 with RabbitMQ's errata field types) are encoded and decoded; unit tests pin recorded bytes.
- [ ] The methods connection.start/start-ok/tune/tune-ok/open/open-ok/close/close-ok, channel.open/open-ok/close/close-ok, queue.declare/declare-ok, basic.publish, basic.get/get-ok/get-empty and basic.ack (section 1 class reference, class and method ids from the XML) are written and read, with the content header frame (section 4.2.6.1: class id, body size, property flags and the basic properties) and body frames split at frame-max; unit tests per method with recorded bytes.
- [ ] A malformed frame or method (truncated field, unknown frame type, unknown class/method id) raises one named exception type carrying the AMQP reply code (501 frame-error, 502 syntax-error, 503 command-invalid, 505 unexpected-frame), never an unrelated exception.
- [ ] The library meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4117's lane 9 prerequisite pass. BL-4260 then adds the client flow, BL-4117 the registration, description, ADR and `CURL_EXTENSIONS=0` test.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 71074988 6ea580bf f0cd5c18 b210128f 54704e52 f4f7f560 3d836d5e.
