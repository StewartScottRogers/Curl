# ADR-0590 — `CURLOPT_CURLU` stores the engine's `ICurlUrlHandle` by reference as `EasyNetworkOptions.UrlHandle`, replacing the string `UrlFromUrlHandle`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2462.

## Context

BL-2295 gave `EasyNetworkOptions` a `string? UrlFromUrlHandle` for `CURLOPT_CURLU` before the seam had
`ICurlUrlHandle`. Upstream's `lib/setopt.c` (curl 8.22.0, `case CURLOPT_CURLU`) stores the `CURLU *`
pointer itself (`data->set.uh = va_arg(param, CURLU *)`), not a copy of its URL: the application may
change the handle's parts after `curl_easy_setopt` and before `curl_easy_perform`, and the transfer
uses the handle as it is at perform time. A string taken at set time cannot do that. Nothing read
`UrlFromUrlHandle` yet.

## Decision

1. `EasyNetworkOptions.UrlFromUrlHandle` is replaced, not kept beside, by `ICurlUrlHandle? UrlHandle`:
   one concept, one name, and no stale copy of the URL to fall out of step with the handle.
2. `UrlHandle` holds the engine handle by reference, as libcurl holds the pointer. The easy handle
   does not own it: `curl_url_cleanup` stays the application's job, as upstream.
3. `Libcurl`'s `SetOption(CurlOption.Curlu, value)` takes a `CurlUrlHandle` and stores its
   `EngineHandle`; `null` clears it (upstream's `NULL`); any other object answers
   `BadFunctionArgument`, as every other `Libcurl` object option given a value its field cannot hold.
   A cleaned-up `CurlUrlHandle` throws `ObjectDisposedException` from `EngineHandle`, as every member
   of a cleaned-up handle does (ADR-0587); upstream's equivalent, a freed pointer, is undefined.

## Consequences

- The engine reads the handle's URL at perform time and uses it in place of `Url`, as libcurl does.
- The record's equality compares the handle by reference, which is the identity libcurl uses.
