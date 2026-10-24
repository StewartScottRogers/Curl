# ADR-0562 — `CurlErrorText` answers upstream's strerror strings from tables, and the header API's text is upstream's `CURLHE_*` comments

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2459.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` gives `Libcurl` the strerror family.
Upstream curl 8.22.0's `lib/strerror.c` has `curl_multi_strerror`, `curl_share_strerror` and
`curl_url_strerror`; upstream's test1538 (`Curl.Conformance.UnitTests/CurlTestCases/Original/test1538.curltest`)
prints every code of each, plus one past the last, so it pins both the strings and the fallback for
an unknown code: `Unknown error` for multi, `CURLSHcode unknown` for share, `CURLUcode unknown` for
URL. Upstream has no strerror function for the header API's `CURLHcode`, but BL-2459 asks for its
text too, so `Libcurl` users can describe a header API result the same way.

## Decision

1. `Libcurl.Misc.ErrorText.CurlErrorText` has `Multi`, `Share`, `Url` and `Header`, each a lookup in
   a string table indexed by the code (multi by code + 1, since `CURLM_CALL_MULTI_PERFORM` is -1),
   with upstream's fallback for an index outside the table. A table keeps each method at
   cyclomatic complexity 2, where a switch over 33 URL codes would break the gate of 10.
2. The strings are test1538's, byte for byte.
3. `Header` answers the comment upstream writes beside each `CURLHE_*` value in `include/curl/header.h`,
   capitalised as a sentence (`CURLHE_OK` is `No error`, as in every other family), and
   `CURLHcode unknown` for an unknown code, following share's and URL's pattern. There is no upstream
   function to match, so this is the simplest text that says what upstream says the code means.
4. `curl_easy_strerror` stays `Curl.Console`'s `CurlEasyErrorText` until BL-2450 moves it here.

## Consequences

- A new code upstream adds needs one string appended to its table; until then it answers the fallback,
  as an older libcurl does.
- If upstream ever adds `curl_header_strerror`, `Header`'s strings are re-pinned to it.
