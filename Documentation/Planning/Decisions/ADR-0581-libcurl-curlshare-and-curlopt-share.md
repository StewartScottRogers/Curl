# ADR-0581 — `Libcurl`'s `CurlShare` checks `curl_share_setopt` itself and `CURLOPT_SHARE` attaches its engine handle

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2433.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` lists `curl_share_init`, `_setopt`
(`CURLSHOPT_SHARE`, `UNSHARE`, `LOCKFUNC`, `UNLOCKFUNC`, `USERDATA`) and `_cleanup`. The seam already
has `ICurlEngine.CreateShare` and `IShareHandle` (BL-2314), whose lock delegates take the engine's
`IEasyTransfer` and no user pointer, and `EasyBehaviourOptions.Share` holds an `IShareHandle`, set by
the `CURLOPT_SHARE` row of `Libcurl/Options/Behaviour`. The managed engine's `CreateShare` still throws
`NotSupportedException`; its share work is a separate engine task.

## Decision

1. `Libcurl/Share` holds `CurlShare` over the engine's `IShareHandle`, `CurlShareOption`
   (`CURLSHOPT_*` with `curl.h`'s numbers) and the `CurlLibrary.ShareInit` part (`curl_share_init`).
2. `CurlShare.SetOption(option, value)` is `curl_share_setopt`. `SHARE` and `UNSHARE` take a
   `CurlLockData` or its number (`int` or `long`); `COOKIE` to `HSTS` go to the engine, and `NONE`,
   `SHARE` and any number `curl.h` lacks answer `CURLSHE_BAD_OPTION` without calling it, as
   `share.c`'s `default:` arm does. `LOCKFUNC` and `UNLOCKFUNC` take the seam's own delegate or
   `null` and hand it to the engine as given. `USERDATA` takes any value and keeps it as `UserData`:
   the seam's delegates have no user pointer, so a callback carries its state in its closure. An
   unknown option, or a value of the wrong type (which C cannot pass), answers `BadOption`.
   `CURLSHE_IN_USE` while an easy handle is attached is the engine's answer, passed through.
3. `Cleanup()` is `curl_share_cleanup`: on the engine's `Ok` the handle is cleaned up; on any other
   answer (`InUse`) it stays usable, as libcurl frees nothing then. A cleaned-up handle answers
   `CURLSHE_INVALID` to every later call, as libcurl's `GOOD_SHARE_HANDLE` check does.
4. `CURLOPT_SHARE` given a `CurlShare` stores its engine handle, so the engine sees its own
   `IShareHandle`, never a Libcurl wrapper; given a cleaned-up `CurlShare` it detaches and answers
   `Ok`, as `setopt.c` drops a share that fails `GOOD_SHARE_HANDLE`; `null` detaches; an
   `IShareHandle` is still taken as itself. The row changed in `Libcurl/Options/Behaviour`.

## Consequences

- Attaching and detaching need no change to `CurlEasy` or the seam.
- Locks are called with the engine's transfer, not the `CurlEasy`; mapping back would need a
  registry of easy handles, and no caller needs it yet.
