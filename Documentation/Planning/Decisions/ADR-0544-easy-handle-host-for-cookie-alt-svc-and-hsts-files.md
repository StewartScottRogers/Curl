# ADR-0544 — The easy handle reaches files, standard input and the log through an internal host

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2362.

## Context

ADR-0542 shared cookies, HSTS, pooled connections and `%{xfer_id}` across a `ManagedCurlEngine`'s
handles, but `ManagedEasyTransfer.PerformAsync` read no `-b` file and wrote no `-c` jar, alt-svc or
HSTS file, numbered no `%{conn_id}` and carried no diagnostic log. `CurlCommandRunner` does all of
these around its serial attempt through `TransferPerformer`'s steps (`LoadCookieFilesAsync`,
`OpenAltSvcCacheAsync`, `SwitchToHttpsForHstsAsync`, `WriteCookieJarAltSvcAndHstsFilesAsync`) and
`ConnectionIdRecordingTransferEvents`. Those steps take an `IFileSystem`, standard input, the `-c -`
print target, a diagnostic log and the platform's line endings, none of which is an easy option:
libcurl reads the real disk and has no standard stream of its own to print to.

## Decision

- **One internal record, `EasyTransferHost`, on `ManagedEasyTransfer.Host`.** It holds the
  `IFileSystem`, the standard input `-b -` reads, the `Func<Stream>` `-c -` prints to, the
  `IDiagnosticLog` every context carries, whether the `--hsts` file ends its lines in CR LF, and
  whether the alt-svc cache serves plain `http` (`CURL_ALTSVC_HTTP`). It sits beside
  `ToolSettings` (ADR-0542) and, like it, is internal: `Curl.Console` sets it from its runner
  (BL-2318); no public `Curl.Api` member is added.
- **The default is `EasyTransferHost.Library`:** `Curl.Core`'s `PhysicalFileSystem`, as libcurl reads
  `CURLOPT_COOKIEFILE` and writes `CURLOPT_COOKIEJAR`, `CURLOPT_ALTSVC` and `CURLOPT_HSTS` on disk;
  nothing to read for `-` and `Stream.Null` for `-c -`, as the engine writes to no standard stream
  (ADR-0542: printing is `Libcurl`'s job); no diagnostic log; the running platform's line endings.
- **`Duplicate` copies the host and `Reset` keeps it**: it is the host's wiring, not an option.
- **The steps run in the runner's order, through the same `TransferPerformer` methods**: the `-b`
  files, the alt-svc file, then the HSTS file and switch, before connecting; after the transfer,
  whatever its outcome (the progress callback's abort included), the jar, the alt-svc file and the
  HSTS file. A caller's cancel that throws writes nothing, as the exception ends the perform.
- **`%{conn_id}` is numbered by the engine's `TransferPerformer`** through
  `EngineConnectionIdTransferEvents`, an engine copy of the runner's decorator over
  `EngineTransferState`, with the pool-number map on `ManagedEngineSharedState`; it is always on, as
  `TransferInfo.ConnectionId` is always filled. A transfer failing with an unsupported scheme or a
  malformed URL gets -1, as the runner gives it. The runner keeps its own decorator until BL-2318
  routes it through the engine.
- **The `-b` files load on every perform**, as each perform builds its own `CookieEngine` over the
  shared store; reloading the same file replaces the same cookies.

## Consequences

`Curl.Api` and `Libcurl` handles now read and write the cookie, alt-svc and HSTS files they name
and report `CURLINFO_CONN_ID`. BL-2318 sets `Host` and `ToolSettings` from the command line to get
the runner's bytes.
