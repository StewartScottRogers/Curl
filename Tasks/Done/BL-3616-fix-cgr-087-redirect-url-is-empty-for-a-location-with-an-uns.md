---
id: BL-3616
title: Fix CGR-087: %{redirect_url} is empty for a Location with an unsupported scheme or raw UTF-8 bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr087Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3616 — Fix CGR-087: %{redirect_url} is empty for a Location with an unsupported scheme or raw UTF-8 bytes

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-087 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-087: %{redirect_url} is empty for a Location with an unsupported scheme or raw UTF-8 bytes

- **Severity:** Medium
- **Area:** writeout-verbose
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** GF-0008
- **Source findings:** `writeout-verbose WV-05`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- `curl -sS -w '%{redirect_url}\n' http://127.0.0.1:20700/raw/protoloc` (the response has `Location: foo://x/`).
- `curl -s -o NUL -w '%{redirect_url}|%header{location}\n' http://127.0.0.1:20700/raw/locutf` (the response has `Location: /caf\xc3\xa9?q=\xe9`).

Commands (run each with REF and with PORT):

```sh
curl -sS -w '%{redirect_url}\n' http://127.0.0.1:20700/raw/protoloc
curl -s -o NUL -w '%{redirect_url}|%header{location}\n' http://127.0.0.1:20700/raw/locutf
```

**Expected (real curl)**

- `protoloc`: `foo://x/`
- `locutf`: `http://127.0.0.1:20700/caf%C3%A9?q=%E9|/caf\xc3\xa9?q=\xe9`

**Actual (Curl)**

- `protoloc`: empty.
- `locutf`: `|/caf\xc3\x83\xc2\xa9?q=\xc3\xa9`. `redirect_url` is empty, and the header is double-encoded as in WV-03.

**Suspected location in source**

- The redirect_url computation in `Curl.Core.UnitLibrary/RedirectFollower.cs`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr087Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 3): **Not reproducible at 9a068f6f5.** Measured with `Record-CurlExchange.ps1` on port 20716 (server answers `HTTP/1.1 302 Found`, the issue's `Location`, `Content-Length: 0`), REF = curl 8.22.0 Schannel (`C:\Program Files\Git\ucrt64\bin\curl.exe`), PORT = `Curl.Console` Debug build:
  - `curl -sS -w '%{redirect_url}\n' http://127.0.0.1:20716/raw/protoloc` (`Location: foo://x/`): REF and PORT both exit 0, stdout `foo://x/\n`, stderr empty, request bytes identical (`cmp`).
  - `curl -s -o NUL -w '%{redirect_url}|%header{location}\n' http://127.0.0.1:20716/raw/locutf` (`Location: /caf\xc3\xa9?q=\xe9`): REF and PORT both exit 0, stdout `http://127.0.0.1:20716/caf%C3%A9?q=%E9|/caf\xc3\xa9?q=\xe9\r\n` (raw bytes, no double encoding), stderr empty, request bytes identical. Neither writes a file.
  - Earlier work (HttpRedirectLocation's CGR-061/CGR-062 changes and the header byte marking) already fixed both. No production change was needed, so no quality run.
  - ADR-0482 test: `Curl.Core.UnitTests/Cgr087Tests.cs` pins that `RedirectFollower` without `-L` keeps both measured `%{redirect_url}` values whole. It passes at this commit. It never failed, because the bug was already fixed before this run; the task's own Notes allow finishing with evidence.
  - `dotnet build` clean (0 warnings). Fast tests: 46 test assemblies passed, 0 failed (Core: 1899 passed).

## Log

- 2026-10-26: Created from CGR-087 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. CGR-087 not reproducible: %{redirect_url} for foo:// and raw UTF-8 Location matches curl 8.22.0; pinned by Cgr087Tests
