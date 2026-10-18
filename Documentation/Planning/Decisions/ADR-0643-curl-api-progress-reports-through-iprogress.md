# ADR-0643 — `Curl.Api` reports transfer progress through `IProgress<CurlTransferProgress>`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2505.

## Context

libcurl reports a transfer's progress through `CURLOPT_XFERINFOFUNCTION`: download and upload
totals and counts, nothing else. `Curl.Api` already sets that callback for cancellation
(ADR-0639). A .NET caller expects progress as `IProgress<T>`, with the speed and a sense of which
way the bytes are moving, and must not be able to wedge or crash the engine with a failing report.

## Decision

- `CurlRequest.Progress` (`IProgress<CurlTransferProgress>?`, default `null`) takes one
  `CurlTransferProgress` per `CURLOPT_XFERINFOFUNCTION` call: `DownloadTotal`, `DownloadNow`,
  `UploadTotal`, `UploadNow` as libcurl gives them (a total of 0 means unknown), `DownloadSpeed` and
  `UploadSpeed` from the handle's `CURLINFO_SPEED_*_T` (the engine's own average, not one computed
  here, so no clock is needed), and `Phase`.
- `Phase` is read from the byte counts alone: `Downloading` once the response body has a size or a
  byte, else `Uploading` once a request body byte is sent, else `Connecting`. A finer phase (name
  lookup, TLS handshake, waiting for the response) would need the timing infos on every call and
  is not what libcurl's callback reports.
- `TransferProgressReporter` (`Curl.Api/Progress`) is the callback: it reports, then answers what the
  cancellation's callback answers, so cancellation keeps working with progress on.
- The report is made on the thread the engine calls from; the caller's `IProgress<T>` decides the
  marshalling (`Progress<T>` posts to its synchronization context).
- A report that throws makes the callback answer abort, then and on every later call; the transfer
  ends with `CURLE_ABORTED_BY_CALLBACK`, thrown as `CurlException`. The caller's exception is not
  re-thrown into the engine, which, like libcurl, must never see one.
- Reports stop once the transfer ends: `ResponseBodyStream` marks the reporter ended when perform
  returns, so a late engine call makes no report.

## Consequences

- No new clock or timer: the engine's speeds are used as they are.
- `SendWithBodyCapacityAsync` no longer awaits inside a `catch` (the compiler's rethrow branch for a
  non-`Exception` object can never be covered): a set-up failure is captured with
  `ExceptionDispatchInfo` and rethrown after the handles are disposed.
