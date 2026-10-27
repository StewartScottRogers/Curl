---
id: BL-3661
title: Fix CGR-132: -i/--include writes the output of the sftp quote commands pwd and statvfs to stdout ahead of the body
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/Cgr132Tests.cs, Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommands.cs, Curl.Protocol.Ssh.UnitTests/Cgr132Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3661 — Fix CGR-132: -i/--include writes the output of the sftp quote commands pwd and statvfs to stdout ahead of the body

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-132 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-132: -i/--include writes the output of the sftp quote commands pwd and statvfs to stdout ahead of the body

- **Severity:** Medium
- **Area:** sftp-scp-ssh
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `sftp-scp-ssh SSH-05`

**Reproduction**

- *Setup (sftp-scp-ssh):* E = `SCR\evidence\sftp-scp-ssh`. Server: a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21523 (`E\setup.sh`, then `E\setup2.sh`; `E\remote2.sh reset` rebuilds the test tree; Windows reaches it through WSL localhost forwarding). Odd servers: `E\oddstart.sh` (21531 accepts and stays silent 60 s; 21533 sends `SSH-1.5-old` and then nothing). `AUTH` = `-k -u stewart_rogers: --key E\keys\c_rsa_pem --pubkey E\keys\c_rsa_pem.pub`; `U` = `sftp://127.0.0.1:21523/home/stewart_rogers/sftpt21501`; `UC` = the same with `scp://`. Verifier: `E\verify\vwin.sh`, `E\verify\vlin.sh`, `E\verify\vwinlog.sh`.
- `curl -sS AUTH -i -Q pwd U/hello.txt` and `curl -sS AUTH -i -Q "statvfs /home/stewart_rogers/sftpt21501" U/hello.txt`.

Commands (run each with REF and with PORT):

```sh
curl -sS AUTH -i -Q pwd U/hello.txt
curl -sS AUTH -i -Q "statvfs /home/stewart_rogers/sftpt21501" U/hello.txt
```

**Expected (real curl)**

- stdout is only `hello sftp\n`. The pwd and statvfs text goes only to `-D`.

**Actual (Curl)**

- stdout starts with `257 "/home/stewart_rogers/sftpt21501/hello.txt" is current directory.\n` (or with the 12 `statvfs:` / `f_bsize: ...` lines), followed by the body. The `-D` output matches REF.

**Suspected location in source**

- `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommands.cs:263`. WriteHeaderAsync writes to `context.HeaderOutput`, and `Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs` copies that to stdout for -i.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr132Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Added `Curl.Engine.UnitLibrary/TransferContextFactory.cs` to touches: whether header lines go to the body output is decided by its `WritesHeaderLinesToBody`, not by `HeaderLineTeeStream` (which only tees once chosen). No task in Doing on origin/work/dark-factory names it. `SftpQuoteCommands.cs` and the Ssh `Cgr132Tests.cs` needed no change: the quote output is rightly a header line, as curl's `libssh2.c` writes it.
- Fix (ADR-0844, decided by Claude under Stewart's delegation): an `sftp` or `scp` URL's header lines never go to the body output under `-i`, `-I` or `CURLOPT_HEADER`, only to `-D`. Other schemes are left until measured. Focused test `Curl.Engine.UnitTests/Cgr132Tests.cs`: with the SSH check removed its four sftp/scp rows fail ("Expected condition to be false"); with it they pass.
- Measured 2026-10-26, real curl 8.22.0 (ucrt64, Schannel, libssh2 1.11.1) and Curl, against a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21561 (RSA host key and `KexAlgorithms +diffie-hellman-group14-sha256` for the WinCNG libssh2; absolute `-f`/`-E` paths, or sshd-session fails). AUTH = `-k -u stewart_rogers: --key c_rsa --pubkey c_rsa.pub`, U = `sftp://127.0.0.1:21561/tmp/l1ssh/tree`, `hello.txt` = `hello sftp\n`. Request bytes cannot differ: the fix only changes where lines already received are written.
  - `curl -sS AUTH -i -Q pwd U/hello.txt -D hd.txt`: REF exit 0, stdout `hello sftp\n`, stderr empty, hd.txt `257 "/tmp/l1ssh/tree/hello.txt" is current directory.`. Curl before: stdout started with the 257 line, then the body. Curl after: identical to REF.
  - `curl -sS AUTH -i -Q "statvfs /tmp/l1ssh/tree" U/hello.txt -D hd.txt`: REF exit 0, stdout `hello sftp\n`, stderr empty, hd.txt the statvfs lines (md5 365cfab09cfa1a68...). Curl before: stdout started with the 12 statvfs lines. Curl after: identical to REF, same hd.txt md5.
  - Variants: `-I -Q pwd`: REF stdout empty and the 257 line in `-D`; Curl before wrote the 257 line to stdout, after identical to REF. `scp://... -i -Q pwd`: both write only `hello sftp\n` (already matched, still matches).
- Linux and macOS: the change and its test are platform-neutral (a scheme check; no paths or OS text) and the fast tests are green here (46 test projects, 0 failed). `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 100% line and branch, 0 failing members (the first run flagged complexity 12 on `WritesHeaderLinesToBody`, so it was split into `KeepsHeaderLinesOffTheBody` and `IsFtpNoBody`).

## Log

- 2026-10-26: Created from CGR-132 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -i and -I on sftp and scp URLs write the quote command output (pwd, statvfs) only to -D, as curl 8.22.0
