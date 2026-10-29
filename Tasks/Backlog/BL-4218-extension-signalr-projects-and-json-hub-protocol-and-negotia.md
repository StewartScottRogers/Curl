---
id: BL-4218
title: Extension SignalR: projects and JSON hub protocol and negotiate wire format
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4092]
touches: [Curl.Extension.SignalR.UnitLibrary, Curl.Extension.SignalR.UnitTests, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4218 — Extension SignalR: projects and JSON hub protocol and negotiate wire format

## Goal

`Curl.Extension.SignalR.UnitLibrary` and `Curl.Extension.SignalR.UnitTests` exist, with a pure codec for the ASP.NET Core SignalR JSON hub protocol and the negotiate response, so BL-4219 and BL-4108 build the client on it.

## Context

Split from BL-4108 for size (its Notes: wire format, then the client flow, then the options). Extension rules: ADR-0999, ADR-1003 (one single-line `<Folder Name="/Extensions/SignalR/">` in `Curl.slnx` under the marker comment). Specification: dotnet/aspnetcore `src/SignalR/docs/specs/HubProtocol.md` (JSON encoding, record separator 0x1E, handshake request `{"protocol":"json","version":1}` and response, message types 1 Invocation, 2 StreamItem, 3 Completion, 4 StreamInvocation, 5 CancelInvocation, 6 Ping, 7 Close) and `TransportProtocols.md` (the `POST /negotiate?negotiateVersion=1` response: `connectionId`, `connectionToken`, `negotiateVersion`, `availableTransports`, the redirect form `url` + `accessToken`, and `error`). JSON with `System.Text.Json`'s `Utf8JsonReader`/`Utf8JsonWriter` only (AOT-safe, no serializer reflection). Reference only `Curl.Extension.Abstractions.UnitLibrary` and `Curl.Protocol.Abstractions.UnitLibrary` (and `Curl.WebSocket.UnitLibrary` if needed); no `Curl.Protocol.*` library, no other extension. No handler or registration here: BL-4108 registers it.

## Acceptance criteria

- [ ] `Curl.Extension.SignalR.UnitLibrary` and `Curl.Extension.SignalR.UnitTests` exist in `/Extensions/SignalR/` in `Curl.slnx` as one line under the marker comment, and the build's `VerifyExtensionIsolation` passes.
- [ ] The library writes the handshake request, Invocation, StreamInvocation and CancelInvocation messages and Ping, each terminated by 0x1E, and reads the handshake response and every message type 1 to 7 from a byte stream split at any point; unit tests cite the HubProtocol.md section each checks.
- [ ] The library reads a negotiate response (version 0 and 1, the redirect form, and `error`) and builds the WebSocket URL with the `id` query parameter (connectionToken for version 1, connectionId for 0); unit tests cite TransportProtocols.md.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the specification version followed and what the codec leaves out (MessagePack, Long Polling, Server-Sent Events transports) and why.
- [ ] `Curl.Extension.SignalR.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.SignalR.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Put codec files under `HubProtocol/` and `Negotiate/` folders so BL-4219 can work in `Transport/` beside them.

## Log

- 2026-10-29: Created.
