---
id: BL-260
title: Retry a request without Expect after a 417 reply, as curl does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-175]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-260 — Retry a request without Expect after a 417 reply, as curl does

## Goal

A `417 Expectation Failed` reply to a request that carried `Expect: 100-continue` makes the handler resend the request without `Expect` and write only the second response, as curl 8.21.0 does.

## Context

- BL-175 (HttpContinueWaitConnection) treats any final status during the `100 Continue` wait as the response and leaves the body unsent. curl 8.21.0 instead, on a 417, resends the head without `Expect: 100-continue` followed by the body; measured in BL-175 with a server that answered 417 at once: the second request head and the 1048577-byte body followed on the same connection.
- Measure first with a loopback server: which connection the retry uses, what stdout, the header output and `%{size_request}` show, and what happens when the retry also gets 417.

## Acceptance criteria

- [x] The retry is measured on curl 8.21.0 and the command and bytes are in Notes.
- [x] A handler test replays a 417 then a 200 through the fakes (1-byte reads too) and matches the measured request bytes and output.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*` reports no failing member.

## Notes

### Measured on curl 8.21.0 (Schannel, mingw64), 2026-09-26

Loopback Python server on 127.0.0.1:18260 that answers the `Expect: 100-continue` head at
once and each later request with `HTTP/1.1 200 OK
Content-Length: 2

ok`;
`big.bin` is 1048577 bytes of `a`. Every run used
`-D h -o out -w "%{http_code} %{size_request} %{size_upload} %{size_header} %{num_connects}"`.

| Command / server reply | Result |
| --- | --- |
| `--data-binary @big.bin`, 417 `Content-Length: 0` | Same connection (`Reusing existing http: connection`). Request 2 is request 1 minus `Expect: 100-continue` (177 then 155 bytes) followed by the body. `-D` = both heads, stdout `ok`. `200 1048909 1048577 92 1`, exit 0. |
| same, 417 with `Content-Length: 4` body `nope` | Body discarded (`Ignoring the response-body`), resend as above; stdout `ok`; `200 1048909 1048577 92 1`. |
| same, 417 then 417 again | No third request; the second 417 is the result, exit 0, stdout empty, both 417 heads in `-D`; `417 1048909 1048577 108 1`. |
| same, 417 with `Connection: close` | No resend at all (`we are done reading and this is set to close, stop send`); `417 177 0 73 1`, exit 0. |
| `-f`, 417 | No resend; exit 22 `The requested URL returned error: 417`; `417 177 0 54 1`. |
| `--fail-with-body`, 417 then 200 / 417 then 417 | Resends; `200 1048909 92 1` exit 0 / `417 1048909 108 1` exit 22. |
| `-d hi -H "Expect: 100-continue"`, 417 then 200 (`--trace-ascii`) | Resend sends the same 171-byte head, `Expect: 100-continue` line included, then `hi` at once with no wait; `200 344 2 92 1`. With 417 twice: still one resend, `417 344 2 108 1`. |
| `-T big.bin` / `cat big.bin \| curl -T -` | Both resend without `Expect` on the same connection and send the body (stdin included, it was never read); `200 1048809 1048577 92 1` / `200 1049002 1048764 92 1`. |

One early run of the plain case logged `Got HTTP failure 417 while sending data` and resent on a
new connection (`num_connects` 2) - a start-up race where the wait was no longer running; three
reruns all took the `while waiting for a 100` path above. That path is filed as BL-316.

### What was built

- `HttpRequestFraming.WithoutExpect()`: same framing, no own `Expect`, no wait.
- `HttpRequestHeadFormatter.Format` takes an optional `framing`, so the resend's head follows the
  plan rather than re-deciding from options (Format's complexity kept at 10 by extracting
  `FramingOf` and `AppendRequestLine`).
- `HttpProtocolHandler`: the one-shot authentication retry became a general retry plan
  (`HttpAttemptOutcome.Retry`). `RetryOf` gives the auth retry or, via `RetriesWithoutExpect`, the
  resend without `Expect`: a 417 that arrived during the wait (body left unsent - `SendBodyAsync`
  now says so), not under `-f`, on a connection the 417 leaves open. Retries on an open
  connection loop until none is asked for; each kind happens at most once, so the loop ends.
- Tests: `HttpProtocolHandlerTests.ExpectationFailed.cs`, seven cases, each at 1-byte and 64 KiB reads.

### Choices (sensible defaults, no ADR needed: every behaviour pinned is measured)

- A 417 that arrives after the wait ran out is left as the result for now; curl's resend on a new
  connection there is filed as BL-316 rather than widening this task.
- A stream body (`-T -`) is resent too: the 417 arrived before any of it was read, as measured.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. A 417 during the 100-continue wait resends the request once without Expect on the same connection, as curl 8.21.0 does
