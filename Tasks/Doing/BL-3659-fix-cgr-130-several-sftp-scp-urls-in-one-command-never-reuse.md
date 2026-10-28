---
id: BL-3659
title: Fix CGR-130: several sftp/scp URLs in one command never reuse the SSH connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Protocol.Ssh.UnitTests/Cgr130Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3659 — Fix CGR-130: several sftp/scp URLs in one command never reuse the SSH connection

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-130 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-130: several sftp/scp URLs in one command never reuse the SSH connection

- **Severity:** Medium
- **Area:** sftp-scp-ssh
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0051, GF-0057
- **Source findings:** `sftp-scp-ssh SSH-03`

**Reproduction**

- *Setup (sftp-scp-ssh):* E = `SCR\evidence\sftp-scp-ssh`. Server: a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21523 (`E\setup.sh`, then `E\setup2.sh`; `E\remote2.sh reset` rebuilds the test tree; Windows reaches it through WSL localhost forwarding). Odd servers: `E\oddstart.sh` (21531 accepts and stays silent 60 s; 21533 sends `SSH-1.5-old` and then nothing). `AUTH` = `-k -u stewart_rogers: --key E\keys\c_rsa_pem --pubkey E\keys\c_rsa_pem.pub`; `U` = `sftp://127.0.0.1:21523/home/stewart_rogers/sftpt21501`; `UC` = the same with `scp://`. Verifier: `E\verify\vwin.sh`, `E\verify\vlin.sh`, `E\verify\vwinlog.sh`.
- `curl -sS AUTH -o NUL -o NUL -w "[%{num_connects} %{conn_id}]\n" U/hello.txt U/digits.txt`. Add `-v` to see the connection lines.

Commands (run each with REF and with PORT):

```sh
curl -sS AUTH -o NUL -o NUL -w "[%{num_connects} %{conn_id}]\n" U/hello.txt U/digits.txt
```

**Expected (real curl)**

- prints `[1 0]` then `[0 0]`.
  - `-v` shows `* Reusing existing sftp: connection with host 127.0.0.1`, and Connection #0 for both URLs.
  - The sshd log shows one publickey login, one sftp subsystem and one `Received disconnect ... 11: Shutdown`.

**Actual (Curl)**

- prints `[0 0]` then `[0 1]`.
  - `-v` shows a second `Trying 127.0.0.1:21523...` and `Connection #1`.
  - The sshd log shows two logins, two subsystems and two disconnects.

**Suspected location in source**

- `Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:148-197`. ExecuteAsync opens a new connection for every transfer and disposes of it at the end.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr130Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-130 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-3659-20261026-151632-L4.jsonl
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
