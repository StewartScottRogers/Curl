# ADR-0507 — libcurl's callbacks are typed delegates in `Curl.Api.Callbacks`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2301.

## Context

ADR-0506 says libcurl's callbacks become delegates; the plan's `EasyCallbackOptions` row lists
the 22 `CURLOPT_*FUNCTION` options. Each upstream callback takes C pointers, sizes and a
`void *` user pointer, and several return magic numbers. The shared core needs one .NET shape
for each that both `Libcurl` and `Curl.Api` can use and that a native engine could implement.

## Decision

- One delegate per callback in `Curl.Api.UnitLibrary/Callbacks`, held by `EasyCallbackOptions`,
  every property `null` by default. The `*DATA` user pointer is the delegate's closure.
- Buffers are spans: `ReadOnlySpan<byte>` for write, header, debug, interleave and SSH host key
  data, `Span<byte>` for the read buffer.
- Callbacks that return a byte count (`size_t` upstream: write, read, header, interleave)
  return `long`; their magic values are `long` constants with upstream's numbers in
  `WriteCallbackResult` (`CURL_WRITEFUNC_PAUSE` `0x10000001`, `CURL_WRITEFUNC_ERROR`
  `0xFFFFFFFF`) and `ReadCallbackResult` (`CURL_READFUNC_ABORT` `0x10000000`, `PAUSE`
  `0x10000001`). `long` holds every one without the platform-width `nuint`.
- Progress and xferinfo return `int` (0 carries on, non-zero aborts) with
  `ProgressCallbackResult.Continue` (`CURL_PROGRESSFUNC_CONTINUE`, `0x10000001`). Callbacks
  whose return is a closed set return an enum with upstream's numbers (`SeekResult`,
  `SocketOptionResult`, `PrerequestResult`, `TrailerResult`, `ChunkBeginResult`,
  `ChunkEndResult`, `FileNameMatchResult`, `HstsResult`, `SshKeyStatus`, `SshKeyMatch`);
  the SSL context callback returns `CurlCode`.
- Sockets are the opaque `CurlSocket(nint Handle)` with `CurlSocket.Bad` = -1
  (`CURL_SOCKET_BAD`); opaque library pointers (the resolver state, the TLS context) are `nint`.
  Upstream structs become records: `CurlSocketAddress`, `HstsEntry`, `SshKey`, `CurlFileInfo`.
- The seek origin is `System.IO.SeekOrigin`, whose numbers are `SEEK_SET`/`CUR`/`END`'s.
- The HSTS read callback hands its entry back through an `out HstsEntry?` rather than filling
  a caller's buffer; the HSTS write callback takes `curl_index`'s two fields as `index` and
  `total`. The trailer callback appends to an `IList<string>` (upstream's `curl_slist **`).
- `CurlInfoType` has no member for `CURLINFO_END`: it only marks the end of the list and is
  never passed to a debug callback.

## Consequences

The engine task that wires callbacks (plan phase "Callbacks wired through the engine") converts
between these shapes and its transfer loop; `Libcurl`'s native-looking surface can wrap them
one to one. `SSL_CTX_FUNCTION` is carried so setting it can answer `CURLE_NOT_BUILT_IN`, as
curl's Schannel build does.
