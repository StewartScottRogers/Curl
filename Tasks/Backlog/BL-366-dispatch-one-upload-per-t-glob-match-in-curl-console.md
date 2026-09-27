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
completed:
---
# BL-366 — Dispatch one upload per -T glob match in Curl.Console

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

- [ ] A test in `Curl.Console.UnitTests` shows `-T '{local.txt,sub/in.txt}' http://h/g/` uploads
      `local.txt` to `http://h/g/local.txt` and then `sub/in.txt` to `http://h/g/in.txt`, over fakes.
- [ ] A test shows `-T 'f[3-1].txt' http://h/g/` exits 3 with curl's measured message.
- [ ] A test shows `-g -T '{local.txt,sub/in.txt}' http://h/g/` tries the one literal file (exit 26
      when it is missing).
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports 100% line and branch and no failing member for `Curl.Console`.

## Notes

- Filed by BL-031 (2026-09-27): BL-031 stayed inside `Curl.Cli`, since `Curl.Console` was held by BL-328.
- Depends on BL-240 so the URL glob and the upload glob nest in one change to the runner.

## Log

- 2026-09-27: Created.
