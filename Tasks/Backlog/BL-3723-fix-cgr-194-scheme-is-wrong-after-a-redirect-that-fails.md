---
id: BL-3723
title: Fix CGR-194: %{scheme} is wrong after a redirect that fails
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr194Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3723 — Fix CGR-194: %{scheme} is wrong after a redirect that fails

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-194 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-194: `%{scheme}` is wrong after a redirect that fails

- **Severity:** Low
- **Area:** url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `url-parsing UP-17`

PORT prints `http` where REF prints the target's scheme, and prints nothing when `--proto-redir` refuses the redirect.

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- `curl -sS -L "http://127.0.0.1:20200/__loc=ftp://127.0.0.1:20299/y" -w "[%{scheme}]"` (v17_scheme_redir)
- `curl -sS -L --proto-redir =https "http://127.0.0.1:20200/__loc=http://127.0.0.1:20200/y" -w "[%{scheme}]"` (v17b_scheme_redir_dis)
- Linux cases: lv17_*.

Commands (run each with REF and with PORT):

```sh
curl -sS -L "http://127.0.0.1:20200/__loc=ftp://127.0.0.1:20299/y" -w "[%{scheme}]"
curl -sS -L --proto-redir =https "http://127.0.0.1:20200/__loc=http://127.0.0.1:20200/y" -w "[%{scheme}]"
```

**Expected (real curl)**

- `[ftp]` (exit 7) and `[http]` (exit 1).

**Actual (Curl)**

- `[http]` and `[]`.

**Suspected location in source**

- `Curl.Core.UnitLibrary/RedirectFollower.cs`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr194Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-194 of the third-party gap report.
