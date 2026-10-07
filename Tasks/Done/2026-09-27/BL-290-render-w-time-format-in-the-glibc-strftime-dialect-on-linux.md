---
id: BL-290
title: Render -w %time{format} in the glibc strftime dialect on Linux and macOS
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-279]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions/ADR-0038-w-time-follows-the-windows-c-runtime-strftime-and-onerror-reads-transfer-failed.md, Documentation/Planning/Decisions/ADR-0078-w-time-on-linux-and-macos-follows-glibc-strftime-in-the-c-locale.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-290 — Render -w %time{format} in the glibc strftime dialect on Linux and macOS

## Goal

On Linux and macOS, `-w "%time{format}"` prints what curl 8.21.0's OpenSSL build prints there, through glibc's `strftime` in the C locale.

## Context

- BL-279 made `WriteOutTimeFormatter` follow the Windows C runtime (ADR-0038). glibc knows more conversions (`%C %D %e %F %g %G %h %k %l %n %P %r %R %t %T %u %V`, the `_ - 0 ^ #` flags, widths, the `E` and `O` modifiers), and its C-locale `%c`, `%x`, `%X` are `Sun Sep 27 03:30:08 2026`, `09/27/26`, `03:30:08`.
- curl substitutes `%f`, `%z` and `%Z` itself on every platform (`src/tool_writeout.c`, `outtime`).
- Measure on a Linux curl 8.21.0 before pinning; the renderer needs a way to choose the dialect, as it already takes `writesLineFeedAsCrLf` for the Windows line ends.

## Acceptance criteria

- [x] `WriteOutTimeFormatter` (or a sibling) renders the glibc conversions and flags measured on Linux curl 8.21.0, pinned in tests with a fake `TimeProvider`, and the Windows dialect's tests still pass.
- [x] `WriteOutTemplateRenderer` chooses the dialect from a constructor argument, documented in ADR-0038 or a successor ADR.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output.UnitLibrary`.

## Notes

- Plan, done directly (one library and its tests): `WriteOutTimeDialect { WindowsCRuntime, Glibc }`;
  `WriteOutTimeFormatter.Format(format, dialect, timeProvider)` dispatches to
  `WindowsCRuntimeTimeFormat` (the BL-279 code, moved unchanged) or the new `GlibcTimeFormat`;
  `WriteOutTemplateRenderer` takes the dialect as a constructor argument. Decision in ADR-0078.
- Measurement: Ubuntu's WSL has curl 8.18.0, whose `outtime` differs from 8.21.0 (8.21.0
  rewrites `%s` and keeps `%%`; compared the two `src/tool_writeout.c`). So curl 8.21.0 was
  built from the release tarball in a `debian:trixie` container (glibc 2.41) with
  `./configure --enable-debug --without-ssl --without-libpsl --without-zlib --without-brotli
  --without-zstd --without-libidn2 --without-nghttp2 --disable-ldap`, and every format run as
  `CURL_TIME=<seconds> LD_LIBRARY_PATH=lib/.libs src/.libs/curl -s -o /dev/null -w '%time{<format>}' file:///tmp/f`
  (the debug build's `CURL_TIME` sets the seconds and uses seconds % 1000000 as the
  microseconds). Instants 1790480000, 1798762029, 1735566306 (a Sunday 03:33, New Year's
  Day 2027 in ISO week 53 of 2026, a Monday afternoon in ISO week 1 of 2025). 1,593 formats
  (59 conversion characters under 26 flag/width/modifier prefixes, plus edge cases) are in
  `Curl.Output.UnitTests/Fixtures/glibc-time-format.json`; `GlibcTimeFormatTests` checks all
  4,779 results. `%s`/`%-s` under `TZ=JST-9` and `TZ=EST5EDT,M3.2.0,M11.1.0` measured
  separately (mktime reads the UTC fields as local standard time).
- `touches` widened (rule 3): the successor ADR-0078, its line in the Decisions README, and
  an "Extended by" line in ADR-0038. No task in Doing names them.
- `Curl.Console` builds the renderer (`CurlCommandRunner.writeOutRenderer`) and is BL-131's,
  in Doing. Default taken: kept the three-argument constructor, which uses the Windows
  dialect, so Console builds unchanged; filed BL-387 to pass the platform's dialect from
  `Curl.Console` and remove that constructor. Until then Linux still renders the Windows
  dialect.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` reports 100% line,
  100% branch, 320 members, 0 failing, worst CRAP 10. Flag parsing and curl's rewrite
  use lookups to stay under complexity 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -w %time{format} renders glibc's strftime (C locale) through WriteOutTimeDialect.Glibc, pinned to 4,779 results measured from curl 8.21.0 on glibc 2.41
