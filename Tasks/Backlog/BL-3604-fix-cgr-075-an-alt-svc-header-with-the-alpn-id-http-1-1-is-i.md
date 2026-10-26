---
id: BL-3604
title: Fix CGR-075: An Alt-Svc header with the ALPN id "http/1.1" is ignored; curl stores it as an h1 entry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/AltSvc/AltSvcAlpnToken.cs, Curl.Core.UnitTests/Cgr075Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3604 — Fix CGR-075: An Alt-Svc header with the ALPN id "http/1.1" is ignored; curl stores it as an h1 entry

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-075 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-075: An Alt-Svc header with the ALPN id "http/1.1" is ignored; curl stores it as an h1 entry

- **Severity:** Medium
- **Area:** http-stateful
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-stateful HS-7`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- An HTTPS server on 127.0.0.1:20502 (`20502s`, self-signed) whose response carries `Alt-Svc: http/1.1="localhost:20502"`.
- Command: `curl -sSk --alt-svc a.txt --resolve example.com:20502:127.0.0.1 https://example.com:20502/x`.
- Case: `EV\cases\alt-h1-other`. Script: `EV\c18_tls_state.ps1`.

Commands (run each with REF and with PORT):

```sh
curl -sSk --alt-svc a.txt --resolve example.com:20502:127.0.0.1 https://example.com:20502/x
EV\c18_tls_state.ps1
```

**Expected (real curl)**

- adds the line `h1 example.com 20502 h1 localhost 20502 "<expiry>" 0 0` to a.txt.

**Actual (Curl)**

- a.txt holds only the comment header lines. With a list such as `http/1.1=":20502", h2=":443"`, only the h2 entry is stored.

**Suspected location in source**

- `Curl.Core.UnitLibrary/AltSvc/AltSvcAlpnToken.cs:12-18` accepts only h1, h2 and h3. curl's alpn2alpnid also maps `http/1.1` to h1.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr075Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-075 of the third-party gap report.
