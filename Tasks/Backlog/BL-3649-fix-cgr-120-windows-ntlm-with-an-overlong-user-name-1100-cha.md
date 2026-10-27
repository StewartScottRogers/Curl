---
id: BL-3649
title: Fix CGR-120: Windows --ntlm with an overlong user name (1100+ characters): REF fails with exit 67 before sending anything; PORT sends a request with n...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests/Cgr120Tests.cs, Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests/Cgr120Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3649 — Fix CGR-120: Windows --ntlm with an overlong user name (1100+ characters): REF fails with exit 67 before sending anything; PORT sends a request with n...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-120 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-120: Windows --ntlm with an overlong user name (1100+ characters): REF fails with exit 67 before sending anything; PORT sends a request with no Authorization header and exits 0

- **Severity:** Medium
- **Area:** upstream-cases
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0003
- **Source findings:** `upstream-cases UC-7`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- `verify\vwin.ps1` (case `v_ntlm775`). The server replies `HTTP/1.1 401 X\r\nWWW-Authenticate: NTLM\r\nContent-Length: 0\r\n\r\n`. Run `curl -s -S http://127.0.0.1:21372/775 -u testuser<1100 x A>:testpass --ntlm`. Linux check: `verify\v4.sh`.

Commands (run each with REF and with PORT):

```sh
curl -s -S http://127.0.0.1:21372/775 -u testuser<1100 x A>:testpass --ntlm
```

**Expected (real curl)**

- REF-WIN: exit 67, `curl: (67) Login denied`, no bytes sent.
- REF-LINUX: sends an NTLM type-1 Authorization header.

**Actual (Curl)**

- PORT-WIN: `GET /775 HTTP/1.1` with no Authorization header, then exit 0 on the 401, with nothing on stderr. This matches neither REF build.
- PORT-LINUX: matches REF-LINUX (an Authorization header is sent).

**Suspected location in source**

- NTLM type-1 creation on Windows (`Curl.Ntlm.UnitLibrary` / `Curl.Authentication.UnitLibrary`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr120Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-120 of the third-party gap report.
