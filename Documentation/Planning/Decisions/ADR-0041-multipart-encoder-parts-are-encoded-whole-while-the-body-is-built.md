# ADR-0041 — Multipart `;encoder=` parts are encoded whole while the body is built

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (task BL-274, 2026-09-26).

## Context

`-F 'name=value;encoder=<name>'` and `-F 'name=@file;encoder=<name>'` ask curl to send a
part's body through one of libcurl 8.21.0's five transfer encoders (`lib/mime.c`,
`encoders[]`). Measured against curl 8.21.0 (`/mingw64/bin/curl`, Schannel) on 2026-09-26 with
`Record-CurlExchange.ps1`:

| Encoder | Body sent | Size known beforehand |
| --- | --- | --- |
| `binary`, `8bit` | the bytes as they are | yes |
| `7bit` | the bytes as they are; a byte above 127 fails the transfer with exit 26, `read error getting mime data`, once curl reaches it while sending | yes |
| `base64` | 76-column lines separated by CRLF, no CRLF after the last | yes |
| `quoted-printable` | `encoder_qp_read`'s rules, 76-column lines with `=` CRLF soft breaks | no, unless the data is empty: the request goes chunked |

Every encoded part gets `Content-Transfer-Encoding: <name>` (lower case whatever case was
typed) after its `Content-Type`, unless its own `;headers=` give one, which then replaces it
while the body is still encoded. An unknown name, or an empty one, fails before anything is
sent with exit 43, `A libcurl function was given a bad argument`; parts fail in command-line
order, so an unopenable file before it gives exit 26 instead.

`MultipartFormBodyBuilder` (ADR-0027) builds the whole body before a connection is made and
streams file parts.

## Decision

- `MultipartFormPart.Encoder` carries the `;encoder=` value unchecked; the builder resolves it
  with `MultipartPartEncoder.Find` and fails the build with exit 43 when curl has no such
  encoder.
- `binary` and `8bit` file parts are streamed as before. A file part under `7bit`, `base64` or
  `quoted-printable` is read whole while building and its encoded bytes are sent from memory;
  a read failure is exit 26, `read error getting mime data`.
- The encoded size is the body's `Content-Length` exactly when curl knows it beforehand (table
  above); otherwise the body's length is unknown and it goes chunked, as curl's does. A file
  that cannot seek leaves the length unknown whatever the encoder.
- A `7bit` refusal is found while building but reported only after every other part has been
  built, so a later unopenable file or unknown encoder wins as it does in curl, which meets
  7-bit data only while sending.

## Consequences

- Bytes, `Content-Length`, framing, exit codes and messages match curl for every measured case.
- Unlike curl, the `7bit` failure happens before a connection is made, so the server never sees
  the partial request curl sends; stdout, stderr and the exit code are the same.
- A large file under `base64`, `quoted-printable` or `7bit` is held in memory, encoded, from
  building until it is sent. Streaming those encoders is BL-299.
- An encoder on a `Multipart` part is ignored: the `-F` syntax never gives a nested multipart
  one.

## Alternatives considered

- **Encode while streaming, as curl does.** Keeps memory flat for large files, but needs a
  streaming base64 and quoted-printable encoder with look-ahead, and a `7bit` refusal met while
  sending reaches the HTTP body writer as a read failure it reports as `client mime read EOF
  fail`, not curl's message. Left for BL-299.
- **Pre-scan a `7bit` file, then stream it.** Correct for a seekable file but not for a pipe,
  and it still leaves `base64` and `quoted-printable` to solve.
