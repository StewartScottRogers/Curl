# ADR-0093 — A refused multipart byte fails the send with exit 26

- **Status:** Accepted
- **Date:** 2026-09-27
- **Supersedes:** the unseekable `7bit` file-part decision of ADR-0076

Decided by Claude under Stewart's delegation (task BL-385, 2026-09-27).

## Context

ADR-0076 streams every encoded multipart file part except a `7bit` file that cannot seek (a
pipe), which `MultipartFormBodyBuilder` read whole: it cannot be checked while building and
then read again, and `HttpRequestBodyWriter` took every failed read as the end of the body, so
streaming it would have sent a short chunked body with no error.

Measured (BL-385 Notes):

- curl 8.21.0 (Windows, Schannel), `-F 'f=@f.bin;encoder=7bit'`, a 200000-byte file with a
  byte above 127 at 150000: exit 26, `curl: (26) read error getting mime data`, after sending
  part of the body.
- curl 8.18.0 (Linux, OpenSSL), the same bytes from a FIFO: the body is chunked, the chunks
  before the byte go out, no closing `0` chunk is sent, then exit 26 with the same message.
  `lib/mime.c` is unchanged between the two for this path (`encoder_7bit_read` returns
  `STOP_FILLING`, the mime reader `READ_ERROR`). The Windows build cannot show the chunked case:
  it gives a named pipe the size 0 and sends a `Content-Length` body.
- A plain failed read of a form file stays curl's end of data: a locked file still fails a
  known-length body with `client mime read EOF fail, only N/M of needed bytes read` (BL-184).

So the sender has to tell a refused byte from any other failed read.

## Decision

- `Curl.Protocol.Abstractions` gains `RequestBodyReadFailedException`, a sealed
  `IOException` carrying curl's message. It is the one failed body read a sender reports as a
  failure; every other `IOException` stays the end of the body.
- `EncodedReadStream` throws it, with `read error getting mime data`, at a byte its encoder
  refuses; `MultipartDataRefusedException` is gone.
- `HttpRequestBodyWriter` turns it into exit 26 with its message. A chunked body is left
  without its closing chunk, as curl leaves it.
- A `7bit` file that cannot seek is streamed through `EncodedReadStream` like every other
  encoded file and never read while building. A seekable `7bit` file is still checked while
  building (ADR-0076), so its refusal still comes before a connection is made.

## Consequences

- Memory stays flat for every encoded file part, pipes included.
- A pipe under `7bit` holding a byte above 127 now fails while sending, after the bytes before
  it, as curl does, instead of before a connection; a pipe that fails a read fails while
  sending too, as any unencoded pipe does.
- The contract grows by one exception type, which any future body source (not only multipart)
  can use to fail a send with curl's message.

## Alternatives considered

- **Match the message text in the HTTP writer.** No new contract, but a behaviour hanging on a
  string is exactly the hidden coupling "say what it does" forbids.
- **Throw `InvalidDataException` (not an `IOException`).** No contract change, but it tells the
  sender nothing about the message or exit code, and readers that catch `IOException` would
  miss it.
- **Treat every failed multipart read as exit 26.** Contradicts the measured locked-file case.
