# ADR-0695 — Curl has the `threadsafe` and `threaded-resolver` features in the upstream case runner

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2700.

## Context

Upstream case 3026 (`lib3026`, `curl_global_init` from 100 threads at once, ported by BL-2551) names
`<features>threadsafe threaded-resolver</features>`. `UpstreamCurlPlatform` listed neither, calling `threadsafe` a
libcurl build feature a command-line tool does not have and `threaded-resolver` absent from Curl's `curl -V`, so the
ratchet skipped the case as `Curl lacks the feature threadsafe`.

Both statements had stopped being true:

- Curl's `curl -V` `Features:` line already names `threadsafe` on every platform (`CurlVersionText`), as both
  reference builds do, and `ManagedCurlVersion` gives `Libcurl`'s `curl_version_info` the `CURL_VERSION_THREADSAFE`
  bit. `CurlGlobal` is safe to call from many threads at once: each init makes its own `CurlLibrary`.
- `runtests.pl` never reads `threaded-resolver` from the `Features:` line. It sets the feature when the line names
  `AsynchDNS` and the `libcurl/` line names no `c-ares`. Curl's line names `AsynchDNS` and no `c-ares`, as both
  reference builds (the Schannel build on Windows, the OpenSSL build on Linux and macOS) do: their resolver runs
  `getaddrinfo` on a thread, and Curl's resolves through `Dns.GetHostAddressesAsync`, which does the same on the
  thread pool.

## Decision

- `UpstreamCurlPlatform` reports `threadsafe` and `threaded-resolver` on every platform, as `runtests.pl` would derive
  them from Curl's own `curl -V`. `c-ares` stays off.
- Curl's `curl -V` and `curl_version_info` are unchanged: they already list `threadsafe`, and `threaded-resolver` is
  a harness feature, not a `Features:` word.

## Consequences

- Case 3026 runs and passes on Windows; it starts threads in process and exchanges no bytes, so nothing in it depends
  on the platform. It is on `PassingUpstreamCases.txt`.
- Case 506 (`!threaded-resolver`, shared cookie list and DNS cache) is now skipped, as `runtests.pl` skips it for the
  reference curl, and leaves `PassingUpstreamCases.txt`. Its lock order assumes a resolver that runs inside the
  transfer, which neither reference build has.
