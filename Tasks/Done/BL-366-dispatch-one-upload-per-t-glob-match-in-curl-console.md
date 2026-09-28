---
id: BL-366
title: Dispatch one upload per -T glob match in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-031, BL-240]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: FR-005
created: 2026-09-27
completed: 2026-09-27
---
# BL-366 â€” Dispatch one upload per -T glob match in Curl.Console

## Goal

`CurlCommandRunner` expands each `-T` argument with `Curl.Cli.UploadFileGlob` and runs one
upload per match, upload files as the outer loop and the URL glob inner, as curl 8.21.0 does.

## Context

- BL-031 delivered `UploadFileGlob` (`Curl.Cli.UnitLibrary/UploadFileGlob.cs`): `TryParse(uploadFile,
  globOff, ...)` (exit 3 with the glob position message), `ExpandUploadFiles()` and
  `ResolveTransferTargets(url)`, each target resolved through `UploadTransferUrl.TryResolve`.
- Today `CurlCommandRunner` (around line 478) resolves the single `-T` value with
  `UploadTransferUrl.TryResolve`; URL globbing itself is wired by BL-240.
- Measured on curl 8.21.0 (mingw, Schannel), 2026-09-27, with
  `curl -s -S -w '%{url_effective}
' -T '<glob>' <url>` (BL-031 Notes):
  `-T '{local.txt,sub/in.txt}' 'http://h/{x,y}/'` runs x/local.txt, y/local.txt, x/in.txt,
  y/in.txt; `-T '{local.txt,nosuch}' http://h/g/` prints `curl: cannot open 'nosuch'` and the
  try-help line for the missing match yet still reports the run's last transfer error;
  `-T 'f[3-1].txt'` exits 3 with `bad range in position 7:` and a caret under the `-T` text;
  `-g` takes the braces literally.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` shows `-T '{local.txt,sub/in.txt}' http://h/g/` uploads
      `local.txt` to `http://h/g/local.txt` and then `sub/in.txt` to `http://h/g/in.txt`, over fakes.
- [x] A test shows `-T 'f[3-1].txt' http://h/g/` exits 3 with curl's measured message.
- [x] A test shows `-g -T '{local.txt,sub/in.txt}' http://h/g/` tries the one literal file (exit 26
      when it is missing).
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports 100% line and branch and no failing member for `Curl.Console`.

## Notes

- Filed by BL-031 (2026-09-27): BL-031 stayed inside `Curl.Cli`, since `Curl.Console` was held by BL-328.
- Depends on BL-240 so the URL glob and the upload glob nest in one change to the runner.
- 2026-09-27 (lane 2): `CurlCommandRunner.TransferEachUrlAsync` now reads each URL's `-T`
  value with `UploadFileGlob.TryParse` (honouring `-g`) before the URL glob, as curl reads it
  first; a malformed one is written like a bad URL glob (exit 3, message and caret about the `-T`
  text). `TransferEachMatchAsync` loops upload files outer, URL glob matches inner.
  `UrlTransfer` gained `UploadFile` (one match), which `TransferUrlAsync` now uses in place of
  `UploadFileOf`. No ADR: every behaviour was measured in BL-031, none chosen.
- Choice (sensible default): a match that cannot be opened still ends the run, as a lone
  missing `-T` file does; curl's glob behaviour there is not fully measured, so it is filed as
  BL-440 rather than guessed.
- Tests: `CurlCommandRunnerUploadTests` gained 4 (glob order, upload-outer/URL-inner nesting,
  `f[3-1].txt` exit 3, `-g` literal exit 26). Gates: `dotnet build -warnaserror` clean; fast tests
  0 failed (Curl.Console.UnitTests 865 passed). `Measure-CodeQuality.ps1 -Library Curl.Console`
  reports 99.49% on the fast run only because `DiskWriteOutFileOpener.TryOpen` (untouched) is
  covered by its Integration tests (BL-280); with `-IncludeIntegration` it is 100% line, 100%
  branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Console runs one upload per -T glob match, upload files outer and URL glob inner; a malformed -T glob exits 3 with curl's message; -g takes it literally
