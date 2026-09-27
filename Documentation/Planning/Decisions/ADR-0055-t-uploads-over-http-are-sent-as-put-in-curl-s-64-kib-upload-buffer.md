# ADR-0055 — `-T` uploads over HTTP are sent as PUT, read in curl's 64 KiB upload buffer

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-184, 2026-09-27).

## Context

BL-030 hands the opened `-T` file, or standard input for `-T -`, to the HTTP handler as
`ITransferContext.Upload`. The handler ignored it. BL-184 had to decide how it is framed, how
it is read, and what a failed read prints.

Measured on 2026-09-27 with the Windows reference build (`/mingw64/bin/curl`, curl 8.21.0,
Schannel) against a loopback server that read the whole request and answered `200` with an
empty body:

| Command | Request curl sent | Exit and message |
| --- | --- | --- |
| `-T f.txt` (`hello`) | `PUT`, `Content-Length: 5`, no `Content-Type`, no `Expect`, body `hello` | 0 |
| `printf hello \| curl -T -` | `PUT`, `Transfer-Encoding: chunked`, `Expect: 100-continue`; after the 1 s wait `5\r\nhello\r\n0\r\n\r\n` | 0 |
| 200000 bytes on stdin, `-T -` | the head alone, then chunks 65524, 65524, 65524, 3428 | 0 |
| the same with `-H "Expect:"` (108-byte head) | chunks 65416, 65524, 65524, 3536 | 0 |
| `-T big.bin`, 100000 bytes, all locked | nothing at all | 26 `client read function EOF fail, only 0/100000 of needed bytes read` |
| the same, locked from byte 70000 (104-byte head) | the head and 65432 bytes | 26 `... only 65432/100000 ...` |
| 200000 bytes locked from 140000 | the head and 130968 bytes | 26 `... only 130968/200000 ...` |
| `-T big2.bin`, 2000000 bytes | `Content-Length: 2000000`, `Expect: 100-continue` | 0 |

## Decision

1. `HttpRequestFraming.Of` takes the upload. It is a PUT unless `-X` names another method, with
   no `Content-Type`. A stream that can seek has the length left from its position and is sent
   with `Content-Length`; one that cannot (standard input) has an unknown length and is sent
   chunked. Chunking, `Expect: 100-continue`, the 1-second wait, the 417 retry and the HTTP/1.0
   refusal are the ones `-d` and `-F` bodies already get (ADR-0036).
2. An upload takes the place of any `-d`/`-F` body. curl's command line refuses the two
   together, so the handler never has to choose between them in a real run.
3. `HttpRequestBodyWriter` reads a stream body as curl does, into a 65536-byte buffer. The
   first read shares the buffer with the request head unless the head went out alone to wait
   for `100 Continue`; a chunked read keeps 12 bytes of the buffer back for the chunk framing.
   This is what makes the read count in the exit 26 message and the chunk sizes match. It
   applies to `-F` stream bodies too, which curl reads through the same buffer.
4. A short read of an upload of known length fails with exit 26 and
   `client read function EOF fail, only N/M of needed bytes read`; a `-F` body keeps
   `client mime read EOF fail`.

## Consequences

- When the very first read fails, curl has sent nothing, because the head waits in the same
  buffer; the handler has already written the head. The exit code and message match; only the
  bytes a server sees differ.
- A head of 64 KiB or more leaves no room in the first buffer; the handler then makes the first
  read a whole one. That case was not measured.
- `-C`/`--continue-at` with `-T` over HTTP is not handled here: the upload is sent from the
  stream's current position.
