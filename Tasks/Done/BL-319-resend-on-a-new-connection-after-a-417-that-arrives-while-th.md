---
id: BL-319
title: Resend on a new connection after a 417 that arrives while the body is being sent
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-260]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-319 — Resend on a new connection after a 417 that arrives while the body is being sent

## Goal

A `417 Expectation Failed` that arrives after the one-second `100 Continue` wait ran out, while or after the body is sent, makes the handler close the connection and resend the request without `Expect` on a new connection, as curl 8.21.0 does.

## Context

- BL-260 resends only after a 417 that arrives during the wait, with the body unsent (`HttpProtocolHandler.RetriesWithoutExpect`, `bodyLeftUnsent`).
- In one racy BL-260 run curl 8.21.0 logged `Got HTTP failure 417 while sending data`, shut the connection down and resent on a second connection (`%{num_connects}` 2). In curl's source this path also rewinds the body (`http_perhapsrewind`), so a body that cannot be rewound (stdin) likely fails instead: measure it.
- Measure first with a loopback server that answers 417 only after reading some body bytes: the connections used, stdout, `-D`, `%{size_request}`, `%{size_upload}`, and what `-T -` does.

## Acceptance criteria

- [x] The case is measured on curl 8.21.0 and the commands and bytes are in Notes.
- [x] A handler test replays a 417 that arrives after the wait through the fakes (1-byte reads too) and matches the measured requests, connections and output.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*` reports no failing member.

## Notes

### Measured on curl 8.21.0 (Schannel, mingw64), 2026-09-27

Loopback Python server on 127.0.0.1 that reads the `Expect: 100-continue` head, sends nothing
until at least 1000 body bytes have arrived (curl sends them once its 1 s wait runs out), then
answers `HTTP/1.1 417 Expectation Failed` / `Content-Length: 0` and drains until curl stops;
any later request is read to its end (Content-Length or chunked) and answered
`HTTP/1.1 200 OK` / `Content-Length: 2` / `ok`. `big.bin` is 1048577 bytes of `a`; `seq.bin`
is 1048576 bytes of numbered 8-byte lines, so a resend's first bytes show where it restarted.
Every run used `-D h -o out -w "%{http_code} %{size_request} %{size_upload} %{size_header} %{num_connects}"`.

| Command / server reply | Result |
| --- | --- |
| `--data-binary @big.bin` | `* Done waiting for 100-continue`, `* Got HTTP failure 417 while sending data`, `* Need to rewind upload for next request`, `* abort upload after having sent 65536 bytes`, `* shutting down connection #0`, `* Issue another request to this URL`; new connection, 155-byte head without `Expect`, whole body. `-D` = 417 head + 200 head, stdout `ok`, `200 1114445 1048577 92 2`, exit 0. (177 + 65536 + 155 + 1048577 = 1114445.) Reruns stopped after 131072 bytes (`200 1179981 1048577 92 2`): how much goes before the 417 is seen is timing. |
| `-T big.bin` | Same, `PUT`, heads 127 then 105 bytes, the file sent whole again: `200 1310953 1048577 92 2` (stopped after 262144). |
| `--fail-with-body --data-binary @big.bin` | Same as the first row: resends, `200 1179981 1048577 92 2`, exit 0. |
| `-f --data-binary @big.bin` / `-f -T big.bin` | Sending stops, no resend: exit 22 `The requested URL returned error: 417`, `417 131249 131072 54 1` / `417 131199 131072 54 1`. |
| `--data-binary @big.bin`, 417 with `Connection: close` | `* we are done reading and this is set to close, stop send`, `* abort upload after having sent 65536 bytes`, no resend: exit 0, `417 65713 65536 73 1`. |
| `--data-binary @big.bin`, 417 only after all 1048577 body bytes | `* upload completely sent off`, no resend: exit 0, `417 1048754 1048577 54 1`. |
| `cat seq.bin \| curl -T -` | Chunked, `Expect`. `* abort upload after having sent 65532 bytes` (one 65524-byte chunk), rewind "needed" but stdin cannot seek and curl does not fail: the resend (108-byte head, chunked, no `Expect`) goes on from stdin byte 65524, so the server gets 983052 more bytes, none twice and none lost. `200 1048995 983225 92 2`, exit 0. Another run stopped mid-chunk after 196626 bytes and resent from exactly the first byte not on the wire. |
| `-T big.bin -H "Expect: 100-continue"`, every `Expect` request answered 417 mid-body | The custom line goes on every resend, each waits for `100 Continue` again and draws 417 again: 51 connections, `* Maximum (50) redirects followed`, exit 47, `417 7543117 131072 2754 51`. Not built here: filed as BL-389. |

### What was built

- `HttpContinueWaitConnection` keeps the first status code the wait's read saw, and
  `SendUnlessExpectationFailedAsync` races one write against that read: a 417 that arrives
  first cancels the write and reports the piece unsent.
- `HttpRequestBodyWriter.ExpectationWatch` (set when the request waited for `100 Continue`)
  sends every piece that way - a `-d` body cut into curl's 64 KiB buffer pieces, a chunk sent
  as one write with its framing - and stops at the first piece a 417 beats (`CutShort`, no
  last chunk). `Rewound` gives the resend's body: the same bytes, a seekable stream seeked back
  to where the writer began, or a stream that cannot seek wrapped in `HttpPrefixedStream` so the
  cut-short piece goes first and stdin goes on from there.
- `HttpProtocolHandler`: a cut-short body makes the connection not keep alive, so the resend
  without `Expect` (`RetriesWithoutExpect`, now for a body left unsent or cut short) goes on a new
  connection through the existing reconnect path; `%{size_request}`, `%{size_header}` and
  `%{num_connects}` add up across both, `%{size_upload}` is the resend's.
- Tests: `HttpProtocolHandlerTests.ExpectationFailedWhileSending.cs` (seven cases, 1-byte and
  64 KiB reads), new `HttpContinueWaitConnectionTests` and `HttpPrefixedStreamTests`;
  `GatedConnection.StallsWritesOnceReleased` plays a server that answered early and stopped
  reading. The same binary against the loopback server resent on a second connection for
  `-d`, `-T file` and `-T -` (stdin contiguous across the two connections).

### Choices (sensible defaults; every pinned behaviour is measured, so no ADR)

- Sending stops at the first piece the 417 beats, so the tests pin one 65536-byte piece; curl
  sends one to four pieces depending on timing, and the bytes that decide the result (heads,
  resend, exit code, connection count) do not depend on it.
- Only a 417 stops the sending. curl also stops for any status of 300 or above mid-upload;
  that is filed as BL-388 rather than widening this task.
- A cancelled socket write that already put some bytes on the wire would have those bytes sent
  again ahead of stdin, since how much of a cancelled write went out is unknowable; the
  loopback runs showed no such overlap.
- `%{size_upload}` for a chunked body stays the data bytes, as before this task; curl counts
  chunk framing there too (983225 above), which was already so for every chunked upload.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A 417 that arrives while the body is being sent stops the upload and resends without Expect on a new connection, rewinding the body, as curl 8.21.0 does
