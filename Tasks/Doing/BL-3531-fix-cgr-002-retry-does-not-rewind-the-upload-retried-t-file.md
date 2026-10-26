---
id: BL-3531
title: Fix CGR-002: --retry does not rewind the upload: retried -T <file> PUTs are sent with Content-Length 0 and exit 0 (silent data loss), and retried -F f...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/TransferRetrier.cs, Curl.Core.UnitTests/Cgr002Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Protocol.Http.UnitTests/Cgr002Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3531 — Fix CGR-002: --retry does not rewind the upload: retried -T <file> PUTs are sent with Content-Length 0 and exit 0 (silent data loss), and retried -F f...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-002 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-002: `--retry` does not rewind the upload: retried `-T <file>` PUTs are sent with Content-Length 0 and exit 0 (silent data loss), and retried `-F` forms fail with exit 26

- **Severity:** Critical
- **Area:** http-stateful
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [http-stateful HS-1] Confirmed (re-run on Windows and Linux); [http-stateful HS-2] Confirmed (Windows and Linux)
- **Related existing finding (GF/AF):** GF-0005
- **Source findings:** `http-stateful HS-1`, `http-stateful HS-2`

Same root cause: `Curl.Core.UnitLibrary/TransferRetrier.cs:96-98` calls `transfer(context)` again with the same context and never rewinds the upload source.

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- **[http-stateful HS-1]** --retry with -T <file> resends an empty body; every retried PUT has Content-Length: 0 and curl exits 0
  - Routes: `p.1.http` = `HTTP/1.1 503 X`, `Content-Length: 2`, body `e\n`. `p.2.http` = `200 OK`, body `ok\n`.
  - Commands: `printf upload > u.txt`, then `curl -sS --retry 2 --retry-delay 1 -T u.txt http://127.0.0.1:20500/p`.
  - Also fails with `--retry-all-errors` after a dropped connection (case rT-drop-all) and with `-L --retry 1` across a 307 (case rT-retry-L).
  - Cases: `EV\cases\retry-T`, `rT-again`, `rT-drop-all`, `rT-retry-L`. Replay: `pwsh EV\verify.ps1 retry-T,rT-drop-all,rT-retry-L`.
- **[http-stateful HS-2]** --retry with any -F form fails on the retry with exit 26 "client mime read EOF fail"
  - the same 503-then-200 routes as HS-1. Run `curl -sS --retry 2 --retry-delay 1 -F f=@u.txt http://127.0.0.1:20500/p` and `curl -sS --retry 2 --retry-delay 1 -F f=value http://127.0.0.1:20500/p`. Cases: `EV\cases\rF`, `rF-str`.

Commands (run each with REF and with PORT):

```sh
# [http-stateful HS-1]
printf upload > u.txt
curl -sS --retry 2 --retry-delay 1 -T u.txt http://127.0.0.1:20500/p
pwsh EV\verify.ps1 retry-T,rT-drop-all,rT-retry-L
# [http-stateful HS-2]
curl -sS --retry 2 --retry-delay 1 -F f=@u.txt http://127.0.0.1:20500/p
curl -sS --retry 2 --retry-delay 1 -F f=value http://127.0.0.1:20500/p
```

**Expected (real curl)**

- **[http-stateful HS-1]** every attempt sends `PUT /p`, `Content-Length: 6` and the body `upload`. Stdout `e\nok\n`, exit 0.
- **[http-stateful HS-2]** resends the whole multipart body (Content-Length 198 or 153) and gets the 200. Stdout `e\nok\n`, exit 0.

**Actual (Curl)**

- **[http-stateful HS-1]** 
  - Attempt 1 sends `Content-Length: 6` and `upload`. Attempt 2 and every later one sends `PUT /p` with `Content-Length: 0` and no body.
  - Exit is 0, so the server stores an empty file and nothing reports an error.
  - With -L, both retried hops (/p and /q) are sent empty.
  - `-T -` (stdin) matches REF.
- **[http-stateful HS-2]** sends only the first request, then prints `curl: (26) client mime read EOF fail, only 0/153 of needed bytes read` (0/198 for the file form). Exit 26, stdout `e\n`.

**Suspected location in source**

- **[http-stateful HS-1]** `Curl.Core.UnitLibrary/TransferRetrier.cs:96-98`. The attempt loop calls `transfer(context)` again with the same context and never rewinds the upload source.
- **[http-stateful HS-2]** `Curl.Core.UnitLibrary/TransferRetrier.cs:96-98` (no rewind between attempts). The message comes from `Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr002Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-002 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
