---
id: BL-1415
title: Carry --proxy-http2 from the parsed options to the connector in Curl.Console (ADR-0408)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1416, BL-1414]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1415 — Carry --proxy-http2 from the parsed options to the connector in Curl.Console (ADR-0408)

## Goal

Off Windows, `curl --proxy-http2 -x https://proxy ...` reaches the connector with proxy HTTP/2 requested, so the transfer runs through an HTTP/2 tunnel end to end.

## Context

- ADR-0408 decision 5. BL-1416 parses the option in `Curl.Cli.UnitLibrary`, and BL-1414 adds the connector option in `Curl.Networking.UnitLibrary`.
- `Curl.Console` builds the connect request from the parsed options; start at `HttpVersionMapping.cs` and the connector set-up.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test shows the parsed proxy-HTTP/2 setting reaches the connector option, and that leaving it out leaves the option off.
- [ ] A test over a fake connector runs a transfer through the HTTP/2 tunnel.
- [ ] `--ai-help` still describes `--proxy-http2` correctly.
- [ ] `Curl.Console` keeps 100% line and branch coverage. `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
