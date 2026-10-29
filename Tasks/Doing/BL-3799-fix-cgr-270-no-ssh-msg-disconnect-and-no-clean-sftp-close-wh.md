---
id: BL-3799
title: Fix CGR-270: no SSH_MSG_DISCONNECT and no clean SFTP close when --max-time or --speed-limit stops a transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Protocol.Ssh.UnitTests/Cgr270Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3799 — Fix CGR-270: no SSH_MSG_DISCONNECT and no clean SFTP close when --max-time or --speed-limit stops a transfer

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-270 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-270: no SSH_MSG_DISCONNECT and no clean SFTP close when --max-time or --speed-limit stops a transfer

- **Severity:** Low
- **Area:** sftp-scp-ssh
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux.
- **Confidence:** Confirmed. Detail: Confirmed for --max-time and --speed-limit. The key-exchange failure case (the proxy on 21536) comes only from the original agent's evidence (`E\runs\odd_36_full`); the verifier did not re-run it.
- **Related existing finding (GF/AF):** none
- **Source findings:** `sftp-scp-ssh SSH-13`

**Reproduction**

- *Setup (sftp-scp-ssh):* E = `SCR\evidence\sftp-scp-ssh`. Server: a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21523 (`E\setup.sh`, then `E\setup2.sh`; `E\remote2.sh reset` rebuilds the test tree; Windows reaches it through WSL localhost forwarding). Odd servers: `E\oddstart.sh` (21531 accepts and stays silent 60 s; 21533 sends `SSH-1.5-old` and then nothing). `AUTH` = `-k -u stewart_rogers: --key E\keys\c_rsa_pem --pubkey E\keys\c_rsa_pem.pub`; `U` = `sftp://127.0.0.1:21523/home/stewart_rogers/sftpt21501`; `UC` = the same with `scp://`. Verifier: `E\verify\vwin.sh`, `E\verify\vlin.sh`, `E\verify\vwinlog.sh`.
- run each of these, then read `~/sshd21501/sshd.log` and `~/sshd21501/sftp.log`:
  - `curl -sS AUTH --max-time 2 --limit-rate 50K -o NUL U/big.bin`
  - `curl -sS AUTH --speed-limit 100000000 --speed-time 2 --limit-rate 50K -o NUL U/big.bin`

Commands (run each with REF and with PORT):

```sh
curl -sS AUTH --max-time 2 --limit-rate 50K -o NUL U/big.bin
curl -sS AUTH --speed-limit 100000000 --speed-time 2 --limit-rate 50K -o NUL U/big.bin
```

**Expected (real curl)**

- sftp-server logs `close "..."`, and sshd logs `Received disconnect from 127.0.0.1 port N:11: Shutdown`.

**Actual (Curl)**

- sftp-server logs `forced close "..."`, and sshd logs only `Connection closed by 127.0.0.1 port N`. The exit code matches (28).

**Suspected location in source**

- `Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:177-190` and the end of RunSessionAsync. DisconnectIgnoringFailureAsync runs only when the session reaches its normal end.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr270Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-270 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
