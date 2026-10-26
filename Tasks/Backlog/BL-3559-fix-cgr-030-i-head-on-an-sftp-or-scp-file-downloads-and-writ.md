---
id: BL-3559
title: Fix CGR-030: -I/--head on an sftp:// or scp:// file downloads and writes the whole body, and fails on a directory path without a trailing slash
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Protocol.Ssh.UnitTests/Cgr030Tests.cs]
requirement: none
created: 2026-10-26
completed:
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

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr030Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-030 of the third-party gap report.
