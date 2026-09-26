---
id: BL-146
title: Emulate upstream's sws HTTP test server as an in-memory connector
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-144]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-146 — Emulate upstream's sws HTTP test server as an in-memory connector

## Goal

An `IConnector` whose connections are served by an in-memory emulation of upstream's `sws`
HTTP test server, answering from a test case's `<reply>` and recording every byte it received.

## Context

- ADR-0013, decisions 4 and 7: the HTTP emulation comes first; no socket is opened.
- Upstream's server: `tests/server/sws.c` at `curl-8_21_0`
  (https://github.com/curl/curl/blob/curl-8_21_0/tests/server/sws.c), its behaviour described
  in `docs/tests/FILEFORMAT.md` under `<reply>` and `<servercmd>`.
- Scope for this task: read one request (headers, then a body by `Content-Length` or chunked);
  answer with `<data>`, or with `<dataN>` when the request path ends in a number that selects
  it as sws does; keep the connection for the next request unless the reply closes it.
  `<servercmd>` commands such as `auth_required`, `idle`, `stream`, `writedelay`, `skip`,
  `connection-monitor` and `pipe` may be reported as unsupported and filed as follow-up.
- The recording is what BL-147 compares against `<verify><protocol>`.
- `Curl.Console.UnitTests/ScriptedConnector.cs` shows the `IConnector` / `IConnection` shape.

## Acceptance criteria

- [ ] `SwsHttpServerConnector` (or a name that says what it does) implements `IConnector`; tests
      show a GET receives `<data>`, a path ending `0002` receives `<data2>`, a POST body is read
      by `Content-Length` and by chunked encoding, and the received bytes are recorded in order
      across two requests on one connection.
- [ ] Each unsupported `<servercmd>` command is reported by name, so a case using it can be
      skipped with a reason (tested), and follow-up tasks are filed for them.
- [ ] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10 per
      method, per `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
