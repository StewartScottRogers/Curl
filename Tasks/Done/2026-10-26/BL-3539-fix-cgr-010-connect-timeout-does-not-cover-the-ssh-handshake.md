---
id: BL-3539
title: Fix CGR-010: --connect-timeout does not cover the SSH handshake, so a server that accepts TCP but never sends a banner hangs PORT
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Protocol.Ssh.UnitLibrary/SshConnectPhaseLimit.cs, Curl.Protocol.Ssh.UnitLibrary/Transport/SshTransport.cs, Curl.Protocol.Ssh.UnitTests/Cgr010Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3539 — Fix CGR-010: --connect-timeout does not cover the SSH handshake, so a server that accepts TCP but never sends a banner hangs PORT

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-010 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-010: --connect-timeout does not cover the SSH handshake, so a server that accepts TCP but never sends a banner hangs PORT

- **Severity:** Critical (hang)
- **Area:** sftp-scp-ssh
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `sftp-scp-ssh SSH-02`

**Reproduction**

- *Setup (sftp-scp-ssh):* E = `SCR\evidence\sftp-scp-ssh`. Server: a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21523 (`E\setup.sh`, then `E\setup2.sh`; `E\remote2.sh reset` rebuilds the test tree; Windows reaches it through WSL localhost forwarding). Odd servers: `E\oddstart.sh` (21531 accepts and stays silent 60 s; 21533 sends `SSH-1.5-old` and then nothing). `AUTH` = `-k -u stewart_rogers: --key E\keys\c_rsa_pem --pubkey E\keys\c_rsa_pem.pub`; `U` = `sftp://127.0.0.1:21523/home/stewart_rogers/sftpt21501`; `UC` = the same with `scp://`. Verifier: `E\verify\vwin.sh`, `E\verify\vlin.sh`, `E\verify\vwinlog.sh`.
- run `E\oddstart.sh`, then `timeout 20 curl -sS AUTH --connect-timeout 3 sftp://127.0.0.1:21531/x`. Repeat with `scp://127.0.0.1:21531/x`, and with `sftp://127.0.0.1:21533/x` under `timeout 70`.

Commands (run each with REF and with PORT):

```sh
E\oddstart.sh
timeout 20 curl -sS AUTH --connect-timeout 3 sftp://127.0.0.1:21531/x
timeout 70
```

**Expected (real curl)**

- after about 3 s, exit 28 with `curl: (28) Operation timed out after 3012 milliseconds with 0 bytes received`. Same for scp and for port 21533.

**Actual (Curl)**

- no output. The harness kills it at 20 s (exit 124). It would end only when the server closes the connection. Against port 21533 it ignores the 3 s limit and waits for the server to close (60 s). It then exits 2 with `curl: (2) Failure establishing ssh session: -1, Unable to exchange encryption keys`.

**Suspected location in source**

- `Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:148-170`. The connect deadline covers only `connector.ConnectAsync`. `RunSessionAsync` (banner exchange and KEX) runs with no connect deadline.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr010Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 2): Added `Curl.Protocol.Ssh.UnitLibrary/SshConnectPhaseLimit.cs` (new file) and `Transport/SshTransport.cs` (a `HasServerIdentificationArrived` flag) to touches. No task in Doing on origin/work/dark-factory names either one.
- Fix (ADR-0815): `SshConnectPhaseLimit` (`--connect-timeout`, or 300 s when it is not given or is 0, counted from the start of `ExecuteAsync`) cancels the identification exchange, the key exchange, the ssh-userauth request, the host-key check and the authentication. When it passes, the result is exit 28 `Operation timed out after N milliseconds with 0 bytes received`. Once the server's identification has arrived, a DISCONNECT is sent first, as curl does.
- Repros rebuilt with `Record-CurlExchange.ps1 -Port 21531`. Silent server: `-ResponseDelayMilliseconds 8000`. Identification only: `-Response 'SSH-1.5-old\r\n' -HoldOpenMilliseconds 8000`. CurlArgs `-sS -k -u u:p --connect-timeout 2 <scheme>://127.0.0.1:21531/x`. REF is Git for Windows curl 8.22.0. PORT is `dotnet run --no-build --project Curl.Console --`. I used 2 s rather than the report's 3 s.
  - sftp, silent: REF exit 28 `curl: (28) Operation timed out after 2015 milliseconds with 0 bytes received`, no stdout, wire `SSH-2.0-libssh2_1.11.1\r\n`. PORT the same (2008 ms).
  - scp, silent: REF exit 28 (2009 ms). PORT exit 28 (2018 ms). Both have no stdout and the same wire bytes.
  - sftp, `SSH-1.5-old` then silence: REF exit 28 (2003 ms), 1144 wire bytes (banner, KEXINIT, DISCONNECT Shutdown). PORT exit 28 (2024 ms), 1144 wire bytes with the DISCONNECT. Cookie and padding are random.
  - Before the fix, PORT waited for the server to close (CGR-010's Actual). No files are written in any repro.
- `Cgr010Tests` (5 cases plus the cancelled-transfer case) fails before the fix: 5 failures with the handshake on the transfer's token. It passes after it.
- No repro variant matched before, so there is none to hold. The other 1905 Curl.Protocol.Ssh.UnitTests still pass.
- `dotnet build` is clean (0 warnings). The full fast run passed except one Cgr010 case, which was then fixed; Curl.Protocol.Ssh.UnitTests then passed 1905/1905. Measure-CodeQuality for Curl.Protocol.Ssh.UnitLibrary: 100% lines, 100% branches, 0 failing members, worst CRAP 10.
- The change is platform-neutral: no paths, OS error text or certificates, and the clock is a fake TimeProvider.

## Log

- 2026-10-26: Created from CGR-010 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. --connect-timeout now holds the SSH handshake and authentication: a silent server ends in exit 28 Operation timed out, as curl does
