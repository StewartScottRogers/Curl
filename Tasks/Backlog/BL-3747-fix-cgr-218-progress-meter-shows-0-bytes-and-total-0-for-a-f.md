---
id: BL-3747
title: Fix CGR-218: Progress meter shows 0 bytes (and Total 0) for a followed redirect response that has a body
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Protocol.Http.UnitTests/Cgr218Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3747 — Fix CGR-218: Progress meter shows 0 bytes (and Total 0) for a followed redirect response that has a body

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-218 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-218: Progress meter shows 0 bytes (and Total 0) for a followed redirect response that has a body

- **Severity:** Low
- **Area:** writeout-verbose, http-stateful
- **Platform(s):** Both (Windows and Linux). Detail: [http-stateful HS-11] Both (compared on Windows)
- **Confidence:** Confirmed. Detail: [writeout-verbose WV-14] Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** AF-0156, GF-0078
- **Source findings:** `writeout-verbose WV-14`, `http-stateful HS-11`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- **[writeout-verbose WV-14]** The progress meter row for a followed redirect hop shows Total 0 instead of the hop's Content-Length
  - `curl -L -o x http://127.0.0.1:20700/redir/1` (the 302 has `Content-Length: 3`).
- **[http-stateful HS-11]** The progress meter shows 0 bytes for every followed redirect response that has a body
  - `/a` returns `302` with `Location: /b` and the body `abc` (Content-Length 3). `/b` returns 200 with 6 bytes.
  - Command (no -s): `curl -L -o x http://127.0.0.1:20500/a`.
  - Routes: `EV\vroutes\prog`. Run: `pwsh EV\vrun.ps1 prog -L --output x http://127.0.0.1:20500/a`.

Commands (run each with REF and with PORT):

```sh
# [writeout-verbose WV-14]
curl -L -o x http://127.0.0.1:20700/redir/1
# [http-stateful HS-11]
curl -L -o x http://127.0.0.1:20500/a
pwsh EV\vrun.ps1 prog -L --output x http://127.0.0.1:20500/a
```

**Expected (real curl)**

- **[writeout-verbose WV-14]** A row `  0      3   0      0 ...`
- **[http-stateful HS-11]** the redirect hop's meter rows read `100      3 100      3 ...`.

**Actual (Curl)**

- **[writeout-verbose WV-14]** `  0      0   0      0 ...`
- **[http-stateful HS-11]** the redirect hop's meter rows read `  0      0   0      0 ...`. The final hop matches.

**Suspected location in source**

- **[writeout-verbose WV-14]** `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs` (the redirect body length is not reported).
- **[http-stateful HS-11]** `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, around lines 1968-1988. The ignored redirect body is not counted.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr218Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-218 of the third-party gap report.
