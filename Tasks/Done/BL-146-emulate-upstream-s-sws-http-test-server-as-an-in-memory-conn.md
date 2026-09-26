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
completed: 2026-09-26
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

- [x] `SwsHttpServerConnector` (or a name that says what it does) implements `IConnector`; tests
      show a GET receives `<data>`, a path ending `0002` receives `<data2>`, a POST body is read
      by `Content-Length` and by chunked encoding, and the received bytes are recorded in order
      across two requests on one connection.
- [x] Each unsupported `<servercmd>` command is reported by name, so a case using it can be
      skipped with a reason (tested), and follow-up tasks are filed for them.
- [x] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10 per
      method, per `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Delivered by one session rather than the full `/feature` agent chain: the scope was one
  library and its tests, and every rule was pinned by reading `sws.c`, `getpart.c` and
  `runner.pm` at `curl-8_21_0` directly.
- Shape: `SwsHttpServerConnector` (public, `IConnector`) → `SwsHttpServerConnection`
  (`IConnection`), with `SwsHttpRequestFraming`, `SwsHttpRequestLine`, `SwsHttpReplySelector`,
  `SwsHttpReply` and `SwsServerCommands` internal. 57 new tests in `SwsHttpServerConnectorTests`.
- Faithful to sws, found while reading it (no choice involved, so no ADR):
  - Part number = number starting the path's last segment, `% 10000` when over 10000, else 0;
    a path with no number serves this case's `<data>` (runtests writes `Testnum` to the
    server command file, which sws falls back to).
  - The connection closes only on `swsclose` in the reply, an empty or missing part, or
    `swsclose` in `<servercmd>`. A request's `Connection: close` or HTTP/1.0 does not close
    it: sws sets `req->open` from them but `sws_send_doc` overwrites it with `persistent`.
  - Reply parts get only what sws's `getpart` does: base64-decoding for any `base64`
    attribute and a cut last byte for any `nonewline` attribute; `crlf` is already applied by
    `UpstreamTestFileExpander` (runner.pm's `prepro`). Undecodable base64 sends nothing.
  - Chunked wins over `Content-Length`; the first non-zero `Content-Length` counts; one that
    does not parse leaves the body empty. Unknown `<servercmd>` lines are ignored, as sws
    ignores them (so `pipe`, which 8.21.0's sws no longer knows, is not reported).
  - A malformed first line gets sws's 404 document and a close.
- Defaults taken where the emulation has no upstream equivalent:
  - A read with no reply waiting returns 0 (closed) rather than blocking: in memory the
    client is the only writer, so a blocked read could only hang the test.
  - Writes after the server closed are accepted and dropped unrecorded, as a socket write into
    a closed peer can succeed while sws never reads it; `ReceivedBytes` excludes them.
  - Replies are handed over whole, not in sws's 20-byte writes; the reader still gets them
    in pieces when its buffer is smaller.
- Follow-ups filed: BL-259 (`auth_required`, `no-expect`, `skip`), BL-260 (`idle`, `stream`,
  `delay`, `writedelay`, `connection-monitor`, `upgrade`, `<postcmd>` `wait`), BL-261
  (auth, `swsbounce` and `CONNECT` part selection).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. SwsHttpServerConnector serves a case's <data>/<dataN> over in-memory connections, framing requests by Content-Length and chunked, recording received bytes, and reporting unsupported servercmd commands
