---
id: BL-3559
title: Fix CGR-030: -I/--head on an sftp:// or scp:// file downloads and writes the whole body, and fails on a directory path without a trailing slash
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Protocol.Ssh.UnitTests/Cgr030Tests.cs, Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpFileDownload.cs, Curl.Protocol.Ssh.UnitLibrary/Scp/ScpFileDownload.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3559 — Fix CGR-030: -I/--head on an sftp:// or scp:// file downloads and writes the whole body, and fails on a directory path without a trailing slash

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-030 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-030: -I/--head on an sftp:// or scp:// file downloads and writes the whole body, and fails on a directory path without a trailing slash

- **Severity:** High
- **Area:** sftp-scp-ssh
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed. Detail: Confirmed (by the original agent, then by the verifier on both platforms).
- **Related existing finding (GF/AF):** none
- **Source findings:** `sftp-scp-ssh SSH-01`

**Reproduction**

- *Setup (sftp-scp-ssh):* E = `SCR\evidence\sftp-scp-ssh`. Server: a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21523 (`E\setup.sh`, then `E\setup2.sh`; `E\remote2.sh reset` rebuilds the test tree; Windows reaches it through WSL localhost forwarding). Odd servers: `E\oddstart.sh` (21531 accepts and stays silent 60 s; 21533 sends `SSH-1.5-old` and then nothing). `AUTH` = `-k -u stewart_rogers: --key E\keys\c_rsa_pem --pubkey E\keys\c_rsa_pem.pub`; `U` = `sftp://127.0.0.1:21523/home/stewart_rogers/sftpt21501`; `UC` = the same with `scp://`. Verifier: `E\verify\vwin.sh`, `E\verify\vlin.sh`, `E\verify\vwinlog.sh`.
- `curl -sS AUTH -I -w "<%{size_download}>" U/hello.txt`. Repeat with `UC/hello.txt`, `U/big.bin`, `-I -r 0-3 U/digits.txt`, and `-I U/sub` (a directory with no trailing slash).

Commands (run each with REF and with PORT):

```sh
curl -sS AUTH -I -w "<%{size_download}>" U/hello.txt
```

**Expected (real curl)**

- exit 0, nothing written, `<0>`. The sftp-server log shows stat, open, stat, then close with `bytes read 0`. `U/sub` also exits 0.

**Actual (Curl)**

- exit 0, and the whole body is written:
  - 11 bytes for hello.txt (`<11>`)
  - 1048576 bytes for big.bin
  - 4 bytes with `-r 0-3`

  The log has no first stat and shows `close ... bytes read 11`. On `U/sub` PORT exits 79 with `curl: (79) Error in the SSH layer`.

**Suspected location in source**

- `Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:385-405`. `context.NoBody` is passed only to `SftpDirectoryListing.ListAsync`. `SftpFileDownload.DownloadAsync` and `ScpFileDownload.DownloadAsync` never receive it.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr030Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 5): touches widened with `Sftp/SftpFileDownload.cs` and `Scp/ScpFileDownload.cs`, where the body is read; no task in Doing on origin/work/dark-factory named either. Fix: both downloads take an init-only `NoBody` the handler sets from `context.NoBody`. SFTP with `-I` sends `STAT` for the file time at `SSH_SFTP_GETINFO` (curl's tool sets `CURLOPT_FILETIME` for `-I`; the answer is ignored, as in the directory case, BL-1207), then opens, sends `STAT` for the size, still fails a range or `-C` the file cannot serve, and closes with no `READ`. SCP reads the `T`/`C` header and closes the channel with nothing copied. No `READ` also explains `U/sub`: OpenSSH opens a directory, and its first read is what failed with 79. No ADR: this is curl's measured behaviour, with no choice in it.
- Measured 2026-10-26: REF = Git for Windows curl 8.22.0 (Schannel, libssh2 1.11.1) and PORT = this branch's Curl.Console, against a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21525 (RSA host key, `KexAlgorithms +diffie-hellman-group-exchange-sha256,...`, since WinCNG libssh2 has no curve or PQ kex), sftp-server log at INFO. AUTH = `-sS -k -u stewart_rogers: --key c_rsa_pem --pubkey c_rsa_pem.pub`, `-w "<%{size_download}>"`. Results, REF = PORT in every row:
  - `-I sftp://.../hello.txt`: exit 0, stdout `<0>`, stderr empty; log `open hello.txt flags READ`, `close ... bytes read 0 written 0`.
  - `-I scp://.../hello.txt`: exit 0, `<0>`.
  - `-I sftp://.../big.bin` (1 MiB): exit 0, `<0>`.
  - `-I -r 0-3 sftp://.../digits.txt`: exit 0, `<0>`; `bytes read 0`.
  - `-I sftp://.../sub` (directory, no slash): exit 0, `<0>`; `open sub`, `close sub bytes read 0`.
  - Extra variants: `-I -r 20-30 .../digits.txt`: both exit 33 `curl: (33) Offset (20) was beyond file size (10)`, `<0>`, `bytes read 0`. Without `-I`, `.../digits.txt`: both exit 0, `0123456789<10>` (already matched, still matches).
  - STAT is logged only at DEBUG, so the STAT, OPEN, STAT, CLOSE order is pinned by `Cgr030Tests` against the in-memory server rather than by the log.
- `Cgr030Tests` (3 tests, 4 cases): with the handler's `NoBody` wiring removed, the SFTP `-r 0-3` and SCP cases fail (Success(11)/Success(4) with bytes written); with it, all pass. Curl.Protocol.Ssh.UnitTests 1909 passed; solution fast tests 46 projects, 43084 passed, 0 failed; `dotnet format --verify-no-changes` clean on both Ssh projects; Measure-CodeQuality for Curl.Protocol.Ssh.UnitLibrary: 0 failing members. The fix is platform-neutral (no paths, OS text or certificates in the tests), so CI on Linux and macOS should stay green.

## Log

- 2026-10-26: Created from CGR-030 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -I on sftp:// and scp:// files reads and writes no body and exits 0, a directory without a trailing slash included, as curl 8.22.0 does
