# ADR-0541 — `ManagedCurlEngine` and `ManagedEasyTransfer` run one transfer from `EasyOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2316.

## Context

ADR-0506's plan, phase 2 step 4: the engine implements `Curl.Api`'s `ICurlEngine` and
`IEasyTransfer` so the seam is proven end to end. The engine already reads only the easy option
groups (BL-2310), but several of the curl tool's own settings (`EngineTransferSettings`) have no
easy option yet.

## Decision

- **One dispatcher per perform, over engine-owned caches.** `ManagedCurlEngine.CreateDispatcher`
  builds the transports and handlers from the handle's options with `EngineComposition`, as
  `Curl.Console` builds them per option group, over a `ConnectionCache`, `TlsSessionCache` and
  `DnsCache` held by the engine object. Nothing is static, so two engines share nothing; disposing
  the engine closes its pooled connections (`curl_global_cleanup`).
- **The tool's settings take libcurl's defaults** where no easy option sets them: silent, no
  `--fail-with-body` (`-f` is `EasyBehaviourOptions.FailOnError`), no protocol limits, no
  `--ssl*` level, no `-d` copy (the body is `EasyHttpOptions.PostFields`), no URL query appended.
  Verify-peer, the key password, the crypto engine, the proxy tunnel and the Unix socket come from
  their easy options.
- **Callbacks, not standard output.** Body bytes go to the write callback and header lines to the
  header callback, each discarded when unset; libcurl's default of writing to `stdout` is
  `Libcurl`'s job in phase 3. A callback returning anything but the chunk's length is
  `CURLE_WRITE_ERROR`, as in libcurl; `CURL_WRITEFUNC_PAUSE` is not honoured yet (phase 3).
- **No URL is `CURLE_URL_MALFORMAT`** with `No URL set`, as libcurl's `Curl_pretransfer` answers.
- **Phase 3 members throw `NotSupportedException`**: the multi, share, URL, MIME and form handles,
  the header API, WebSocket and connect-only. `Pause` records the directions and succeeds, and
  `UpkeepAsync` succeeds, as libcurl does on an idle handle.

## Consequences

`Curl.Api` and `Libcurl` can drive a transfer through `ManagedCurlEngine`. The tool's runner still
uses its own loop; moving it onto the engine, retries and the per-transfer setup steps are later
phases.
