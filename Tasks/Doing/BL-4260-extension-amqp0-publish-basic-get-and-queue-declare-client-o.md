---
id: BL-4260
title: Extension Amqp0: publish, basic.get and queue declare client over IConnection for amqp and amqps
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4259]
touches: [Curl.Extension.Amqp0.UnitLibrary/Client, Curl.Extension.Amqp0.UnitTests/Client]
requirement: none
created: 2026-10-29
completed:
---
# BL-4260 — Extension Amqp0: publish, basic.get and queue declare client over IConnection for amqp and amqps

## Goal

An `IProtocolHandler` in `Curl.Extension.Amqp0.UnitLibrary` serves `amqp://` (port 5672) and `amqps://` (port 5671, TLS through the connector curl's handlers receive): it negotiates the connection, opens a channel, declares the URL's queue, and with `-d` publishes the data to it, otherwise takes one message with basic.get and writes its body to the output.

## Context

Split from BL-4117 (extension 16 of 63) for size by its first pass. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Specification: AMQP 0-9-1 (RabbitMQ). Builds on BL-4259's frame and method codec. URL form follows RabbitMQ's URI specification (`amqp://user:pass@host:port/vhost`, the first path segment URL-decoded is the virtual host, an empty one is `/`); choose and record how the queue (and an optional exchange and routing key) is named in the URL, and its exit codes, in an ADR marked "Decided by Claude under Stewart's delegation". Never reference a `Curl.Protocol.*` library other than Abstractions, or another extension. Do not register the handler in any catalogue here: BL-4117 does that.

## Acceptance criteria

- [ ] The handler sends the protocol header (4.2.2), answers connection.start with start-ok using the PLAIN mechanism and `--user` (or guest/guest), answers tune with tune-ok taking the smaller of frame-max and channel-max, opens the virtual host and channel 1, and closes channel and connection at the end; unit tests over a fake `IConnection` with recorded bytes.
- [ ] With a body it sends queue.declare (passive off, durable off) and basic.publish to the default exchange with the queue as routing key, header and body frames split at the negotiated frame-max; without a body it sends queue.declare and basic.get with no-ack, writes a get-ok body to the output and treats get-empty as an empty transfer; unit tests.
- [ ] A connection.close or channel.close from the server ends the transfer with an exit code and an error message naming the reply code and reply text; the mapping is recorded in the ADR.
- [ ] `amqps://` runs the same flow over TLS from the connector the handler receives; unit test with a fake connector.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4117's lane 9 prerequisite pass.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b6acbd07 64204dd7 e5ad1fae 17098196 14de81cc.
