---
id: BL-4257
title: Extension Amqp1: send and receive client over IConnection for amqp1 and amqp1s
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4255, BL-4256]
touches: [Curl.Extension.Amqp1.UnitLibrary/Client, Curl.Extension.Amqp1.UnitTests/Client, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4257 — Extension Amqp1: send and receive client over IConnection for amqp1 and amqp1s

## Goal

An `IProtocolHandler` in `Curl.Extension.Amqp1.UnitLibrary` serves `amqp1://host[:5672]/address` and `amqp1s://host[:5671]/address` (TLS through the connector curl's handlers receive): with a request body (`-d`, `-T`) it sends one message to the address, otherwise it receives one message from it and writes its body to the output.

## Context

Split from BL-4118 (AMQP 1.0, extension 17 of 63) for size by its first pass. Builds on BL-4255 (`Types/`) and BL-4256 (`Frames/`). Specification: OASIS AMQP Version 1.0. Scheme names `amqp1`/`amqp1s` because BL-4117's AMQP 0-9-1 extension (extension 16) owns `amqp://`/`amqps://`; BL-4118's ADR records that. Put the code in `Client/`. Never reference a `Curl.Protocol.*` library other than Abstractions, or another extension. Do not register the handler in any catalogue here: BL-4118 does that.

## Acceptance criteria

- [ ] SASL (Part 5 section 5.3): with `--user` the client offers PLAIN, otherwise ANONYMOUS, picks it from sasl-mechanisms and fails the transfer on a sasl-outcome other than ok; unit tests over a fake `IConnection` with recorded bytes.
- [ ] Send: open, begin, attach as sender (role false) to the URL's URL-decoded path as target, waits for flow credit, transfer with the body as a data section, waits for disposition accepted, then detach, end, close (Part 2 sections 2.4-2.7, Part 3 section 3.4); unit test.
- [ ] Receive: attach as receiver with the address as source, flow with link-credit 1, reads the transfer (across several frames when `more` is set), settles it accepted, writes the body of its data, amqp-value or amqp-sequence sections to the output, then closes; unit test.
- [ ] A rejected or released disposition, an error on attach, detach, end or close, or a missing mechanism ends the transfer with an exit code and the condition symbol and description in the error message; the mapping is recorded in an ADR marked "Decided by Claude under Stewart's delegation".
- [ ] `amqp1s://` runs the same flow over TLS from the connector the handler receives; unit test with a fake connector.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4118's lane 3 prerequisite pass.

## Log

- 2026-10-29: Created.
