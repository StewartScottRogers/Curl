# ADR-0620 — The HSTS read and write callbacks go through the cache's file text

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2421.

## Context

`Curl.Api`'s `EasyCallbackOptions` carries `CURLOPT_HSTSREADFUNCTION` (`HstsReadCallback`) and
`CURLOPT_HSTSWRITEFUNCTION` (`HstsWriteCallback`), each entry an `HstsEntry` (name, `includeSubDomains`, expiry as
`YYYYMMDD HH:MM:SS` or `null` for never) and each answer an `HstsResult` (`CURLSTS_*`). Nothing called them, and
`Libcurl` answered `CURLE_NOT_BUILT_IN` for them and their `*DATA` options.

In curl 8.22.0 a handle has an HSTS cache once `CURLOPT_HSTS_CTRL` sets `CURLHSTS_ENABLE` or `CURLOPT_HSTS` names a
file. `Curl_pretransfer` then calls `hsts_pull` before every transfer: it asks the read callback for one entry per
call while it answers `CURLSTS_OK`, adding each (an empty expiry meaning never); `CURLSTS_FAIL` fails the transfer
with `CURLE_ABORTED_BY_CALLBACK` and an `OK` with no name with `CURLE_BAD_FUNCTION_ARGUMENT`, neither through
`failf`. `Curl_close` (`curl_easy_cleanup`) calls `Curl_hsts_save`, whose `hsts_push` hands the write callback each
entry with its index and the total, stopping at the first answer that is not `CURLSTS_OK`; the read-only flag
only skips the file, and cleanup ignores the result.

The engine's cache is `Curl.Core`'s `HstsTransferPolicy`, shared by an engine's handles (ADR-0542) or by a share
handle's (ADR-0617). Its only ways in and out are the HSTS file's text: `ReadFile` and `FormatFile`. This task's
`touches` do not include `Curl.Core.UnitLibrary`.

## Decision

1. **`CallbackHstsCache` in the engine.** `Load` turns each entry the read callback hands over into one file line
   (`[.]name "expiry"`, `unlimited` for none) and reads it with `HstsTransferPolicy.ReadFile`; `Save` formats the
   cache with `FormatFile` (no expiry limit, so it never declines) and hands each line after the header comments
   to the write callback as an `HstsEntry`. The callbacks' expiry text is the file's own, so nothing is
   converted. An entry the file reader skips (an expiry already past or unreadable, a name with a space) is
   skipped from the callback too; libcurl would add it. That edge is accepted rather than widening the task into
   `Curl.Core`.
2. **When.** Both callbacks run only while `CallbackHstsCache.IsEnabled` (`HstsControl.Enable` or a `CURLOPT_HSTS`
   value, even empty). `ManagedEasyTransfer` calls `Load` on every perform from the options, after the URL parses and
   before connect-only or any setup, on the cache the perform uses (the share handle's when attached); a failure
   ends the perform with its code and no error text. `DisposeAsync` calls `Save` on the cache the last perform
   used, or the engine's before any perform, ahead of closing connect-only connections. The `--hsts` file is still
   read and written around each perform, as before (ADR-0544).
3. **Libcurl.** `Libcurl/Callbacks/Hsts` sets both function options (their delegate or `null`, any other value
   `BadFunctionArgument`); `HSTSREADDATA` and `HSTSWRITEDATA` take any value and change nothing, the closure being
   the user data (ADR-0573).

## Consequences

An application can preload and persist the HSTS cache without a file, as with libcurl. If `HstsTransferPolicy`
ever gains entry-level access, `CallbackHstsCache` can drop the text round trip without changing its behaviour.
