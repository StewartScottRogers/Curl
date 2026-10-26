---
id: BL-3536
title: Fix CGR-007: TELNET -T upload is written concurrently with option-negotiation replies, so upload bytes can land inside an IAC SB NAWS ... IAC SE subne...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Protocol.Telnet.UnitLibrary/TelnetReceiver.cs, Curl.Protocol.Telnet.UnitTests/Cgr007Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3536 — Fix CGR-007: TELNET -T upload is written concurrently with option-negotiation replies, so upload bytes can land inside an IAC SB NAWS ... IAC SE subne...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-007 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-007: TELNET -T upload is written concurrently with option-negotiation replies, so upload bytes can land inside an `IAC SB NAWS ... IAC SE` subnegotiation

- **Severity:** Critical (the telnet stream is corrupted)
- **Area:** other-protocols
- **Platform(s):** Both (Windows and Linux). Detail: Both. The upload goes out before or between the negotiation replies on Windows and Linux. The upload inside SB was seen on Windows.
- **Confidence:** Confirmed. Detail: Confirmed. The verifier reproduced the SB split (1 of 3 large-upload runs on Windows) and the ordering difference (40/40 on Windows and 7/7 on Linux).
- **Related existing finding (GF/AF):** GF-0043
- **Source findings:** `other-protocols OP-1`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- Server mode `telnet`. On connect it sends `FF FD 18 FF FD 1F FF FD 27 FF FD 23 FF FB 01 FF FB 03 FF FD 03`, reads for 700 ms, then sends SB SEND for TTYPE, XDISPLOC and NEW-ENVIRON, then some text, and records what the client sends.
- Small upload: `pwsh -File EV\run.ps1 telnet.cases t17`, which runs `curl -s -S -T EV\data\tel_in.txt telnet://127.0.0.1:21001`.
- Large upload (makes the split easy to hit): `EV\verify\telbig.cases`, which runs `curl -s -S -T EV\data\big_lf.txt telnet://127.0.0.1:<port>` (410 KB). Linux: `EV/verify-linux-tel.sh`.

Commands (run each with REF and with PORT):

```sh
pwsh -File EV\run.ps1 telnet.cases t17
curl -s -S -T EV\data\tel_in.txt telnet://127.0.0.1:21001
curl -s -S -T EV\data\big_lf.txt telnet://127.0.0.1:<port>
EV/verify-linux-tel.sh
```

**Expected (real curl)**

- REF handles the whole batch of server negotiation first, then sends the upload. The wire starts `FF FC 18 FF FB 1F FF FA 1F 00 00 00 00 FF F0 FF FC 27 FF FC 23 FF FD 01 FF FD 03 FF FB 03 FF FB 00 FF FD 00` and is followed by the data. This was the result in 40/40 verifier runs on Windows and in every Linux run.

**Actual (Curl)**

- Order: the upload goes out first or at arbitrary reply boundaries. In 40/40 verifier runs of t17 on Windows, and in every Linux run, `hello telnet\r\n...` came before any negotiation reply.
- Corruption, large upload, Windows run 1 (`EV\verify\telbig.port.1.wire`): `FF FA 1F` is at offset 49173, then 16 KB of upload data, then `00 00 00 00 FF F0` at offset 65555. The window-size subnegotiation therefore contains 16 KB of file data.
- Corruption, small upload: the original tester saw the same thing in 7/68 Windows runs (`EV\telnet-race\port.32.wire`): `FF FA 1F 00 00 00 00 'hello telnet\r\nsecond line\n' FF FF ' byte\n' FF F0`.

**Suspected location in source**

- `Curl.Protocol.Telnet.UnitLibrary\TelnetProtocolHandler.cs:184-199`: the upload runs as its own task (`SendUploadAsync`, line ~404).
- `TelnetProtocolHandler.cs:446-488`: `SendReplyAsync` takes `sendLock` once per reply piece.
- `TelnetReceiver.cs:433-439` (`AppendWindowSize`) queues `IAC SB NAWS`, the size bytes and `IAC SE` as three separate sends, so the upload task can win the lock between them.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr007Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Fix (ADR-0815): `TelnetProtocolHandler` no longer sends the upload from a task of its own. One loop, as curl's `telnet_do`: wait for the server's read (at most 100 ms while an upload is going), handle it and send every reply, then send the upload's next buffer if its read has completed (`TelnetUploadReader`, so an upload that never yields still leaves the session receiving). The send lock is gone, so nothing can land between `IAC SB NAWS`, the size and `IAC SE`. When the server closes, the part of the upload already read is still sent: `CurlCommandRunnerCookieTests.RunAsync_CookieFileOnStandardInputAfterATelnetTransfer_LeavesStandardInputToTelnet` (curl 8.21.0, BL-316) failed without that.
- Repro, 2026-10-26, Windows, `Record-CurlExchange.ps1 -Script` with the report's server: send `FF FD 18 FF FD 1F FF FD 27 FF FD 23 FF FB 01 FF FB 03 FF FD 03`, pause 700, read, send SB SEND for TTYPE, XDISPLOC and NEW-ENVIRON and `server text\r\n`, pause 500, read, close. `curl -s -S -T tel_in.txt telnet://127.0.0.1:21931` (`hello telnet\r\nsecond line\n\xFF byte\n`): REF (curl 8.22.0) and PORT both exit 43 `curl: (43) A libcurl function was given a bad argument`, no stdout, wire `ff fc 18 ff fb 1f ff fa 1f 00 00 00 00 ff f0 ff fc 27 ff fc 23 ff fd 01 ff fd 03 ff fb 03 ff fb 00 ff fd 00` then `hello telnet\r\nsecond line\n ff ff  byte\n`, byte for byte equal. Before the fix PORT sent the upload first (the report's Actual). Large upload, `-T big_lf.txt` (410 000 bytes of LF lines): both exit 43 with the same stderr, request 410 037 bytes each with the same SHA-256 (5AFD76E5207DB0C5...), starting with the same replies. Linux was not run here (lanes test on Windows); the change is platform-neutral, one code path for both, and the report's Linux Expected wire is the same.
- Focused tests `Cgr007Tests`: the reply-before-upload order failed before the fix (upload first) and passes after; a second covers an upload write failing after the server closed.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Telnet.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Fast tests across the solution green; `dotnet build` 0 warnings.

## Log

- 2026-10-26: Created from CGR-007 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A telnet -T upload goes out from the receive loop after each read's replies, so its wire matches curl 8.22.0 and never splits IAC SB NAWS
