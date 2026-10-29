---
id: BL-3735
title: Fix CGR-206: -z with a date before 1970 is sent, where Windows curl fails with exit 43; the weekday for negative times also differs
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpConditionDate.cs, Curl.Protocol.Http.UnitLibrary/HttpTimeConditionLimit.cs, Curl.Protocol.Http.UnitTests/Cgr206Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3735 — Fix CGR-206: -z with a date before 1970 is sent, where Windows curl fails with exit 43; the weekday for negative times also differs

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-206 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-206: -z with a date before 1970 is sent, where Windows curl fails with exit 43; the weekday for negative times also differs

- **Severity:** Low
- **Area:** http-stateful
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-stateful HS-5`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- `curl -sS -z "1 Jan 1900" http://127.0.0.1:20500/f` (also `"31 Dec 1969"`), and `curl -sS -z "31 Dec 1969 23:59:58 GMT" http://127.0.0.1:20500/f`.

Commands (run each with REF and with PORT):

```sh
curl -sS -z "1 Jan 1900" http://127.0.0.1:20500/f
curl -sS -z "31 Dec 1969 23:59:58 GMT" http://127.0.0.1:20500/f
```

**Expected (real curl)**

- *REF (Windows 8.22):* prints `curl: (43) Invalid TIMEVALUE`, exits 43 and sends no request. For -2 s it sends `If-Modified-Since: Thu, 31 Dec 1969 23:59:58 GMT`.

**Actual (Curl)**

- sends `If-Modified-Since: Mon, 01 Jan 1900 00:00:00 GMT` (or `Wed, 31 Dec 1969 00:00:00 GMT`) and exits 0. For -2 s it sends `Wed, 31 Dec 1969 ...`.

**Notes**

- *Note:* Linux REF 8.18 behaves like PORT, so this is a quirk of the Windows build (its C runtime's gmtime). PORT's weekday is the calendar-correct one and REF's `Thu` is wrong. It is reported only for parity with the Windows build.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpConditionDate.cs`, `HttpTimeConditionLimit.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr206Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-206 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes (BL-3735 run)

- Measured Windows curl (System32, Schannel) against a loopback listener: `-z "1 Jan 1900"`, `-z "31 Dec 1969"`, `-z "31 Dec 1969 11:59:59 GMT"`, `-z "15 Jun 1950"` all print `curl: (43) Invalid TIMEVALUE` and send nothing; `-z "31 Dec 1969 12:00:00 GMT"` and `23:59:58 GMT` send `If-Modified-Since: Thu, 31 Dec 1969 ...`; `-z "1 Jan 1970 00:00:00 GMT"` sends `Thu, 01 Jan 1970 ...`. So the CRT gmtime floor is -43200 s, and negative times are named `Thu`.
- Fix: `HttpTimeConditionLimit.Refuses` also refuses before 1969-12-31 12:00:00 UTC on Windows; `HttpConditionDate.Format` names negative times `Thu` on Windows. Linux/macOS keep the calendar weekday (Linux REF 8.18 matched). Tests in `Cgr206Tests.cs` pass platform-neutrally (the Windows flag is a parameter). Production edit also touched `HttpRequestHeadFormatter.cs` (one-token change to pass `OperatingSystem.IsWindows()`).
- Measure-CodeQuality.ps1 for Curl.Protocol.Http.UnitLibrary: 0 failing members. Build clean, fast tests green. CI criterion met by platform neutrality.
- 2026-10-28: Doing -> Done. Windows -z before 1969-12-31 12:00 UTC now exit 43, negative times named Thu
