# ADR-0573 — Write, read and header callbacks: pause, abort and the *DATA pointers

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2415.

## Context

The managed engine already handed body bytes to `EasyCallbackOptions.Write`, header lines to `Header`
and the upload to `Read` (BL-2316, BL-2359), but it treated the callbacks' magic returns as plain
counts: `CURL_WRITEFUNC_PAUSE` failed the transfer as a short write, and `CURL_READFUNC_ABORT` and
`CURL_READFUNC_PAUSE` as `read function returned funny value`. `Libcurl` had no `SetOption` rows for
`CURLOPT_WRITEFUNCTION`, `READFUNCTION`, `HEADERFUNCTION` or their `*DATA` pointers, and the callback
tasks (BL-2415 to BL-2422) each name their own `Libcurl/Callbacks/<Area>` folder, while
`EasyOptionSetters` has one `AddCallbackSetters` partial method only one part can implement.

Upstream curl 8.22.0 (`lib/cw-out.c`, `lib/sendf.c`): a write or header callback that returns
`CURL_WRITEFUNC_PAUSE` keeps the chunk and pauses receiving until `curl_easy_pause` clears it, then
hands the same chunk over again; a read callback's `CURL_READFUNC_PAUSE` pauses sending the same way,
and its `CURL_READFUNC_ABORT` fails with `CURLE_ABORTED_BY_CALLBACK` and `operation aborted by
callback`. A protocol without a network (`file://`, `PROTOPT_NONETWORK`) cannot pause: the write
answers `CURLE_WRITE_ERROR` (`Write callback asked for PAUSE when not supported`), the read
`CURLE_READ_ERROR` (`Read callback asked for PAUSE when not supported`). `CURL_WRITEFUNC_ERROR`
(`0xFFFFFFFF`) is a short write.

## Decision

1. Each `ManagedEasyTransfer` owns an `EasyPauseGate`, the handle's `CurlPause` bits. `Pause` sets them;
   a write or header callback's pause sets `Receive`, a read callback's sets `Send`, and the write or
   read waits asynchronously, under the transfer's token, until that bit is clear, then calls the
   callback again with the same chunk or buffer. Another thread unpauses with `Pause`, as an
   application calls `curl_easy_pause`. `PausedDirections` reads the gate.
2. A `file` URL gets no gate, and the synchronous `Write` and `Read` paths cannot wait, so a pause there
   fails with libcurl's pause-not-supported text (`CallbackWriteStream.PauseNotSupportedText`,
   `CallbackReadStream.PauseNotSupportedText`) and code.
3. A read callback's `CURL_READFUNC_ABORT` marks the `CallbackReadStream` `Aborted` and fails the read;
   the handle then answers `CurlCode.AbortedByCallback` with `operation aborted by callback`
   (`CallbackReadStream.AbortedText`), as it answers a progress callback's abort with `Callback aborted`.
4. `Libcurl/Options/Callbacks` implements `AddCallbackSetters` once, calling one partial method per
   callback area - `ReadWrite`, `Progress`, `Seek`, `Sockets`, `Phases`, `Wildcard`, `Hsts`, `Keys` -
   which each area implements in an `EasyOptionSetters` part in `Libcurl/Callbacks/<Area>`.
5. The write, read and header area lives in `Libcurl/Callbacks/ReadWrite`, not the `Data` folder BL-2415
   named: the session's `data/` path rule refuses any folder of that name, case-insensitively on Windows.
   `CURLOPT_WRITEFUNCTION`, `READFUNCTION` and `HEADERFUNCTION` take their own delegate type or `null`
   (unset, libcurl's default) and answer `BadFunctionArgument` for any other delegate.
6. The managed callback's closure is its user data, so `CURLOPT_WRITEDATA`, `HEADERDATA` and `READDATA`
   matter only as the `FILE *` of libcurl's default function: a `Stream` installs `StreamDataCallbacks`
   (`fwrite`: the whole chunk, or 0 when the stream throws `IOException`; `fread`) over it unless the
   caller's own function is set, and replaces an earlier stream; any other value, `null` included, is
   taken and changes nothing, so a C-style "set the data, then the function" sequence still works.

## Consequences

- An application can pause and resume a transfer from a callback and another thread, and abort an upload
  from its read callback, with libcurl's codes and texts.
- The other callback tasks add their rows without editing a shared file.
- `CURLOPT_WRITEDATA` with no function cannot reach standard output: with neither set the engine discards
  the body, and writing to standard output by default stays `Libcurl`'s job for a later task.
