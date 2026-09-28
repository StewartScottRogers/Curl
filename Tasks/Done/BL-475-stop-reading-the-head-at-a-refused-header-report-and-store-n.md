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
completed: 2026-09-27
---
# BL-475 — Stop reading the head at a refused header: report and store nothing after it

## Goal

When `HttpResponseBodyReader.FindHeadRefusal` refuses a header (an invalid Content-Length, a refused Transfer-Encoding, too many content codings), `HttpProtocolHandler` neither reports the `-v` lines of the headers after it nor stores the cookies of the `Set-Cookie` headers after it, matching what curl 8.21.0 prints and writes to the `-c` jar for the same response.

## Context

- The refusal is decided after `HttpResponseHeadReader.ReadAsync` has read the whole head, so every header line after the refused one is still reported to `ITransferEvents`, and since BL-468 every `Set-Cookie` after it is stored as it arrives (before BL-468, cookies were stored from the head cut at the refusal, `HeadCurlRead`).
- curl 8.21.0 stops reading the head at the refused header (BL-364, BL-412 Notes), so it should print and store nothing after it. Measure first with `Record-CurlExchange.ps1`: `-s -v -c - http://127.0.0.1:<port>/` against `HTTP/1.1 200 OK`, `Content-Length: x`, `Set-Cookie: a=1`, `X-After: 1`, empty line; record stderr and the jar on stdout.
- A likely route: let `HttpResponseHeadReader` ask a per-header check (the framing and coding checks `FindHeadRefusal` makes are prefix checks over the headers in order) before releasing each held header, and stop at the first refused one.

## Acceptance criteria

- [x] A test over a scripted connection pins the `-v` events and the `ScriptedCookieStore` calls for a head whose refused header is followed by a `Set-Cookie` header and another header, as measured.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-468 (2026-09-27).
- Measured (2026-09-27, curl 8.21.0, `Record-CurlExchange.ps1`, `-s -v -c - http://127.0.0.1:<port>/`) against `HTTP/1.1 200 OK` + refused header + `Set-Cookie: a=1` + `X-After: 1` + empty line:
  - `Content-Length: x`: exit 8; stderr shows `< HTTP/1.1 200 OK`, then `* Invalid Content-Length: value` and `* closing connection #0`. No `< Content-Length: x`, no `< Set-Cookie`, no `< X-After`, no empty `<` line. The jar on stdout has only its three comment lines.
  - `Transfer-Encoding: bogus`: exit 61, same shape, `* Unsolicited Transfer-Encoding (bogus) found`.
  - `--compressed` with `Content-Encoding: gzip` ×6 in one header: exit 61, same shape, `* Reject response exceeding limit of 5 content encodings`.
  - With `Set-Cookie: b=2` and `X-Before: 1` before `Content-Length: x`: `* Added cookie b="2" ...`, `< Set-Cookie: b=2`, `< X-Before: 1`, then the refusal. The jar holds `b` only.
- Route taken: `HttpResponseHeadReader` holds a final head's whole headers (lines, cookie callback, HTTP/1.0 keep-alive line) until the head ends. It then asks a new `FindRefusal` (the handler passes `HttpResponseBodyReader.FindHeadRefusal`) once of the whole head, releases only the headers before the refused one, and drops the rest and the empty line. The handler reads `headReader.Refusal` and no longer calls `FindHeadRefusal` itself. 1xx heads still release each header as soon as it is whole.
- Why not check each header as it arrives: `FindHeadRefusal` checks a prefix in O(n), so checking every prefix is O(n²) on a head of thousands of `Content-Length` headers (the head limit allows ~16000). Checking once keeps the binary search's O(n log n). Cost: a final head's `-v` header lines come out when the head ends rather than line by line. Their bytes and order are the same. This is an internal design choice, not a behaviour one, so it is recorded here and in the XML docs rather than in an ADR (`Documentation` is outside `touches`).
- Not done: failing first with the fix stashed was skipped because the stash command was denied. The new test would have seen `< Set-Cookie: a=1` and `< X-After: 1` under the old code, which reported every header as it arrived.
- Follow-up filed: BL-478. A head that fails *after* a refused header (a line with no colon, a peer that never ends the head) still reports the later failure and releases every held header. curl stops at the refused header.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A refused response header ends the -v head lines and the cookies stored: nothing from it on is reported or stored, as curl 8.21.0 does
