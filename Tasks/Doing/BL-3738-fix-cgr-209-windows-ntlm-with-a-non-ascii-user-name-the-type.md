---
id: BL-3738
title: Fix CGR-209: Windows NTLM with a non-ASCII user name: the Type-3 user field differs from the SSPI build
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/HandBuiltNtlmSecurityContext.cs, Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs, Curl.Authentication.UnitTests/Cgr209Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3738 — Fix CGR-209: Windows NTLM with a non-ASCII user name: the Type-3 user field differs from the SSPI build

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-209 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-209: Windows NTLM with a non-ASCII user name: the Type-3 user field differs from the SSPI build

- **Severity:** Low
- **Area:** http-stateful
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0003
- **Source findings:** `http-stateful HS-19`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- 401 `WWW-Authenticate: NTLM`, then a Type-2 challenge, then 200. Run `curl -sS --ntlm -u 'ÜSER:päss' http://127.0.0.1:20500/p`. Case: `EV\cases\nt-_SER_p_ss-82`. The Type-3 decoder is in `EV\c19_ntlm.ps1`.

Commands (run each with REF and with PORT):

```sh
curl -sS --ntlm -u 'ÜSER:päss' http://127.0.0.1:20500/p
EV\c19_ntlm.ps1
```

**Expected (real curl)**

- the Type-3 user field is UTF-16LE `84 25 53 00 45 00 52 00` (`▄SER`): the ANSI byte 0xDC read through the OEM code page.

**Actual (Curl)**

- the user field is `DC 00 53 00 45 00 52 00` (`ÜSER`). The flags (`A2888205`) and the domain match.

**Suspected location in source**

- `Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs` / `HandBuiltNtlmSecurityContext.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr209Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-209 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
