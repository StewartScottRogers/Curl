# ADR-0524: A chunked resend of stdin from a pipe to an HTTP/1.0 server fails with exit 65 off Windows

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2337 (gap finding GF-0082)
- Amends: BL-2130 and BL-2037 (exit 25 for upstream test1072 and test1073)
- Decided by Claude under Stewart's delegation.

## Context

A `-T -` upload is chunked, since stdin has no known size. When an HTTP/1.0 server answers it
with a 401 under `--anyauth` (upstream test1072) or a 301 under `-L` (test1073), curl must send
the body again, and an HTTP/1.0 request cannot carry chunks. Curl failed every such resend with
exit 25, `Chunky upload is not supported by HTTP 1.0`, after reconnecting.

The gap analysis found the Linux and macOS reference exits 65 there. Measured with curl 8.18.0
(OpenSSL) on Linux:

- stdin as a pipe: `* seek callback returned error 2`, `* rewind of client reader 'cr-in' failed: 65`,
  `curl: (65) seek callback returned error 2`, with no new connection, for both the 401 and the 301.
- stdin as a redirected file: exit 25, as upstream's test suite expects, since its harness feeds
  `<stdin>` from a file.

Before curl builds the resend, it rewinds the upload through the tool's seek callback, which
calls `lseek` on stdin. `lseek` fails on a pipe on Linux and macOS, so the rewind fails first. On
Windows the C runtime's `lseek` on a pipe does not fail, so the Schannel build goes on to exit 25,
and Curl already matched it there.

## Decision

- `Curl.Original.Protocol.Http` gains `IRewindableUpload`: an upload stream that cannot seek says whether
  curl could still rewind it (`CanRewind`). A stream that does not implement it counts as one that can.
- `HttpProtocolHandler` fails a chunked resend to an HTTP/1.0 server (`IsChunkedResendToHttp10`)
  whose body is a stream that cannot be rewound before it connects, through the existing
  `UploadRewindFailed`: exit 65 with the two `-v` lines. Otherwise exit 25 holds.
- `Curl.Console`'s `StandardInputUploadStream` implements it. `CanRewind` is true when the
  wrapped stream can seek, or on Windows (`SeekOfUnseekableInputSucceeds`).

## Consequences

- Off Windows, `-T -` from a pipe exits 65 for test1072 and test1073, as the reference does. On
  Windows, and wherever the stdin stream can seek (the in-process upstream cases), it stays 25.
- Two known differences remain:
  - Process stdin on Unix is a `UnixConsoleStream`, which never seeks, so a file redirected into
    Curl off Windows still gets 65 where curl gets 25.
  - Curl writes `Issue another request to this URL` before the two rewind lines, while curl
    writes it after them.
