# ADR-0617 — The share handle's public suffix list and HSTS

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2435.

## Context

ADR-0606 gave the engine's share handle its own HSTS cache (`ManagedShareHandle.Hsts`) and accepted
`CURL_LOCK_DATA_PSL` as a kind that "shares nothing". In curl 8.22.0 a share handle sharing
`CURL_LOCK_DATA_PSL` holds one libpsl handle (`Curl_psl_use`), loaded once and kept for every attached
transfer, and the PSL lock is taken while a transfer uses it. Curl has no libpsl: every cookie-domain check
asks `Curl.Cookies`' `PublicSuffixList.Embedded`, one snapshot embedded in the assembly (ADR-0049), read
once per process and never changed.

## Decision

1. **The public suffix list is shared whatever the share handle says.** Every transfer of every engine already
   checks against the one embedded snapshot, so there is nothing for a share handle to hold. Sharing
   `CurlLockData.Psl` still answers `Ok`, and while it is shared each attached transfer takes it under the
   `Psl` lock as it attaches, right after the `Share` lock, so an application's lock callbacks see the kind
   taken as with every other shared kind. The engine takes no `Psl` lock around each cookie check: the
   snapshot is immutable and thread-safe, so the lock would guard nothing.
2. **HSTS sharing stays as ADR-0606 built it**, now pinned by engine tests: a `Strict-Transport-Security`
   header one engine's handle learns over `https` switches another engine's handle's `http` URL to `https`
   while `CurlLockData.Hsts` is shared, and not when it is not.

## Consequences

- No new type: `ManagedShareHandle.Attach` takes the PSL lock through the same `TakeShared` path as the caches.
- A future loadable public suffix list (a `--psl`-like file) would need the share handle to hold it; none is
  planned, since curl has no such option.
