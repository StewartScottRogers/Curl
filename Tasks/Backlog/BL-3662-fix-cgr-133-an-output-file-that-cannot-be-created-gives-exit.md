---
id: BL-3662
title: Fix CGR-133: an output file that cannot be created gives exit 2 and no -w output for sftp/scp (REF gives exit 23 and still prints -w)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr133Tests.cs, Curl.Console/DeferredOutputFileStream.cs]
requirement: none
created: 2026-10-26
completed:
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

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr133Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-133 of the third-party gap report.
