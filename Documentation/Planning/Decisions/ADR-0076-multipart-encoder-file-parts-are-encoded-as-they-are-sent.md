# ADR-0076 — Multipart `;encoder=` file parts are encoded as they are sent

- **Status:** Accepted
- **Date:** 2026-09-27
- **Supersedes:** the file-part decision of ADR-0041
- **Superseded in part by:** ADR-0093 (the unseekable `7bit` file part)

Decided by Claude under Stewart's delegation (task BL-301, 2026-09-27).

## Context

ADR-0041 read a file part under `;encoder=base64`, `quoted-printable` or `7bit` whole while
the body was built, then sent its encoded bytes from memory, so a large file was held in
memory once. curl 8.21.0 (`lib/mime.c`, `encoder_base64_read`, `encoder_qp_read`,
`encoder_7bit_read`) encodes while it reads.

Two things stood in the way of simply streaming every encoder:

- A `7bit` byte above 127 met while sending reaches `HttpRequestBodyWriter` in
  `Curl.Protocol.Http.UnitLibrary` as a failed read, which it takes as the end of the body:
  a body of known length then fails with `client mime read EOF fail`, not curl's
  `read error getting mime data`, and a chunked one (a file that cannot seek) is sent short
  with no error at all.
- A seekable body has to seek back for a 307 or 308 resend (`RedirectFollower`), and
  `ConcatenatedReadStream` measures and places every segment to do it.

## Decision

- `EncodedReadStream` encodes a part's data as it is read, a buffer at a time, through a
  `MultipartDataEncoding` (`PassThroughDataEncoding`, `Base64DataEncoding`,
  `QuotedPrintableDataEncoding`). Its bytes are those `MultipartPartEncoder.Encode` gives for
  the whole data, however the source splits its reads: base64 encodes whole 57-byte lines and
  keeps the rest, quoted-printable keeps the last four bytes, which its rules look ahead at.
- It seeks when its source seeks, by starting the encoding again from where the source stood
  and reading forward. `Length` is the encoded size when it is known beforehand (as
  ADR-0041's table says curl knows it) and is otherwise measured by encoding once without
  keeping anything; the body's `Content-Length` is unchanged by this and stays unknown for
  quoted-printable, as curl's does.
- `base64` and `quoted-printable` file parts are never read while building. A failed read of
  one surfaces while sending, as it does for a file sent as it is.
- A `7bit` file that can seek is read through once while building, keeping nothing, then
  rewound and streamed; a byte above 127 or a failed read still fails the body before a
  connection is made, with exit 26 and `read error getting mime data`, and the refusal still
  waits for every later part. A read that meets such a byte while sending (the file changed
  in between) throws `MultipartDataRefusedException`, an `IOException`.
- A `7bit` file that cannot seek is still read whole, because it cannot be checked and then
  read again, and streaming it would send a short chunked body with no error. Streaming it is
  follow-up work that needs the HTTP body writer to report a failed read as a failure.

## Consequences

- Memory stays flat for `base64` and `quoted-printable` files of any size, and for seekable
  `7bit` files, which are read twice from disk instead.
- Bytes, `Content-Length`, framing, exit codes and messages are unchanged for every case
  ADR-0041 measured.
- A `base64` or `quoted-printable` file that fails a read now fails while sending, with the HTTP
  body writer's message, rather than with exit 26 before a connection is made; the test that
  pinned the old behaviour now pins it for `7bit`, which still checks while building.
- A 307 or 308 resend of a body holding an encoded file encodes that file again from its start;
  `ConcatenatedReadStream` no longer asks a segment it places at its start for its length, so a
  rewind does not measure a quoted-printable part.
- A `base64` or `quoted-printable` file that cannot seek (a pipe) makes the body unable to seek,
  where ADR-0041's in-memory copy could, so a 307 or 308 resend cannot rewind it, exactly as for
  a pipe sent with no encoder. curl 8.21.0 fails a rewind it cannot make with exit 65; matching
  that for every unseekable body is outside this decision and left to the redirect code.

## Alternatives considered

- **Stream `7bit` too and let the send fail.** Loses curl's message for a seekable file and
  sends a short body silently for an unseekable one until `Curl.Protocol.Http.UnitLibrary`
  reports a failed read as a failure; that project is outside this task.
- **Pre-read every encoded file while building to check it can be read.** Keeps read failures
  before the connection, but reads every file twice, and contradicts the goal that a
  `base64` part is not read before the body is.
- **Buffer the encoded output of a seek-back for 307/308.** Would hold the file in memory
  again; encoding again from the start keeps memory flat.
