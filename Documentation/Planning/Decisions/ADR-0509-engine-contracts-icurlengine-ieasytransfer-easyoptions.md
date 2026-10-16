# ADR-0509 — The engine contracts: `ICurlEngine`, `IEasyTransfer` and `EasyOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2312.

## Context

ADR-0506 puts one transfer engine behind `Curl.Api` and `Libcurl`, fed a CLI-neutral option
model. BL-2312 defines the contract an engine implements for one easy handle, in
`Curl.Api.UnitLibrary/EngineContracts` (namespace `Curl.Api.EngineContracts`). A few shapes had
more than one reasonable answer.

## Decision

- `EasyOptions` (namespace `Curl.Api.Options`) is a `sealed record` with one property per option
  group, each defaulting to `new()` of that group's record. Every group's list defaults to `[]`
  (a shared empty array), so two fresh instances are equal and `curl_easy_reset` is
  `Options = new EasyOptions()`.
- `IEasyTransfer.Options` is a settable `EasyOptions`: `curl_easy_setopt` of one option is a
  `with` expression on the current value. No per-option setter on the interface.
- `CURLOPT_ERRORBUFFER`'s content is `string ErrorText`, empty when there is no detail; the
  buffer itself belongs to `Libcurl`.
- `curl_easy_pause`'s bitmask is `[Flags] enum CurlPause` with upstream's numbers: `Continue` 0
  (`CURLPAUSE_CONT`, also `_RECV_CONT` and `_SEND_CONT`), `Receive` 1, `Send` 4, `All` 5. The
  `*_CONT` aliases are not separate members because they share the number 0.
- `curl_version_info`'s data is the record `CurlVersionInfo`: `Version`, `VersionNumber`, `Host`,
  `Features` (by name, as `feature_names`), `Protocols` and `TlsBackend` (`ssl_version`). The
  `CURL_VERSION_*` bitmask is not stored: `Libcurl` derives it from the names, so the two cannot
  disagree. Defaults describe libcurl 8.22.0 with nothing built in (`"8.22.0"`, `0x081600`, empty
  host, no features, no protocols, no TLS); an engine fills in what it has. The other
  `curl_version_info_data` fields (library versions of zlib, brotli and the like) are added when
  `Libcurl` needs them.
- `ICurlEngine`'s lifetime is `curl_global_init`'s to `curl_global_cleanup`'s: construction is
  the init, `DisposeAsync` the cleanup. Multi, share, URL and MIME factories (BL-2313, BL-2314)
  and the WebSocket, header and connect-only members of `IEasyTransfer` (BL-2315) come later.

## Consequences

The engine and both programming interfaces share one option type, compared by value. Adding a
member to either interface later breaks every implementation, which is intended while only the
one engine implements them.
