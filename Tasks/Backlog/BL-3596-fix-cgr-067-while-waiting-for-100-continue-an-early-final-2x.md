---
id: BL-3596
title: Fix CGR-067: While waiting for 100 Continue, an early final 2xx response makes Curl drop the request body. curl sends the body anyway.
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Protocol.Http.UnitTests/Cgr067Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3596 — Fix CGR-067: While waiting for 100 Continue, an early final 2xx response makes Curl drop the request body. curl sends the body anyway.

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-067 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-067: While waiting for 100 Continue, an early final 2xx response makes Curl drop the request body. curl sends the body anyway.

- **Severity:** Medium
- **Area:** http-requests
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed: reproduced 2/2 on Windows and 2/2 on Linux.
- **Related existing finding (GF/AF):** GF-0025
- **Source findings:** `http-requests HR-7`

**Reproduction**

- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- Use a server that sends `HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nok\n` as soon as the request head arrives and keeps reading (harness `"early":true`). Run:
  - `printf abcde | curl -sS -T - http://127.0.0.1:20335/a`
  - `curl -sS -H "Expect: 100-continue" -d abcde http://127.0.0.1:20335/a`

  These are cases v7_*. The finder's cases are in `E\c18.json`, which includes a 1 MiB+1 `--data-binary` and `-T` where REF sends 1048770 bytes and PORT sends 193.

Commands (run each with REF and with PORT):

```sh
printf abcde | curl -sS -T - http://127.0.0.1:20335/a
curl -sS -H "Expect: 100-continue" -d abcde http://127.0.0.1:20335/a
```

**Expected (real curl)**

- After the 200 it still sends the body: `5\r\nabcde\r\n0\r\n\r\n` for `-T -`, and `abcde` for the `-d` case. Exits 0 with stdout `ok`.

**Actual (Curl)**

- Sends only the request head and no body bytes, even though it declared `Content-Length: 5` or chunked. Exits 0 with stdout `ok`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:64-74`. `WaitForContinueAsync` returns false when a final status arrives first, so the body is skipped. The same class's `StopsSending` (line 90) says that a 2xx should let the body go on.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr067Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-067 of the third-party gap report.
