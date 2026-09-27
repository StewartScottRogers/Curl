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
completed:
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

- [ ] A test in `Curl.Cli.UnitTests` shows `-T '{local.txt,sub/in.txt}' http://h/g/`
      dispatches `local.txt` to `http://h/g/local.txt` first and `sub/in.txt` to
      `http://h/g/in.txt` second.
- [ ] A test shows a range glob, `-T 'f[1-3].txt' http://h/g/`, dispatches `f1.txt`,
      `f2.txt`, `f3.txt` in that order, each to `http://h/g/<name>`.
- [ ] Each expanded name is resolved through
      `UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile`; no second resolver exists.
- [ ] A test shows `--globoff -T '{local.txt,sub/in.txt}' http://h/g/` performs one
      upload of the literal file name `{local.txt,sub/in.txt}`.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green; no test needs
      `TestCategory=Integration`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
