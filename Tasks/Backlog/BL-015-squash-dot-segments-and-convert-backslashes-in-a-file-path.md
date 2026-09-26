---
id: BL-015
title: Squash dot segments and convert backslashes in a file:// path
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008]
requirement: none
created: 2026-09-25
completed:
---
# BL-015 — Squash dot segments and convert backslashes in a `file://` path

## Goal

`FileUrlPath` converts `\` to `/` and removes RFC 3986 dot segments before a path
reaches `IFileSystem`, as curl 8.21.0 does, with a `pathAsIs` switch that suppresses the
dot-segment removal only.

## Context

Measured on this machine against curl 8.21.0 (`curl 8.21.0 (x86_64-w64-mingw32)
libcurl/8.21.0 Schannel`, Release-Date 2026-06-24):

- `file:///C:/dir\..\secret.txt` opens `C:/secret.txt`. Every `\` becomes `/`, then
  `.` and `..` segments are removed per RFC 3986 section 5.2.4, and only then is the
  file opened.
- `curl --help all` documents `--path-as-is` as `Do not squash .. sequences in URL
  path`: it suppresses the dot-segment removal and nothing else, so backslash
  conversion still happens under it. See <https://curl.se/docs/manpage.html>
  (`--path-as-is`) and <https://curl.se/docs/url-syntax.html>.

`Curl.Protocol.File.UnitLibrary\FileUrlPath.cs` does neither: `ToOperatingSystemPath`
decodes escapes and swaps `/` for `Path.DirectorySeparatorChar`, leaving `..` and `\`
exactly as written. Two tests in `Curl.Protocol.File.UnitTests\FileUrlPathTests.cs` pin
that wrong behaviour and must be retracted, not adjusted:
`TryParse_Backslashes_SurviveUnnormalised` (line 357) and
`TryParse_DotDotSegment_SurvivesUnresolved` (line 372).

The same mis-measurement is written into
`Documentation\Planning\Decisions\ADR-0003-itransfercontext-carries-transfer-options.md`,
in the `Known limitation, recorded but not decided here` list:

> - `Uri` normalises `..` away, where curl passes it straight to the OS.

curl does not pass `..` straight to the operating system, so that bullet documents a
divergence on a false premise and is worse than no note at all. It has to say what was
actually measured: both `Uri` and curl remove dot segments, but `Uri` does it always and
curl only without `--path-as-is`, and curl also folds `\` to `/` where `Uri` does not.

The XML remarks inside `FileUrlPath.cs` repeat the wrong claim in two places — the
`OsPath` parameter documentation ("no `.` or `..` segment has been resolved") and step 7
of the `TryParse` remarks ("Hand over unnormalised") — so they change with the code.

## Acceptance criteria

- [ ] `FileUrlPath.TryParse` converts every `\` to `/` before removing dot segments, so
      a test named `TryParse_BackslashDotDotSegments_ResolveBeforeTheOpen` asserts
      `OsPath` is `C:\secret.txt` (`Path.DirectorySeparatorChar`, so `C:/secret.txt` off
      Windows) for `file:///C:/dir\..\secret.txt`.
- [ ] A test asserts the ordering cannot be reversed: `file:///C:/a\../b` gives
      `OsPath` `C:\b`, which only holds if the backslash became a separator before the
      `..` was resolved.
- [ ] A test asserts single-dot removal: `file:///C:/./a/./b.txt` gives `C:\a\b.txt`.
- [ ] `..` segments that would climb above the root are measured before they are pinned:
      run `curl -s -o out.txt -w "%{exitcode}" "file:///C:/../../Windows/win.ini"` under
      curl 8.21.0, record the result in the `Notes` of this task, and pin the matching
      `OsPath` in a named test.
- [ ] `UrlPath` — the text quoted in the exit 37 message — is measured, not assumed: run
      `curl "file:///C:/dir/../nosuch.txt"` under curl 8.21.0, record the exact
      `curl: (37) Could not open file …` line, and pin it in a
      `FileProtocolHandlerTests` test that asserts
      `TransferResult.ErrorMessage` byte for byte for that URL.
- [ ] An overload `FileUrlPath.TryParse(Uri url, bool pathAsIs, out FileUrlPath path)`
      exists; the existing two-argument overload behaves as `pathAsIs: false`; a test
      named `TryParse_PathAsIs_KeepsDotDotButStillConvertsBackslashes` asserts that with
      `pathAsIs: true` the `..` survives in `OsPath` while `\` is still a separator.
- [ ] `TryParse_Backslashes_SurviveUnnormalised` and
      `TryParse_DotDotSegment_SurvivesUnresolved` are deleted from
      `FileUrlPathTests.cs` — not `[Ignore]`d — and the commit message names both.
- [ ] The ADR-0003 bullet quoted in `Context` is replaced by text stating the measured
      behaviour, dated, citing curl 8.21.0 and `--path-as-is`; the `OsPath` parameter
      documentation and step 7 of the `TryParse` remarks in `FileUrlPath.cs` no longer
      claim dot segments survive.
- [ ] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green, with no test carrying `[TestCategory("Integration")]`.

## Notes

`--path-as-is` is not wired to a command line option in this task: no option parsing
exists yet, and nothing on `ITransferContext` carries the flag. Do not add a member to
`Curl.Protocol.Abstractions.UnitLibrary` here — the `pathAsIs` parameter is the seam, and
it exists so both behaviours are pinned by tests now. Wiring the option belongs with the
command line work.

The ADR-0003 correction is a `docs-writer` edit and can be done in the document stage of
the same `/feature` run; it does not need a separate task.

## Log

- 2026-09-25: Created.
