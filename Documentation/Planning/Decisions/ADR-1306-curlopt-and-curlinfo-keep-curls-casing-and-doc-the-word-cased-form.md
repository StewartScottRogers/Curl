# ADR-1306: CurlOption and CurlInfo keep curl's casing and document the word-cased form (BL-5553)

- Status: Accepted
- Date: 2026-11-19
- Decided by Claude under Stewart's delegation.

## Context

`CurlOption` and `CurlInfo` member names are the upstream `CURLOPT_*` / `CURLINFO_*` name with
one capital per underscore-separated word, so `CURLOPT_WRITEFUNCTION` is `Writefunction`. A reader
who knows `WriteFunction` from C finds nothing when searching `Libcurl` for it.

## Decision

Keep the member names. Renaming ~330 members would break every caller of a published preview and
buys nothing the documentation cannot give, and the names stay mechanically derivable from curl's
own (`CURLOPT_X_Y` becomes `XY`). Instead every member whose upstream name joins several English
words without underscores carries, at the end of its XML summary, `Word-cased: <c>WriteFunction</c>.`
so a text search for the word-cased form finds the member. The summary already names the `CURLOPT_*` /
`CURLINFO_*` constant. The `Libcurl` README states the rule. `CurlOptions`' name table
(`curl_easy_option_by_name`) keeps returning curl's own upper-case names.

## Consequences

A new member whose name joins words without underscores adds its `Word-cased:` form to its summary.
