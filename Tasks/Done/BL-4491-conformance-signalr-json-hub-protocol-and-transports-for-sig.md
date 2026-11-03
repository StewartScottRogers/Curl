---
id: BL-4491
title: Conformance: SignalR JSON hub protocol and transports for signalr:// and signalrs://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/SignalR, Curl.Conformance.Extended.UnitTests/SignalR, Documentation/Conformance/Extended/SignalR.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4491 — Conformance: SignalR JSON hub protocol and transports for signalr:// and signalrs://

## Goal

Curl's `signalr://` and `signalrs://` client is measured against ASP.NET Core SignalR's hub protocol (JSON) and
transport protocol (negotiate and WebSocket) through the real curl command line, every client requirement is in a
matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specifications: dotnet/aspnetcore `src/SignalR/docs/specs/HubProtocol.md` (handshake request and response, the
0x1E record separator, message types 1 to 7: Invocation, StreamItem, Completion, StreamInvocation, CancelInvocation,
Ping, Close; invocation ids; non-blocking invocations; JSON encoding rules) and `TransportProtocols.md` (negotiate
POST, `negotiateVersion` 0 and 1, `connectionId` and `connectionToken`, redirect `url` and `accessToken`, `error`;
the WebSocket transport and its `id` query), plus RFC 6455's client rules the handler relies on (masked text frames,
ping/pong, close). Use the specs at the aspnetcore tag the library cites, else `main`, and name it in the matrix.

Library: `Curl.Extended.Protocol.SignalR.UnitLibrary` (`CLAUDE.md`: `HubProtocol/`, `Negotiate/`, `Transport/`,
`Client/`; ADR-1032, ADR-1038, ADR-1084). `-d` names the call (`{"type":1|4,"target":...,"arguments":[...]}`); each
result, stream item or hub call is one JSON line.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`SignalR:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. The reference hub implements the protocol from the specification itself
and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix SignalR conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/SignalR.md` gives every requirement of HubProtocol.md (JSON),
      TransportProtocols.md (negotiate and WebSocket) and the RFC 6455 client rules relied on that binds a client a
      `SignalR:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/SignalR/` run curl in process for `signalr://` and `signalrs://`
      and assert HTTP and WebSocket bytes, stdout, stderr and exit code for at least: negotiate version 0 and 1, a
      negotiate redirect with an access token, a negotiate `error`, a handshake error, an invocation and its
      completion with a result and with an error, a stream invocation with items and completion, a hub call from the
      server, Ping, a Close with and without an error, records split across frames and several in one frame.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix SignalR conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/SignalR/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes
- Specs measured at dotnet/aspnetcore `main`: the library cites no tag (named in the matrix).
- Two cases fail on the extension library and are listed in `KnownFailures.txt`: `SignalR:HUB-Invocation-3`
  (BL-4583, a hub call with an invocation id gets no Completion) and `SignalR:RFC6455-1.4-1` (BL-4584, no
  WebSocket Close frame when the session ends).
- `SignalRReferenceFrame.TryRead` was at complexity 14; the payload length and unmasking moved to their own
  private methods. `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`: 0 failing members.
- `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Windows). The cases use only loopback reference
  routes and no paths, so they are platform-neutral; the Linux and macOS criterion is met by that.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 0d3075a4 d0dc803a cf3940fa d8e3fb0e 95f0eb06 ef6c9026.
- 2026-11-02: Doing -> Done. SignalR conformance matrix and cases for signalr:// and signalrs:// land; two library defects filed as BL-4583 and BL-4584
