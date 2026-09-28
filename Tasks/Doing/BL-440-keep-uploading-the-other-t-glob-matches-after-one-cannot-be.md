---
id: BL-440
title: Keep uploading the other -T glob matches after one cannot be opened
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-366]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: FR-005
created: 2026-09-27
completed:
---
# BL-440 — Keep uploading the other -T glob matches after one cannot be opened

## Goal

A `-T` glob match that cannot be opened ends only its own upload, and the run's exit code
and lines match curl 8.21.0, instead of ending the whole run as a lone missing `-T` file does.

## Context

- BL-366 wired `UploadFileGlob` into `CurlCommandRunner.TransferEachUrlAsync`: one transfer per
  upload file (outer) and URL glob match (inner). A match that cannot be opened returns
  `CannotOpenUploadFileFailure`, which `EndsTheRun` treats as the end of the run, as it does
  for a single `-T nosuchfile`.
- BL-031 Notes record that curl 8.21.0 (mingw, Schannel), for
  `-T '{local.txt,nosuch}' http://127.0.0.1:1/g/`, prints `curl: cannot open 'nosuch'` and the
  try-help line for the missing match "yet still reports the run's last transfer error". Which
  exit code wins, whether the later matches still run when the missing one comes first, and
  what `-w` prints for the missing match were not pinned.
- Measure with `Record-CurlExchange.ps1` (extend it for several transfers if needed) before
  changing behaviour: `{nosuch,local.txt}` and `{local.txt,nosuch}`, each with a reachable
  loopback URL and with an unreachable one, printing `%{url_effective} %{exitcode}`.

## Acceptance criteria

- [ ] The measured stderr, stdout and exit code for `-T '{nosuch,local.txt}'` and
      `-T '{local.txt,nosuch}'` against a loopback server are recorded under Notes.
- [ ] Tests in `Curl.Console.UnitTests` pin both orders over fakes with the measured lines and exit code.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes;
      `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration` reports 100% line and branch.

## Notes

- Filed by BL-366 (2026-09-27), which kept the single-file rule (a file that cannot be opened ends the run).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
