---
id: BL-015
title: Squash dot segments and convert backslashes in a file:// path
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation/Planning/Decisions/ADR-0003-itransfercontext-carries-transfer-options.md, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-25
completed: 2026-09-26
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

- [x] `FileUrlPath.TryParse` converts every `\` to `/` before removing dot segments, so
      a test named `TryParse_BackslashDotDotSegments_ResolveBeforeTheOpen` asserts
      `OsPath` is `C:\secret.txt` (`Path.DirectorySeparatorChar`, so `C:/secret.txt` off
      Windows) for `file:///C:/dir\..\secret.txt`.
- [x] A test asserts the ordering cannot be reversed: `file:///C:/a\../b` gives
      `OsPath` `C:\b`, which only holds if the backslash became a separator before the
      `..` was resolved.
- [x] A test asserts single-dot removal: `file:///C:/./a/./b.txt` gives `C:\a\b.txt`.
- [x] `..` segments that would climb above the root are measured before they are pinned:
      run `curl -s -o out.txt -w "%{exitcode}" "file:///C:/../../Windows/win.ini"` under
      curl 8.21.0, record the result in the `Notes` of this task, and pin the matching
      `OsPath` in a named test.
- [x] `UrlPath` — the text quoted in the exit 37 message — is measured, not assumed: run
      `curl "file:///C:/dir/../nosuch.txt"` under curl 8.21.0, record the exact
      `curl: (37) Could not open file …` line, and pin it in a
      `FileProtocolHandlerTests` test that asserts
      `TransferResult.ErrorMessage` byte for byte for that URL.
- [x] An overload `FileUrlPath.TryParse(Uri url, bool pathAsIs, out FileUrlPath path)`
      exists; the existing two-argument overload behaves as `pathAsIs: false`; a test
      named `TryParse_PathAsIs_KeepsDotDotButStillConvertsBackslashes` asserts that with
      `pathAsIs: true` the `..` survives in `OsPath` while `\` is still a separator.
- [x] `TryParse_Backslashes_SurviveUnnormalised` and
      `TryParse_DotDotSegment_SurvivesUnresolved` are deleted from
      `FileUrlPathTests.cs` — not `[Ignore]`d — and the commit message names both.
- [x] The ADR-0003 bullet quoted in `Context` is replaced by text stating the measured
      behaviour, dated, citing curl 8.21.0 and `--path-as-is`; the `OsPath` parameter
      documentation and step 7 of the `TryParse` remarks in `FileUrlPath.cs` no longer
      claim dot segments survive.
- [x] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green, with no test carrying `[TestCategory("Integration")]`.

## Notes

`--path-as-is` is not wired to a command line option in this task: no option parsing
exists yet, and nothing on `ITransferContext` carries the flag. Do not add a member to
`Curl.Protocol.Abstractions.UnitLibrary` here — the `pathAsIs` parameter is the seam, and
it exists so both behaviours are pinned by tests now. Wiring the option belongs with the
command line work.

The ADR-0003 correction is an `align-and-document` edit and can be done in the document stage of
the same `/feature` run; it does not need a separate task.

### Delivered (2026-09-26, dark factory lane 4)

Measured with `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel` on this machine:

- `curl -s -o out.txt -w "%{exitcode}" "file:///C:/../../Windows/win.ini"` prints `0`,
  and `out.txt` holds `C:\Windows\win.ini`. `file:///C:/../../nosuch.txt` exits 37
  quoting `C:/nosuch.txt`: the drive is the root, and a `..` with nothing above it is
  dropped. Pinned in `TryParse_DotDotAboveTheDrive_StopsAtTheDrive`.
- `curl "file:///C:/dir/../nosuch.txt"` prints exactly
  `curl: (37) Could not open file C:/nosuch.txt` — the message quotes the path after
  dot removal. Pinned in
  `ExecuteAsync_DotDotSourceNotFound_QuotesThePathWithTheDotDotRemoved`.
- A drive is a root only when `/` or nothing follows it:
  `file://localhost/Q:dir/../x` quotes `/x`, `file://localhost/C:` quotes `C:`.
- `%2e` in either case counts as a dot: `.%2e`, `%2E%2E` are `..`, `%2e` is `.`;
  `...` is an ordinary segment.
- `%5c` is not a separator: `file:///C:/dir%5c..%5cnosuch.txt` is quoted unresolved.
- Backslashes in the authority position convert too:
  `file://localhost\C:/dir/../nosuch.txt` quotes `C:/nosuch.txt`.
- `--path-as-is` keeps `.`/`..`/`%2e%2e` segments and still converts `\`.
- A UNC server name is not a protected root: `file:////server/../x` quotes `//x`, and
  `file:////server/share/../../../x` quotes `/x`.
- Review correction: .NET 10 `Uri` converts `\` to `/` in a `file` URL too, so the
  ADR-0003 text says both do; the difference is only that `Uri` removes dot segments
  unconditionally.

Choices made without Stewart, with why:

- `UrlPath` now holds the normalised path (backslashes converted, dot segments
  removed), not the text as written, because that is what curl quotes in the exit 37
  message (measured above).
- Dot removal runs before the drive-letter slash is stripped, on the path with its
  leading `/`, so `/Q:dir/../x` becomes `/x` as curl's does; a drive followed by `/` or
  the end is kept as the root.
- `file:///C:` and `file:///Q:dir/../x` make `System.Uri` throw, so those two tests use
  the `file://localhost/` spelling, which curl treats identically (measured).
- FR-012 in `Documentation/Product/Requirements.md` (in `touches`) was rewritten to
  state what is implemented and that `--path-as-is` option wiring is still open.

Found and filed, not done here: curl uppercases the hex digits of the escapes it quotes
(`a%2eb` is quoted `a%2Eb`, `%5c` as `%5C`, `%2f` as `%2F`); `UrlPath` keeps them as
written. Filed as BL-056.

Coverage after the change: every new line and branch in `FileUrlPath.cs` is covered.
The gaps that remain in the library (`FileUrlPath.AfterScheme` line 247,
`FileProtocolHandler.cs` lines 285-287, 482-484, 691) predate this task.

## Log

- 2026-09-25: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. file:// paths convert backslashes then remove dot segments as curl 8.21.0 does, with a pathAsIs switch
