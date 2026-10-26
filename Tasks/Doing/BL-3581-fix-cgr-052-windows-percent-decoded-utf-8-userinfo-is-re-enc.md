---
id: BL-3581
title: Fix CGR-052: Windows: percent-decoded UTF-8 userinfo is re-encoded to the ANSI code page in Basic auth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/BasicAndBearerAuthenticator.cs, Curl.Authentication.UnitTests/Cgr052Tests.cs, Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitTests/Cgr052Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3581 — Fix CGR-052: Windows: percent-decoded UTF-8 userinfo is re-encoded to the ANSI code page in Basic auth

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-052 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-052: Windows: percent-decoded UTF-8 userinfo is re-encoded to the ANSI code page in Basic auth

- **Severity:** Medium (Corrected from High, because non-ASCII credentials inside the URL are uncommon. `-u` with non-ASCII matches REF)
- **Area:** url-parsing
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `url-parsing UP-03`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- `curl -sS "http://%C3%A9:%E2%82%AC@127.0.0.1:20200/a"` (v03_user_pct_utf8)
- The same URL written as raw UTF-8 in a `-K` config file (v03b_cfg_user_utf8).

Commands (run each with REF and with PORT):

```sh
curl -sS "http://%C3%A9:%E2%82%AC@127.0.0.1:20200/a"
```

**Expected (real curl)**

- `Authorization: Basic w6k64oKs`, which is the bytes C3 A9 3A E2 82 AC.

**Actual (Curl)**

- `Authorization: Basic 6TqA`, which is the bytes E9 3A 80 (CP1252).

**Notes**

- *Linux:* PORT-LINUX matches REF (lv03_user_utf8 SAME).

**Suspected location in source**

- `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:347` (`Uri.UnescapeDataString`) and `Curl.Authentication.UnitLibrary/BasicAndBearerAuthenticator.cs:43` (`CredentialEncoding.ForPlatform`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr052Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-052 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
