---
id: BL-3636
title: Fix CGR-107: -w connection counts, request sizes, header counts and timings are not filled in for non-HTTP protocols (FTP, SMTP, POP3, IMAP, DICT, GOP...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitTests/Cgr107Tests.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitTests/Cgr107Tests.cs, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests/Cgr107Tests.cs, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests/Cgr107Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3636 — Fix CGR-107: -w connection counts, request sizes, header counts and timings are not filled in for non-HTTP protocols (FTP, SMTP, POP3, IMAP, DICT, GOP...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-107 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-107: `-w` connection counts, request sizes, header counts and timings are not filled in for non-HTTP protocols (FTP, SMTP, POP3, IMAP, DICT, GOPHER, TELNET, MQTT, WS, RTSP, LDAP, SFTP, SCP)

- **Severity:** Medium
- **Area:** other-protocols, ftp-file-tftp, sftp-scp-ssh, doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux). Detail: [sftp-scp-ssh SSH-08] Windows, Linux
- **Confidence:** Confirmed. Detail: [other-protocols OP-10] Confirmed (Windows and Linux).; [other-protocols OP-11] Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `other-protocols OP-10`, `ftp-file-tftp FTP-8`, `ftp-file-tftp FTP-9`, `other-protocols OP-11`, `sftp-scp-ssh SSH-08`, `doh-ipfs-resolution DOH-20`

Common root: these protocol handlers never populate `NumConnects`, `RequestSize`, header counts or the timing fields of the transfer report, so `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:208` (`NumConnectsOf`) and `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` fall back to 0.

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- *Setup (sftp-scp-ssh):* E = `SCR\evidence\sftp-scp-ssh`. Server: a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21523 (`E\setup.sh`, then `E\setup2.sh`; `E\remote2.sh reset` rebuilds the test tree; Windows reaches it through WSL localhost forwarding). Odd servers: `E\oddstart.sh` (21531 accepts and stays silent 60 s; 21533 sends `SSH-1.5-old` and then nothing). `AUTH` = `-k -u stewart_rogers: --key E\keys\c_rsa_pem --pubkey E\keys\c_rsa_pem.pub`; `U` = `sftp://127.0.0.1:21523/home/stewart_rogers/sftpt21501`; `UC` = the same with `scp://`. Verifier: `E\verify\vwin.sh`, `E\verify\vlin.sh`, `E\verify\vwinlog.sh`.
- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- **[other-protocols OP-10]** `-w %{num_connects}` is 0 for every non-HTTP protocol; curl reports 1
  - `EV\confirm.cases` C13/C14/C15/C21 and `EV\wvars.cases`. Example: `curl -s -S -w "%{num_connects} %{size_request}\n" smtp://127.0.0.1:21001 --mail-from a@b --mail-rcpt c@d -T EV\data\msg_crlf.txt`
- **[ftp-file-tftp FTP-8]** `%{num_connects}` is 0 for FTP (REF reports 2) and `%{size_request}` is 0 for FTP uploads
  - `curl -sS -o out.bin -w '%{num_connects}\n' ftp://127.0.0.1:20960/file.txt`
  - `curl -sS -T upload.txt -w '%{size_upload} %{size_download} %{size_request} %{size_header}\n' ftp://127.0.0.1:20960/s.txt`

  Cases: `v_numconn` and `v_size_ul` (Windows); `v_numconn` (Linux).
- **[ftp-file-tftp FTP-9]** `%{time_total}`, `%{time_connect}`, `%{time_pretransfer}` and `%{time_starttransfer}` are always 0.000000 for FTP
  - `curl -sS -o o -w '%{time_total} %{time_connect} %{time_starttransfer} %{time_pretransfer}\n' ftp://127.0.0.1:20960/file.txt`. Cases: `v_time` (Windows and Linux). Also `v_P_*`, where `time_total` was `0.000000` after a 60 s wait.
- **[other-protocols OP-11]** `-w` counters are wrong for other protocols: `size_request`, `num_headers` and `http_version`
  - `EV\confirm.cases` C13 (SMTP), C14 (`-o NUL -w "%{num_connects} %{num_headers}\n" ws://...`), C15 (`-w "%{num_connects} %{num_headers} %{http_version}\n" rtsp://127.0.0.1:21001/m`) and C21 (DICT). Also `EV\wvars.cases`.
- **[sftp-scp-ssh SSH-08]** -w timing and speed variables are always 0 for sftp/scp, and %{num_connects} is 0 even for a new connection
  - `curl -sS AUTH -o NUL -w "%{time_namelookup} %{time_connect} %{time_appconnect} %{time_pretransfer} %{time_starttransfer} %{time_total} %{speed_download} %{num_connects}\n" U/big.bin`. Repeat with `UC/big.bin` and with `U/missing.txt` (a failed transfer).
- **[doh-ipfs-resolution DOH-20]** %{num_connects} is 0 after a refused WebSocket upgrade
  - `srv web=21802` (it answers 200), then `curl -sS ws://127.0.0.1:21802/x -w "%{num_connects}\n"`. Case: `cases\v_f20_ws.txt`.

Commands (run each with REF and with PORT):

```sh
# [other-protocols OP-10]
curl -s -S -w "%{num_connects} %{size_request}\n" smtp://127.0.0.1:21001 --mail-from a@b --mail-rcpt c@d -T EV\data\msg_crlf.txt
# [ftp-file-tftp FTP-8]
curl -sS -o out.bin -w '%{num_connects}\n' ftp://127.0.0.1:20960/file.txt
curl -sS -T upload.txt -w '%{size_upload} %{size_download} %{size_request} %{size_header}\n' ftp://127.0.0.1:20960/s.txt
# [ftp-file-tftp FTP-9]
curl -sS -o o -w '%{time_total} %{time_connect} %{time_starttransfer} %{time_pretransfer}\n' ftp://127.0.0.1:20960/file.txt
# [sftp-scp-ssh SSH-08]
curl -sS AUTH -o NUL -w "%{time_namelookup} %{time_connect} %{time_appconnect} %{time_pretransfer} %{time_starttransfer} %{time_total} %{speed_download} %{num_connects}\n" U/big.bin
# [doh-ipfs-resolution DOH-20]
srv web=21802
curl -sS ws://127.0.0.1:21802/x -w "%{num_connects}\n"
```

**Expected (real curl)**

- **[other-protocols OP-10]** `1` for SMTP, POP3, IMAP, DICT, GOPHER, TELNET, MQTT, WS, RTSP and LDAP.
- **[ftp-file-tftp FTP-8]** Prints `2`, and `8 0 8 0` for the upload.
- **[ftp-file-tftp FTP-9]** Real elapsed values, for example `0.020323 0.000681 0.019952 0.018408`.
- **[other-protocols OP-11]** 
  - `size_request`: SMTP upload 75, DICT 44 (`CLIENT ...`, `DEFINE ! word`, `QUIT`), GOPHER 4, MQTT subscribe 34
  - `num_headers`: ws 101 gives 3, rtsp gives 2
  - `http_version`: rtsp gives 1.1
- **[sftp-scp-ssh SSH-08]** real values, for example `0.000059 0.000588 0.092217 0.116552 0.117091 0.140040 7488117 1`. The failed transfer still gives `0.000611 0.115775 1`.
- **[doh-ipfs-resolution DOH-20]** stdout `1`, exit 22 with `Refused WebSocket upgrade: 200`.

**Actual (Curl)**

- **[other-protocols OP-10]** `0` for all of them. HTTP gives 1 and matches.
- **[ftp-file-tftp FTP-8]** Prints `0`, and `8 0 0 0` for the upload.
- **[ftp-file-tftp FTP-9]** `0.000000 0.000000 0.000000 0.000000`. The values are constant zeros, not timing jitter, so the information is missing.
- **[other-protocols OP-11]** `size_request` is 0 for SMTP, DICT, GOPHER and MQTT. WS `num_headers` is 0. RTSP gives `num_headers` 0 and `http_version` 0. POP3/IMAP `size_request` (0) and HTTP match.
- **[sftp-scp-ssh SSH-08]** `0.000000 0.000000 0.000000 0.000000 0.000000 0.000000 0 0` on every sftp/scp transfer, successful or failed. `%{json}` also has num_connects 0. Only the size_* values are filled in.
- **[doh-ipfs-resolution DOH-20]** stdout `0`, the same exit code and message.

**Suspected location in source**

- **[other-protocols OP-10]** `Curl.Output.UnitLibrary\TransferWriteOutVariables.cs:140`. These protocol handlers never set `info.NumConnects`.
- **[ftp-file-tftp FTP-8]** `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:208` `NumConnectsOf` falls back to 0 because the FTP handler never sets a connection count.
- **[ftp-file-tftp FTP-9]** The FTP handler does not populate the TransferReport timing fields (`Curl.Protocol.Ftp.UnitLibrary`). Not narrowed.
- **[other-protocols OP-11]** `Curl.Output.UnitLibrary\TransferWriteOutVariables.cs:136,141`. `RequestSize` and the header counts are not filled in by these protocol handlers.
- **[sftp-scp-ssh SSH-08]** the transfer results from `Curl.Protocol.Ssh.UnitLibrary` (SshProtocolHandler and the Sftp and Scp transfer classes) carry no timings and no connection count. See `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:208` (NumConnectsOf).
- **[doh-ipfs-resolution DOH-20]** `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` (num_connects).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr107Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-107 of the third-party gap report.
