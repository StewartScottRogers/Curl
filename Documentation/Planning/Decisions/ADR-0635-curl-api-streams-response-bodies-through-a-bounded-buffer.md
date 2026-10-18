# ADR-0635 — `Curl.Api` streams response bodies through a bounded buffer paused with `curl_easy_pause`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2501.

## Context

ADR-0632 left `CurlResponse.Body` as `Stream.Null` and `SendAsync` waiting for the whole transfer.
BL-2501 makes the body a readable `Stream` fed by the engine's write callback, with back-pressure
and an abort on early disposal. The seam gives a synchronous `WriteCallback`, the
`CURL_WRITEFUNC_PAUSE` answer and `IEasyTransfer.Pause`; the rules forbid blocking a thread
(`.Result`, `.Wait()`), so the callback cannot wait for the reader.

## Decision

- `ResponseBodyBuffer` (`Curl.Api/ResponseBodies`) is the write callback. It copies each delivery
  into a queue until it holds 64 KiB (`ResponseBodyStream.DefaultCapacity`), then answers
  `CURL_WRITEFUNC_PAUSE`, so the engine keeps the data and pauses receiving, as libcurl does. One
  delivery larger than the capacity is still taken whole into an empty buffer, so a large chunk can
  never stall the transfer. Once a paused buffer is read down to half its capacity, the reader calls
  `Pause(CurlPause.Continue)` and the engine hands the held data again. A hand-rolled lock and
  `TaskCompletionSource` is used rather than `System.Threading.Channels` or `System.IO.Pipelines`:
  the callback must answer synchronously whether it took the bytes, which a pipe's writer does not
  say, and `System.IO.Pipelines` is not in the base shared framework.
- `ResponseBodyStream` is the body: read-only, forward-only, asynchronous only. Synchronous `Read`
  throws `NotSupportedException` (read with `ReadAsync` or `CopyToAsync`): it would have to block a
  thread on the transfer. It owns the easy handle and disposes it when the transfer ends. Disposing
  the stream before the end makes every later write answer `CURL_WRITEFUNC_ERROR` and cancels the
  perform's token, so the transfer aborts whether it is receiving, paused or waiting on the network.
- `SendAsync` returns as soon as the body starts (the first delivery) or the transfer ends, whichever
  comes first. A transfer that has ended with an error by then throws `CurlException` from
  `SendAsync`, with the handle disposed; a later failure is thrown by the body's `ReadAsync`.
  `SendAsync`'s token is linked into the perform's token, so it stops the transfer while the body
  streams too. Cancelling the token given to one `ReadAsync` stops only that read.
- Because the transfer usually still runs when `SendAsync` returns, `StatusCode` and `Version` come
  from the final status line (`ResponseHeaderCollector`), and from `TransferInfo` only when no status
  line came (FTP, file and the rest). `Info` is what the transfer reported when `SendAsync` returned;
  `Trailers` is complete once the body has been read to its end.

## Consequences

- The managed engine's pause gate has a window in which an unpause from the reader's thread arrives
  before the engine records the callback's pause and is lost; BL-2518 closes it in
  `Curl.Engine.UnitLibrary`.
- BL-2504 (cancellation) builds on the linked token; BL-2510's `CurlHttpMessageHandler` can hand the
  body stream to `HttpResponseMessage.Content` as is.
