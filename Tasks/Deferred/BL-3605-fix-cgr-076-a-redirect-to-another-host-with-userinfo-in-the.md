---
id: BL-3605
title: Fix CGR-076: A redirect to another host with userinfo in the Location sends Basic credentials even though the user set a custom Authorization header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3723]
touches: [Curl.Original.Protocol.Http.UnitLibrary, Curl.Original.Protocol.Http.UnitTests/Cgr076Tests.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Cgr076.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr076RedirectFollowerTests.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3605 — Fix CGR-076: A redirect to another host with userinfo in the Location sends Basic credentials even though the user set a custom Authorization header

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-076 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-076: A redirect to another host with userinfo in the Location sends Basic credentials even though the user set a custom Authorization header

- **Severity:** Medium
- **Area:** http-stateful
- **Platform(s):** Both (Windows and Linux). Detail: Both (the original report said Windows only; the verifier also reproduced it on Linux, where REF 8.18 likewise strips the header and sends no Basic)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-stateful HS-10`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- `/a` returns `302` with `Location: http://x:y@127.0.0.1:20501/ok`. Run `curl -sSL -H 'Authorization: Bearer xyz' -H 'Cookie: c=1' -H 'X-Custom: 1' http://127.0.0.1:20500/a`. Case: `EV\cases\authr-userinfo-loc-H`.

Commands (run each with REF and with PORT):

```sh
curl -sSL -H 'Authorization: Bearer xyz' -H 'Cookie: c=1' -H 'X-Custom: 1' http://127.0.0.1:20500/a
```

**Expected (real curl)**

- the second request (port 20501) has no Authorization header. The custom header is stripped and no Basic header is generated.

**Actual (Curl)**

- the second request carries `Authorization: Basic eDp5` (x:y).

**Suspected location in source**

- redirect credential handling in `Curl.Original.Protocol.Http.UnitLibrary` (the follow logic). Not pinned to a line.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr076Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-28 (lane 4, resumed run): the fix needs the follower to tell the HTTP handler that it withheld an `-H Authorization` value, so it adds `HttpRequestOptions.AuthorizationHeaderWithheld` (Abstractions), sets it in `RedirectFollower.cs` (Core) and honours it in `HttpRequestHeadFormatter.AppendAuthorization` and `HttpProtocolHandler.ReportAuthorizationLines`; tests in `Cgr076Tests.cs`, `HttpProtocolHandlerTests.Cgr076.cs` and `Curl.Core.UnitTests/Cgr076RedirectFollowerTests.cs`. Those files were added to touches. `RedirectFollower.cs` is held by BL-3723 (Doing), so the task waits on it; the code is written but not yet built, tested or quality-measured, and is left uncommitted for the shift to stash. Next claim: apply the stash, build, run the fast tests, run Measure-CodeQuality.ps1 for Core, Abstractions and Http, measure real curl for the repro and record it here.

## Log

- 2026-10-26: Created from CGR-076 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Waits on BL-3723, which holds Curl.Core.UnitLibrary/RedirectFollower.cs; the fix is written and left for the stash
- 2026-10-28: Backlog -> Deferred. Split into BL-4002 and BL-4003 (over its cost cap)
