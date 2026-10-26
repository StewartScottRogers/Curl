---
id: BL-3626
title: Fix CGR-097: SIZE reply parsing: a trailing space, sign, text or non-digit suffix makes PORT ignore the size
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr097Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3626 — Fix CGR-097: SIZE reply parsing: a trailing space, sign, text or non-digit suffix makes PORT ignore the size

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-097 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-097: SIZE reply parsing: a trailing space, sign, text or non-digit suffix makes PORT ignore the size

- **Severity:** Medium
- **Area:** ftp-file-tftp
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `ftp-file-tftp FTP-4`

**Reproduction**

- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- Script `SIZE=213 18 ` (trailing space): `curl -sS -I ftp://127.0.0.1:20960/file.txt`.
- Script `SIZE=213 -5` or `SIZE=213 size 5`: `curl -sSv ftp://127.0.0.1:20960/file.txt` (the file is 18 bytes).

Cases: `v_size_trail_I`, `v_size_neg`, `v_size_text` (Windows); `v_size_neg` (Linux).

Commands (run each with REF and with PORT):

```sh
SIZE=213 18 
curl -sS -I ftp://127.0.0.1:20960/file.txt
SIZE=213 -5
SIZE=213 size 5
curl -sSv ftp://127.0.0.1:20960/file.txt
```

**Expected (real curl)**

- Uses the trailing digit run. `-I` prints `Content-Length: 18`. With `-5` or `size 5` it reports `Getting file with size: 5` and writes only `line1`, exit 0.

**Actual (Curl)**

- The size is treated as unknown. `-I` omits the `Content-Length` line. Downloads report `Getting file with size: -1` and write all 18 bytes.

**Suspected location in source**

- `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs:1996-1999` `SizeOf` (`long.TryParse` with `AllowLeadingWhite` only).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr097Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-097 of the third-party gap report.
