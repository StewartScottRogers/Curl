---
id: BL-5553
title: Decide how Libcurl CurlOption names split curl words (Writefunction vs WriteFunction) and make them findable
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Options/CurlOption.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5553 — Decide how Libcurl CurlOption names split curl words (Writefunction vs WriteFunction) and make them findable

## Goal

A reader searching `Libcurl` for the option name they know from C (`WriteFunction` for `CURLOPT_WRITEFUNCTION`, `InterleaveFunction` for `CURLOPT_INTERLEAVEFUNCTION`) finds it.

## Context

From bug report `2026-11-19T0642-Curl-bug-report.md`, finding 3 (Low), third bullet. Build tested: commit `0afbd10e3b`, `Libcurl 0.2.0`.

`CurlOption` names (`Libcurl/Options/CurlOption.cs`) come from curl's `CURLOPT_*` names with only the first letter capitalised (`Writefunction` at line 980, `Interleavefunction`), so a reader searching for `WriteFunction` finds nothing.

Decide in an ADR (decided by Claude under Stewart's delegation) between:

- renaming to word-cased names (`WriteFunction`); `Libcurl` is a 0.2.0 preview, so a breaking rename is possible, and every use in the solution follows;
- keeping the names and making them findable: each member's XML doc names its `CURLOPT_*` constant and the word-cased form, and the README says how names are formed.

Whatever is chosen, `CurlInfo` and any other enum built the same way follow the same rule. `CurlOptions`' name table (`CurlOptions.cs`, `curl_easy_option_by_name`) keeps returning curl's own upper-case names.

## Acceptance criteria

- [ ] An ADR records the naming rule for `CurlOption` and `CurlInfo`, and why.
- [ ] Under that rule, a text search of `Libcurl` for `WriteFunction` finds `CURLOPT_WRITEFUNCTION`'s member, by its name or its doc comment, and the same holds for every multi-word option.
- [ ] Looking up `WRITEFUNCTION` by name through `CurlOptions` behaves as before, pinned by a test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-19: Created from bug report 2026-11-19T0642-Curl-bug-report.md, finding 3 (option names).
