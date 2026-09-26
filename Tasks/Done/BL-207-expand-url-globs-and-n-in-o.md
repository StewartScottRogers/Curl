---
id: BL-207
title: Expand URL globs and #N in -o
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions/ADR-0032-url-globs-expand-as-curl-8-21-0s-tool-expands-them.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-207 — Expand URL globs and #N in -o

## Goal

URL globs (`{a,b}`, `[1-10]`, `[01-10]`, `[a-z:2]`) expand in curl's order, `#N` in `-o` substitutes, and `-g` turns globbing off.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-031 (globs in `-T`) waits on BL-030 and may reuse this expander.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Each glob form and `#N` substitution is tested against curl 8.21.0's expansion.
- [x] A bad glob returns `CurlExitCode.UrlMalformat` (3) with the measured `bad range ... position N` message.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered as `Curl.Core.Globbing.UrlGlob` (`TryParse`, `Unglobbed` for `-g`, lazy `Expand()`, `UrlCount`) and `UrlGlobMatch.SubstituteGlobValues` for `#N`; decisions in ADR-0032 (decided by Claude under Stewart's delegation). Implemented directly in the session rather than through the full `/feature` agent chain: one library, a port of `tool_urlglob.c`, driven by measurement.
- Measured with `/mingw64/bin/curl` 8.21.0 on 2026-09-26: `curl -s -S -w '%{url}|%{filename_effective}\n' -o '<name>' '<url>'` over `file:///n/...` (each transfer fails with 37 but prints every expanded URL and `-o` name). Examples: `-o 'o_#1_#2.txt' 'file:///nonexist/{a,b}x[1-2]'` gives `ax1|o_a_1.txt`, `ax2|o_a_2.txt`, `bx1|o_b_1.txt`, `bx2|o_b_2.txt`; `[08-100:45]` gives `08`, `53`, `98`; `[a-z:5]` gives `a f k p u z`; `-o 'o_#01_#0_#1#' '[1-2]'` gives `o_1_#0_1#`; `-g -o 'o_#1' '[1-2]{a,b}'` gives the URL and `o_#1` as written; `'file:///n/[3-1]'` gives `curl: (3) bad range in position 16:` / URL / 15 spaces and `^`. Every case, including all 45 error cases, is a `DataRow` in `UrlGlobTests`.
- Learned by measuring: every closed `{...}` set before an error moves the reported column one left (`{a}]` reports position 3), because curl passes `}` without counting it; a set's `range overflow` has no position; `[x-MAX]` is `range end/step overflow`; there is no limit on the number of globs (100 sets expand); a leading `[` whose text is not an IPv6 literal is a range, so `http://[1.2.3.4]/` is `bad range in position 10`.
- Choice (sensible default): IPv6 literals are recognised by shape plus `IPAddress` rather than by porting libcurl's URL parser; recorded in ADR-0032 as the accepted risk.
- `touches` widened to the new ADR-0032 file and `Documentation/Planning/Decisions/README.md` (its index row); no task in Doing names either.
- Follow-ups filed: BL-276 (parse `-g`/`--globoff` in `Curl.Cli`), BL-277 (Windows `sanitize_file_name` on substituted `-o` names). Wiring into `Curl.Console` is the existing BL-240; `-T` globs are BL-031.
- Gates: `dotnet build` clean; fast tests green (Curl.Core.UnitTests 497 passed, 2 skipped; whole solution 0 failed); `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. URL globs ({a,b}, [1-10], [01-10], [a-z:2]) expand in curl 8.21.0's order with its exit-3 position messages, #N substitutes into -o names, and UrlGlob.Unglobbed serves -g
