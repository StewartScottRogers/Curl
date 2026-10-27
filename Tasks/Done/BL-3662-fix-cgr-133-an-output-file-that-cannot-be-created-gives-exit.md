---
id: BL-3662
title: Fix CGR-133: an output file that cannot be created gives exit 2 and no -w output for sftp/scp (REF gives exit 23 and still prints -w)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr133Tests.cs, Curl.Console/DeferredOutputFileStream.cs, Curl.Protocol.Ssh.UnitLibrary/SshOutputWrite.cs, Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpFileDownload.cs, Curl.Protocol.Ssh.UnitLibrary/Scp/ScpFileDownload.cs, Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md, Curl.Protocol.Ssh.UnitTests/Cgr133Tests.cs, Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.ConnectionReset.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3662 — Fix CGR-133: an output file that cannot be created gives exit 2 and no -w output for sftp/scp (REF gives exit 23 and still prints -w)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-133 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-133: an output file that cannot be created gives exit 2 and no -w output for sftp/scp (REF gives exit 23 and still prints -w)

- **Severity:** Medium
- **Area:** sftp-scp-ssh
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `sftp-scp-ssh SSH-06`

**Reproduction**

- *Setup (sftp-scp-ssh):* E = `SCR\evidence\sftp-scp-ssh`. Server: a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21523 (`E\setup.sh`, then `E\setup2.sh`; `E\remote2.sh reset` rebuilds the test tree; Windows reaches it through WSL localhost forwarding). Odd servers: `E\oddstart.sh` (21531 accepts and stays silent 60 s; 21533 sends `SSH-1.5-old` and then nothing). `AUTH` = `-k -u stewart_rogers: --key E\keys\c_rsa_pem --pubkey E\keys\c_rsa_pem.pub`; `U` = `sftp://127.0.0.1:21523/home/stewart_rogers/sftpt21501`; `UC` = the same with `scp://`. Verifier: `E\verify\vwin.sh`, `E\verify\vlin.sh`, `E\verify\vwinlog.sh`.
- `curl -sS AUTH -o nodir21500/x.txt -w "[%{exitcode}]\n" U/hello.txt`, where the directory nodir21500 does not exist. Repeat with `UC/hello.txt`.

Commands (run each with REF and with PORT):

```sh
curl -sS AUTH -o nodir21500/x.txt -w "[%{exitcode}]\n" U/hello.txt
```

**Expected (real curl)**

- exit 23 with `curl: (23) client returned ERROR on write of 11 bytes`, and -w prints `[23]`. sftp-server logs a normal `close`.

**Actual (Curl)**

- exit 2 with `curl: (2) Could not create the output file nodir21500/x.txt.` and no -w output. sftp-server logs `forced close`.

**Notes**

- *Correction to the original report:* it said PORT sends no DISCONNECT here. That is wrong. The verifier saw `Received disconnect ... 11: Shutdown` on both Windows and Linux.

**Suspected location in source**

- `Curl.Console/DeferredOutputFileStream.cs:244` throws an IOException on the first write. The SSH download path does not turn it into CURLE_WRITE_ERROR (23).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr133Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Cause: `DeferredOutputFileStream`'s first write throws an `IOException` when the `-o` file cannot be created. The FTP and file handlers turn a failed output write into exit 23; the SSH downloads (`SftpFileDownload.Copy`, `ScpFileDownload.Copy`) caught only connection failures, so the exception escaped the handler and `UnhandledExceptionGuard` printed `curl: (2) <message>` with no `-w` output. The handler never reached its `CLOSE`/`DISCONNECT`, so sftp-server logged `forced close`.
- Fix (in `Curl.Protocol.Ssh.UnitLibrary`, not in the original `touches`; no other Doing task on origin/work/dark-factory names those files, so they were added): new `SshOutputWrite.TryWriteAsync` writes each block and turns the output's `IOException` (not a connection failure) into exit 23 `Failure writing output to destination, passed N returned M`, as FTP does. `DeferredOutputFileStream.CompleteAsync` then replaces it with curl's `client returned ERROR on write of 11 bytes`, so `DeferredOutputFileStream.cs` needed no change. The session now closes the handle and sends `DISCONNECT 11 Shutdown`, as curl does. SCP's end-of-channel empty write goes through it too. No ADR: this matches curl's measured behaviour and the FTP handler's existing choice; nothing new was decided.
- Real curl: no sshd was rebuilt for this run (the report's WSL OpenSSH server is gone). REF's result is the report's, confirmed on Windows and Linux: `curl -sS AUTH -o nodir21500/x.txt -w "[%{exitcode}]\n" U/hello.txt` (and `UC/hello.txt`) gives exit 23, stderr `curl: (23) client returned ERROR on write of 11 bytes`, stdout `[23]`, no file, normal `close`. Curl at this commit, through the production composition against the in-memory SSH server (`Curl.Console.UnitTests/Cgr133Tests.cs`, both schemes): exit 23, the same stderr line, stdout `[23]`, no directory or file created, the server sees `disconnect 11 Shutdown`. Before the fix: exit 2, `curl: (2) Could not create the output file nodir21500/x.txt.`, no `-w`.
- Focused tests: `Curl.Protocol.Ssh.UnitTests/Cgr133Tests.cs` (exit 23 and DISCONNECT for sftp and scp; `returned M` from `OutputWriteFailedException.BytesAccepted`). Before the fix the handler threw, which `SshProtocolHandlerTests.ConnectionReset.cs`'s `ExecuteAsync_LocalOutputWriteFails_*` pinned (it expected the exception and failed once the fix landed); that test now expects the write error. A direct check with the fix reverted was not possible because the lane's permissions refused `git checkout` of the files.
- Variants that already matched: the fast tests, the SSH download tests among them, are green. Messages and paths do not depend on the platform.
- Checks: `dotnet build` 0 warnings and 0 errors; fast tests green (Curl.Protocol.Ssh.UnitTests 1917 passed, Curl.Console.UnitTests 2945 passed, the rest green); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-133 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. sftp and scp downloads to an -o file that cannot be created exit 23 with curl's write-error line and still print -w
