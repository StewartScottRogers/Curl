---
id: BL-283
title: Sanitize glob-substituted -o names on Windows as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-207]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-283 — Sanitize glob-substituted -o names on Windows as curl does

## Goal

An `-o` name after `#N` substitution is sanitized on Windows as curl 8.21.0 sanitizes it (`sanitize_file_name` with `SANITIZE_ALLOW_PATH | SANITIZE_ALLOW_RESERVED`), so a glob value holding `?`, `*`, `"`, `<`, `>` or `|` gives the file name real curl writes.

## Context

- Filed from BL-207: `UrlGlobMatch.SubstituteGlobValues` substitutes `#N` exactly as `glob_match_url` does but stops before the Windows-only `sanitize_file_name` step that follows it in `tool_urlglob.c`.
- Measure against the Windows reference `/mingw64/bin/curl` (8.21.0), e.g. `curl -s -w "%{filename_effective}\n" -o "o_#1" "file:///n/{a?b,c*d,e:f}"`, record the commands and output in Notes, then pin them.
- Decide whether the step lives in `UrlGlobMatch` or in a separate sanitizer, and whether it runs only on Windows (curl runs it only on Windows and MS-DOS builds); record it in an ADR.

## Acceptance criteria

- [x] Each character curl replaces or keeps in a substituted `-o` name on Windows is pinned in a test against measured curl 8.21.0 output.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

Measured 2026-09-26 against `C:\Program Files\Git\mingw64in\curl.exe` (= `/mingw64/bin/curl`, curl 8.21.0 x86_64-w64-mingw32, Schannel), `-s -w "%{filename_effective}|%{exitcode}
"`, every transfer exit 37:

- `-o "o_#1" "file:///n/{a?b,c*d,e:f,g\"h,i<j,k>l,m|n,q/r}"` -> `o_a_b o_c_d o_e:f o_g_h o_i_j o_k_l o_m_n o_q/r`
- `-o "o_#1" "file:///n/{CON,a.,b%20,a%3Fb}"` -> `o_CON o_a. o_b%20 o_a%3Fb` (reserved names, trailing dots, percent text kept)
- `-o "o?_#1" "file:///n/{a,b}"` -> `o__a o__b`: the whole name is sanitized, not only the substituted values.
- `-o "o?x" file:///n/a` -> `o_x`: sanitized even when the URL holds no glob; with `-g` -> `o?x` (curl skips `glob_match_url` entirely).
- Bytes 0x01 and 0x1F and tab -> `_`; 0x7F and `é` kept.
- `-o '\?\C:	mp?b'` -> `\?\C:	mp_b` (prefix kept); `'\srv?b'` -> `\srv_b`; `'a\b\c'` unchanged.
- 259, 260, 300, 32767 and 40000 `a`s (the long ones through `-K`) all come back unchanged: 8.21.0 has no length limit on this path.

Decisions (Decided by Claude under Stewart's delegation):
- The step is its own `internal static WindowsOutputFileNameSanitizer`, called from the new `UrlGlobMatch.ResolveOutputFileName(name, sanitizesForWindows)`; `SubstituteGlobValues` stays the pure `#N` step. Why: one name per thing, and the sanitizer is testable alone.
- The platform is a parameter, not `OperatingSystem.IsWindows()` read inside, so both branches are tested on any OS; the caller (BL-240, the `Curl.Console` wiring) passes `OperatingSystem.IsWindows()`. Runs only on Windows, as curl does (`#if defined(_WIN32) || defined(MSDOS)`).
- `UrlGlob` now records whether it came from `TryParse` or `Unglobbed`, so under `-g` the name is returned as written, matching the measured `-g` case.
- Rejected: sanitizing inside `SubstituteGlobValues` (hides a platform step behind a name that says substitution); sanitizing only substituted values (measurement shows curl sanitizes the whole name); a length limit (none measured).
- The ADR could not be written here: `Documentation/Planning/Decisions` is in BL-163's `touches` (in Doing on another lane). Filed BL-312 to record it from these Notes.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. On Windows, -o names resolved through UrlGlobMatch.ResolveOutputFileName are sanitized as curl 8.21.0 does; ADR follow-up BL-312
