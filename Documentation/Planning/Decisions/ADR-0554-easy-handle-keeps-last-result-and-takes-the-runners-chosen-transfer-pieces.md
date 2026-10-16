# ADR-0554 — The easy handle keeps its last perform's whole `TransferResult` and takes the runner's already-chosen transfer pieces

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2395.

## Context

ADR-0552 has `Curl.Console`'s runner perform each serial attempt through an easy handle carrying a
prepared `ToolAttempt`, whose whole `TransferResult` the handle keeps as `ToolAttemptResult`. BL-2394
retires that hook: the handle is to build the attempt from its options. Two things stand in the way.
The runner's `--retry` loop, its failure lines and `-w` read the attempt's whole `TransferResult`
(its report, exit code, message and the tool's own fields), of which `TransferInfo` carries only the
libcurl part. And the runner chooses several pieces of each transfer before connecting -
`TrySelectProxyAndCredentials`'s proxy and credentials, `ResolveSshOptionsAsync`'s SSH options,
`--etag-compare`'s `If-None-Match` lines, the parsed `-r` range, the resolved `-C` offset, the `-F`
body, the `-T` source and the transfer's alt-svc cache - which the easy options cannot describe, and it
reads the `-b` files and the `--alt-svc` and `--hsts` files before the transfer and writes the jar and
those files after it.

## Decision

1. `ManagedEasyTransfer` gets an internal `LastResult`: the whole `TransferResult` of the last perform,
   from the options or from a `ToolAttempt` alike, after `TlsVerifyResultRecordingTransferEvents` put the
   verify codes on its report. It is `null` before the first perform, after a perform that failed before
   connecting (no URL set, or one that does not parse) and after one the progress callback aborted. The
   runner reads it after each `PerformAsync` once `ToolAttempt` is gone; `Info` stays the libcurl view.
   A property on the handle, not a new return type, because `IEasyTransfer.PerformAsync` returns a
   `CurlCode` as `curl_easy_perform` does, and `Curl.Api` and `Libcurl` must not change.
2. An internal `ToolInputs` (`EngineToolTransferInputs`: proxy, looked-up credentials, SSH options,
   `If-None-Match` lines, range, resume offset, form body, upload and alt-svc cache) carries the pieces the
   runner chose. While set, a perform from the options passes each to `TransferContextFactory.Create`
   exactly as the runner's own call does, the `null` ones included, in place of what the handle would
   choose itself (`CURLOPT_RESUME_FROM` when positive, the read callback under `CURLOPT_UPLOAD`, its own
   alt-svc cache, and nothing else).
3. While `ToolInputs` is set, the handle does none of its own cookie, alt-svc and HSTS file setup or
   cleanup (ADR-0544): the runner has already loaded the `-b` files, opened the alt-svc cache and made
   the HSTS switch (the URL it sets is the switched one), and writes the files itself after the attempt,
   as it does today. One flag rather than three, because the runner does all three steps in one place and
   a half-and-half split would read or write a file twice. The cookie store the HTTP handler keeps is still
   the engine's shared one; handing the runner's cookie engine to the handle is BL-2394's wiring.
4. `ToolInputs` belongs to one transfer: `Duplicate` does not copy it and `Reset` clears it. A
   `Curl.Api` or `Libcurl` handle never sets it, so it behaves as before.

## Consequences

- BL-2394 can switch the runner from `ToolAttempt` to `ToolInputs` plus `LastResult` and delete
  `IToolAttemptTransfer`, `ToolAttempt` and `ToolAttemptResult`.
- The watchdogs, output wrappers, `-J` header watcher, styled headers and progress meter are still the
  runner's through `ToolAttempt` until the other BL-2394 split tasks add them.
