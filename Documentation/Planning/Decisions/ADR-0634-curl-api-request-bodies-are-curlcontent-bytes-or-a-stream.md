# ADR-0634 — `Curl.Api` request bodies are `CurlContent`: bytes or a stream

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2500.

## Context

ADR-0632 typed `CurlRequest.Body` as `ReadOnlyMemory<byte>?`. BL-2500 asks for a body that is also
a `Stream`, seekable or not, of known length or chunked, uploaded through the seam's read callback
and rewound through its seek callback. The shape, the length rule, and how a stream's failures reach
the caller were open.

## Decision

- `CurlRequest.Body` is `CurlContent?`, an abstract class with two internal kinds:
  `CurlContent.FromBytes(ReadOnlyMemory<byte>)` (also an implicit conversion from `byte[]` and
  `ReadOnlyMemory<byte>`, so ADR-0632's `Body = new byte[] { … }` still compiles) and
  `CurlContent.FromStream(Stream, long? length = null)`. `CurlContent.Empty` is zero bytes. The
  kinds are not open for subclassing (`private protected` constructor): every body must map onto
  libcurl's options.
- Bytes stay `CURLOPT_POSTFIELDS` with `CURLOPT_POSTFIELDSIZE`, as in ADR-0632.
- A stream sets `CURLOPT_POST` with no post fields, so libcurl reads the body through
  `CURLOPT_READFUNCTION`, and sets `CURLOPT_SEEKFUNCTION`. The size is the given `length`, else a
  seekable stream's `Length - Position` at send time, else -1: libcurl then sends it chunked over
  HTTP/1.1 (and without a length over HTTP/2 and HTTP/3). `Expect: 100-continue` is the engine's.
  A method other than `POST` keeps that method through `CURLOPT_CUSTOMREQUEST`, as for bytes.
- The upload starts at the stream's position when the request is sent; the seek callback counts
  `SEEK_SET` offsets from there, so a redirect or authentication retry resends the same bytes. A
  non-seekable stream answers `CURL_SEEKFUNC_CANTSEEK`, which lets libcurl fall back as it does for
  the tool's piped stdin, and otherwise fail with `CURLE_SEND_FAIL_REWIND`.
- No exception crosses into the engine: a read that throws returns `CURL_READFUNC_ABORT`, so the
  transfer ends with `CURLE_ABORTED_BY_CALLBACK` and `SendAsync` throws `CurlException` with that
  code; a seek that throws returns `CURL_SEEKFUNC_FAIL`. The stream is never disposed by `Curl.Api`.
- The read callback reads the stream synchronously (`Stream.Read(Span<byte>)`): the seam's
  `ReadCallback` is synchronous, as libcurl's is. An asynchronous source can be wrapped by the caller
  or paused with `CURL_READFUNC_PAUSE` once the pause seam is used by `Curl.Api`.

## Consequences

- Callers who assigned bytes are unaffected; code that read `Body.Value` reads `CurlContent` now.
- The original exception of a failed read is not carried on the `CurlException`; doing so needs an
  inner exception on `CurlException`, left for when a task asks for it.
