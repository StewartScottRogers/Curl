---
id: BL-3584
title: Fix CGR-055: -G/--url-query with --path-as-is: REF re-parses the URL and removes dot segments, but PORT keeps them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/QueryUrl.cs, Curl.Cli.UnitTests/Cgr055Tests.cs, Curl.Console.UnitTests/Cgr055Tests.cs, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3584 — Fix CGR-055: -G/--url-query with --path-as-is: REF re-parses the URL and removes dot segments, but PORT keeps them

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-055 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-055: `-G`/`--url-query` with `--path-as-is`: REF re-parses the URL and removes dot segments, but PORT keeps them

- **Severity:** Medium
- **Area:** url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** AF-0426, AF-0248
- **Source findings:** `url-parsing UP-08`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- `curl -sS -G -d a=b --path-as-is "http://127.0.0.1:20200/x/../y"` (v08_G_pai; Linux lv08_G_pai)
- `curl -sS --url-query a=b --path-as-is "http://127.0.0.1:20200/x/../y"` (v08b_uq_pai)

Commands (run each with REF and with PORT):

```sh
curl -sS -G -d a=b --path-as-is "http://127.0.0.1:20200/x/../y"
curl -sS --url-query a=b --path-as-is "http://127.0.0.1:20200/x/../y"
```

**Expected (real curl)**

- `GET /y?a=b HTTP/1.1`, and url_effective is `http://127.0.0.1:20200/y?a=b`.

**Actual (Curl)**

- `GET /x/../y?a=b HTTP/1.1`

**Suspected location in source**

- `Curl.Cli.UnitLibrary/QueryUrl.cs:41` (a plain string append) and `Curl.Console/CurlCommandRunner.cs:3516`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr055Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-055 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
