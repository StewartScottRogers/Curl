# ADR-0578 — `Libcurl`'s `curl_getdate` is `CurlDate.Parse` and `curl_slist` is `CurlSlist`, copied on set

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2448.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` lists `curl_getdate` and
`curl_slist_append` / `_free_all`. The plan places the date parser in `Curl.Core`; it is
`CurlDateParser` in `Curl.Protocol.Abstractions.UnitLibrary`, which `Libcurl` reaches through
`Curl.Api.UnitLibrary`. Upstream's list options keep the caller's `curl_slist *`, so the caller must
keep the list alive and unchanged until the transfer ends; `Libcurl`'s option setters already copy
any `IEnumerable<string>` into the `EasyOptions` record when the option is set.

## Decision

1. `Libcurl/Misc/DatesAndLists` holds both. `CurlDate.Parse(string)` is `curl_getdate`: the seconds
   `CurlDateParser.TryParse` reads, or `CurlDate.Invalid` (-1) when it refuses the text. The parser
   already moves a real -1 (one second before the epoch) to 0, as upstream's `curl_getdate` does.
   Upstream's unused second argument is dropped; a `null` text throws `ArgumentNullException`.
2. `CurlSlist` is an `IReadOnlyList<string>`, so it is a value every list-valued option takes.
   `CurlSlist.Append(list, text)` adds to `list`, or to a new list when it is `null`, and answers
   the list; a `null` text throws, where upstream's `strdup(NULL)` is undefined.
   `CurlSlist.FreeAll(list)` empties the list and does nothing for `null`.
3. Setting an option copies the list's strings then; appending to or freeing the list afterwards
   leaves the option unchanged. A caller that keeps the list alive, as upstream requires, sees the
   same transfer as with libcurl, and one that does not cannot corrupt it.

## Consequences

Each option group's setter keeps copying its list on set, as `EasyHttpOptionSetters.ListOf` does; a
new list-valued setter that stored the caller's list would break `CurlSlist`'s promise.
