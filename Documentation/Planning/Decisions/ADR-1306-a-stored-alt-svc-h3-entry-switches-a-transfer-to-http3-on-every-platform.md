# ADR-1306: A stored alt-svc h3 entry switches a transfer to HTTP/3 on every platform

- Status: Accepted
- Date: 2026-11-19
- Task: BL-5549
- Decided by Claude under Stewart's delegation.
- Amends ADR-1238 and ADR-1268.

## Context

ADR-1268 made an `h3` entry count with no version option only when `actsAsQuicBuild` was set, which the executable never sets, and ADR-1238 let a Windows transfer switch to `h1` alone, because the Schannel build then had neither HTTP/2 nor HTTP/3. Since ADR-1282 `curl -V` lists HTTP2 and HTTP3 on every platform and `--http3-only` works, so a server's `Alt-Svc: h3=...` was stored but never used (bug report 2026-11-19T0642, finding 1).

## Decision

What `curl -V` lists and what alt-svc does agree. With no version option a transfer may switch to `h1`, `h2` and `h3` on every platform, as curl built with HTTP/2 and HTTP/3 does (the Linux reference's `url_set_conn_peer` rules, already in `AltSvcTransferCache.LookupOf`). `--http1.1` and `--http2` still allow only `h1` and `h2`, so they ignore an `h3` entry; `--http3` and `--http3-only` are unchanged. The `isWindows` and `actsAsQuicBuild` switches are removed from `LookupOf`, `AltSvcTransferCache.OpenAsync` and `TransferPerformer.OpenAltSvcCacheAsync`. A second URL in one command uses the same cache lookup.

Not measured against a live HTTP/3-capable curl: lanes run on Windows, whose curl has no HTTP/3. The rules are those the cache already pinned for the Linux reference.

## Consequences

`CommandLineOptions.ActsAsQuicBuild` is no longer read by the alt-svc code.
