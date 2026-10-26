---
id: BL-3602
title: Fix CGR-073: -z "=DATE" sends If-Modified-Since; curl sends Last-Modified
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr073Tests.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.TransferOptions.cs, Curl.Cli.UnitTests/CommandLineTimeConditionOptionTests.cs, Curl.Console/EasyOptionsFromCommandLine.cs, Curl.Console.UnitTests/EasyOptionsFromCommandLineTests.Behaviour.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3602 — Fix CGR-073: -z "=DATE" sends If-Modified-Since; curl sends Last-Modified

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-073 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-073: -z "=DATE" sends If-Modified-Since; curl sends Last-Modified

- **Severity:** Medium
- **Area:** http-stateful
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-stateful HS-4`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- `curl -sS -z "=Wed, 21 Oct 2015 07:28:00 GMT" http://127.0.0.1:20500/f`. `==DATE` gives the same result. Case: `EV\cases\z-_Wed__21_Oct_2015_07_28_00_GMT-799`.

Commands (run each with REF and with PORT):

```sh
curl -sS -z "=Wed, 21 Oct 2015 07:28:00 GMT" http://127.0.0.1:20500/f
```

**Expected (real curl)**

- sends the request header `Last-Modified: Wed, 21 Oct 2015 07:28:00 GMT` (CURL_TIMECOND_LASTMOD).

**Actual (Curl)**

- sends the request header `If-Modified-Since: Wed, 21 Oct 2015 07:28:00 GMT`.

**Suspected location in source**

- `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:1684-1685`. `SetTimeCondition` strips a leading `=` and handles it like `+`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr073Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 1): Fix. `SetTimeCondition` now sets `TimeCondition.SentAsLastModified` for a leading `=` (curl's `CURL_TIMECOND_LASTMOD`, ADR-0781's flag, which the engine and HTTP formatter already honoured); `EasyOptionsFromCommandLine.WithConditions` maps it to `EasyTimeCondition.LastModified`, and `--libcurl` writes `CURL_TIMECOND_LASTMOD` (measured: real curl 8.21.0 `-z "=..." --libcurl -` writes it). No new ADR: this is a conformance fix inside ADR-0781's existing design.
- Touches widened to `LibcurlSourceCode.TransferOptions.cs`, `CommandLineTimeConditionOptionTests.cs` (its `=` rows compared whole records and moved to `Cgr073Tests`), `Curl.Console/EasyOptionsFromCommandLine.cs` and its `.Behaviour.cs` test: the condition reaches the wire through the Console mapping. No task in Doing on origin/work/dark-factory names any of them.
- Repro, Windows, real curl 8.21.0 (System32) vs Curl, `Record-CurlExchange.ps1 -Port 20587 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok' -CurlArgs -sS,-z,"<v>Wed, 21 Oct 2015 07:28:00 GMT",http://127.0.0.1:20587/f`: for `<v>` = `=` and `==` both send `Last-Modified: Wed, 21 Oct 2015 07:28:00 GMT`; for none and `+` both send `If-Modified-Since`, for `-` both `If-Unmodified-Since`. Exit code (0), stdout (`ok`), stderr (empty) and request bytes are identical for all five.
- `Cgr073Tests`: 4 of 9 cases failed before the fix, all pass after. Fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Console`: 0 failing members. Linux/macOS: the change is platform-neutral (no paths, OS text or certificates) and the fast tests are green here.

## Log

- 2026-10-26: Created from CGR-073 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -z =DATE and ==DATE send Last-Modified and --libcurl writes CURL_TIMECOND_LASTMOD, as curl does
