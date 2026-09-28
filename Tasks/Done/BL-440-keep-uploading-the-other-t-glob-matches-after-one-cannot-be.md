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
completed: 2026-09-27
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

- [x] The measured stderr, stdout and exit code for `-T '{nosuch,local.txt}'` and
      `-T '{local.txt,nosuch}'` against a loopback server are recorded under Notes.
- [x] Tests in `Curl.Console.UnitTests` pin both orders over fakes with the measured lines and exit code.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes;
      `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration` reports 100% line and branch.

## Notes

- Filed by BL-366 (2026-09-27), which kept the single-file rule (a file that cannot be opened ends the run).
- Measured 2026-09-27, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1` on port 18440,
  `-w '%{url_effective} %{exitcode}\n'`, `local.txt` present, `nosuch` absent. **The title's
  premise is false: curl does not keep uploading the other matches.** A match that cannot be
  opened ends the run whatever its position, as a lone missing `-T` file does. What differs is
  its code: curl sets none of its own and reports the run's *previous transfer's* result.
  - `-T '{nosuch,local.txt}' http://127.0.0.1:18440/g/` (and with `:1`, unreachable): no request;
    stdout `http://127.0.0.1:18440/g/nosuch 26`; stderr `curl: cannot open 'nosuch'`, the try-help
    line, `curl: (26) Failed to open/read local data from file/application`; exit 26. Same with a
    URL glob `{a,b}`: only `/a/nosuch`, then the run ends.
  - `-T '{local.txt,nosuch}' http://127.0.0.1:18440/g/`: `PUT /g/local.txt` sent; stdout
    `.../g/local.txt 0` then `.../g/nosuch 26`; stderr the progress meter, then the same three
    lines; exit 26. `{local.txt,nosuch,local.txt}`: identical, the third match is not sent.
  - `-T '{local.txt,nosuch}' http://127.0.0.1:1/g/`: stdout `.../g/local.txt 7` then
    `.../g/nosuch 7`; stderr `curl: (7) Failed to connect to 127.0.0.1:1 after 2032 ms: Could not
    connect to server`, `curl: cannot open 'nosuch'`, the try-help line, `curl: (7) Could not
    connect to server`; exit 7. `%{errormsg}` for `nosuch` is `Could not connect to server`,
    the `curl_easy_strerror` text, not the previous transfer's detailed message.
  - It is the previous transfer, not the last failure: `-s -T '{local.txt,nosuch}'
    'http://127.0.0.1:{1,18440}/g/'` (7, 0, then nosuch) exits 26; `{18440,1}` (0, 7, then nosuch)
    exits 7. It crosses command-line URLs: `-T local.txt -T nosuch http://127.0.0.1:1/a/
    http://127.0.0.1:1/b/` exits 7 with `curl: (7) Could not connect to server`.
- Implemented: `CurlCommandRunner` remembers the previous transfer's result; a `-T` file that
  cannot be opened after a failed transfer returns that code with `CurlEasyErrorText.Of(code)`
  and still ends the run. `CurlEasyErrorText` holds every `curl_easy_strerror` text, read from
  the strings in 8.21.0's `libcurl-4.dll` (so measured, not recalled; note code 66 is spelled
  "initialize"). No ADR: this pins measured behaviour, no design choice was made.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A -T file or glob match that cannot be opened after a failed transfer now reports that transfer's code and curl's text, as curl 8.21.0 does
