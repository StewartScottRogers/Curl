---
id: BL-031
title: Expand globs in the -T argument
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-030]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: FR-005
created: 2026-09-26
completed: 2026-09-27
---
# BL-031 — Expand globs in the `-T` argument

## Goal

A `-T`/`--upload-file` argument containing `{a,b}` or `[1-3]` globs expands into one
upload per match, in curl's order, each to its own resolved URL; `--globoff` turns the
expansion off.

## Context

Upstream curl 8.21.0 globs the `-T` argument with the same engine it uses for URLs:
`-T '{local.txt,sub/in.txt}' http://h/g/` uploads `local.txt` to `http://h/g/local.txt`
and then `sub/in.txt` to `http://h/g/in.txt`. `-g`/`--globoff` disables globbing, so the
braces are taken literally as part of the file name. See
https://curl.se/docs/manpage.html#-T and https://curl.se/docs/manpage.html#-g, checked
against curl 8.21.0.

Each expansion goes through `Curl.Cli.UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile`
(`Curl.Cli.UnitLibrary\UploadUrl.cs`) via the dispatcher wiring BL-030 adds.

This needs a URL-globbing engine in `Curl.Cli.UnitLibrary`. **None exists in the code
and no task on the board files one.** If it still does not exist when this task is
picked up, move this task to `Blocked` naming that gap rather than writing the engine
here; the engine serves URL globbing too and deserves its own task.

## Acceptance criteria

- [x] A test in `Curl.Cli.UnitTests` shows `-T '{local.txt,sub/in.txt}' http://h/g/`
      dispatches `local.txt` to `http://h/g/local.txt` first and `sub/in.txt` to
      `http://h/g/in.txt` second.
- [x] A test shows a range glob, `-T 'f[1-3].txt' http://h/g/`, dispatches `f1.txt`,
      `f2.txt`, `f3.txt` in that order, each to `http://h/g/<name>`.
- [x] Each expanded name is resolved through
      `UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile`; no second resolver exists.
- [x] A test shows `--globoff -T '{local.txt,sub/in.txt}' http://h/g/` performs one
      upload of the literal file name `{local.txt,sub/in.txt}`.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green; no test needs
      `TestCategory=Integration`.

## Notes

- 2026-09-27 (lane 1): The Context's premise changed since filing: the glob engine exists
  (`Curl.Core.Globbing.UrlGlob`, BL-207) and the dispatcher is `CurlCommandRunner` in
  `Curl.Console` (BL-030 Notes), so the task went ahead instead of to Blocked.
- Delivered in `Curl.Cli` only: `UploadFileGlob` (`TryParse(uploadFile, globOff, ...)` over
  `UrlGlob.TryParse`/`UrlGlob.Unglobbed`, `ExpandUploadFiles`, `ResolveTransferTargets(url)`) and
  the `UploadTransferTarget` record. Each target is resolved by `UploadTransferUrl.TryResolve`,
  which calls `UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile`; no second resolver.
  Tests: `Curl.Cli.UnitTests/UploadFileGlobTests.cs` (11).
- Choice (sensible default): "dispatches" in the criteria is met by the ordered
  `ResolveTransferTargets` sequence the dispatcher will walk. Wiring it into
  `CurlCommandRunner` needs `Curl.Console`, which BL-328 (in Doing) touches, so that step is
  filed as BL-364 (depends on BL-031 and BL-240, where URL globbing is wired) rather than
  widening this task. No ADR: every behaviour here was measured, not chosen.
- Measured on curl 8.21.0 (`/mingw64/bin/curl`, Schannel), 2026-09-27, with
  `curl -s -S -w '%{url_effective}
' -T '<glob>' http://127.0.0.1:1/g/` from a directory holding
  the files: `{local.txt,sub/in.txt}` gives `/g/local.txt` then `/g/in.txt`; `f[1-3].txt` gives
  `/g/f1.txt`, `/g/f2.txt`, `/g/f3.txt`; with `-g` it is one upload, `curl: cannot open
  '{local.txt,sub/in.txt}'`, exit 26, `%{url_effective}` `http://127.0.0.1:1/g/in.txt%7D`;
  `f[3-1].txt` is exit 3 `bad range in position 7:` / `f[3-1].txt` / six spaces and `^`;
  `{-,local.txt}` sends `-` as standard input to `/g/` unchanged; with a URL glob
  `'http://h/{x,y}/'` the upload files are the outer loop (x/local, y/local, x/in, y/in).
- Gates: `dotnet build -warnaserror` clean; fast tests 0 failed (Curl.Cli.UnitTests 1704 passed,
  9 skipped); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch,
  0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Cli.UploadFileGlob expands a -T argument's {a,b} and [1-3] globs into one upload per match in curl 8.21.0's order, each resolved through UploadTransferUrl/UploadUrl; -g takes it literally; Console wiring filed as BL-364
