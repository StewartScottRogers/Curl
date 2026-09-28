---
id: BL-475
title: Stop reading the head at a refused header: report and store nothing after it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-475 — Stop reading the head at a refused header: report and store nothing after it

## Goal

When `HttpResponseBodyReader.FindHeadRefusal` refuses a header (an invalid Content-Length, a refused Transfer-Encoding, too many content codings), `HttpProtocolHandler` neither reports the `-v` lines of the headers after it nor stores the cookies of the `Set-Cookie` headers after it, matching what curl 8.21.0 prints and writes to the `-c` jar for the same response.

## Context

- The refusal is decided after `HttpResponseHeadReader.ReadAsync` has read the whole head, so every header line after the refused one is still reported to `ITransferEvents`, and since BL-468 every `Set-Cookie` after it is stored as it arrives (before BL-468, cookies were stored from the head cut at the refusal, `HeadCurlRead`).
- curl 8.21.0 stops reading the head at the refused header (BL-364, BL-412 Notes), so it should print and store nothing after it. Measure first with `Record-CurlExchange.ps1`: `-s -v -c - http://127.0.0.1:<port>/` against `HTTP/1.1 200 OK`, `Content-Length: x`, `Set-Cookie: a=1`, `X-After: 1`, empty line; record stderr and the jar on stdout.
- A likely route: let `HttpResponseHeadReader` ask a per-header check (the framing and coding checks `FindHeadRefusal` makes are prefix checks over the headers in order) before releasing each held header, and stop at the first refused one.

## Acceptance criteria

- [ ] A test over a scripted connection pins the `-v` events and the `ScriptedCookieStore` calls for a head whose refused header is followed by a `Set-Cookie` header and another header, as measured.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-468 (2026-09-27).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
