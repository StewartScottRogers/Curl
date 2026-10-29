---
id: BL-3660
title: Fix CGR-131: sftp -r/--range ignores text after the first range, and handles '0 - 9' and '0x10-0x20' differently from REF
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3570]
touches: [Curl.Core.UnitLibrary/ByteRangeParser.cs, Curl.Core.UnitTests/Cgr131Tests.cs, Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs, Curl.Protocol.Ssh.UnitTests/Cgr131Tests.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr131Tests.cs, Curl.Protocol.Ssh.UnitTests/Sftp/SftpFileDownloadTests.RangeText.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3660 — Fix CGR-131: sftp -r/--range ignores text after the first range, and handles '0 - 9' and '0x10-0x20' differently from REF

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-131 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-131: sftp -r/--range ignores text after the first range, and handles '0 - 9' and '0x10-0x20' differently from REF

- **Severity:** Medium
- **Area:** sftp-scp-ssh
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** AF-0044
- **Source findings:** `sftp-scp-ssh SSH-04`

**Reproduction**

- *Setup (sftp-scp-ssh):* E = `SCR\evidence\sftp-scp-ssh`. Server: a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21523 (`E\setup.sh`, then `E\setup2.sh`; `E\remote2.sh reset` rebuilds the test tree; Windows reaches it through WSL localhost forwarding). Odd servers: `E\oddstart.sh` (21531 accepts and stays silent 60 s; 21533 sends `SSH-1.5-old` and then nothing). `AUTH` = `-k -u stewart_rogers: --key E\keys\c_rsa_pem --pubkey E\keys\c_rsa_pem.pub`; `U` = `sftp://127.0.0.1:21523/home/stewart_rogers/sftpt21501`; `UC` = the same with `scp://`. Verifier: `E\verify\vwin.sh`, `E\verify\vlin.sh`, `E\verify\vwinlog.sh`.
- `curl -sS AUTH -r "<R>" -w "<%{size_download}>" U/digits.txt`. The file is `seq 1 100` (292 bytes). R is each of `0-1,5-6`, `0-9x`, `0-1,3`, `1-2-3`, `0-0,0-0`, `0 - 9` and `0x10-0x20`.

  | Range | REF | PORT |
  |---|---|---|
  | `0-1,5-6` | exit 33, nothing written | exit 0, 2 bytes |
  | `0-9x` | exit 33, nothing written | exit 0, 10 bytes |
  | `0-1,3` | exit 33, nothing written | exit 0, 2 bytes |
  | `1-2-3` | exit 33, nothing written | exit 0, 2 bytes |
  | `0-0,0-0` | exit 33, nothing written | exit 0, 1 byte |
  | `0 - 9` | exit 0, whole file (292 bytes) | exit 0, 10 bytes |
  | `0x10-0x20` | exit 0, whole file (292 bytes) | exit 33 |

Commands (run each with REF and with PORT):

```sh
curl -sS AUTH -r "<R>" -w "<%{size_download}>" U/digits.txt
```

**Expected (real curl)**

- See the REF column of the table under Reproduction: exit 33 `curl: (33) Requested range was not delivered by the server` with nothing written for `0-1,5-6`, `0-9x`, `0-1,3`, `1-2-3` and `0-0,0-0`; exit 0 with the whole file (292 bytes) for `0 - 9` and `0x10-0x20`. REF ignores -r for scp://.

**Actual (Curl)**

- See the PORT column of the table under Reproduction: exit 0 with 2, 10, 2, 2 and 1 bytes for `0-1,5-6`, `0-9x`, `0-1,3`, `1-2-3` and `0-0,0-0`; exit 0 with 10 bytes for `0 - 9`; exit 33 for `0x10-0x20`. PORT also ignores -r for scp://, so scp matches.

**Notes**

- The exit 33 message is `curl: (33) Requested range was not delivered by the server`.
- REF and PORT both ignore -r for scp://, so scp matches.

**Suspected location in source**

- `Curl.Core.UnitLibrary/ByteRangeParser.cs:59`. TryParse never rejects trailing text. `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs` uses that parsed range before it checks the raw range text.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr131Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 9, no code edited): two causes, both found.
  1. `0 - 9` and `0x10-0x20` (whole file in REF) are curl 8.22.0's tool, not libcurl: its `-r` parser appends a dash ("A specified range MUST include at least one dash (-). Appending one for you") whenever `curlx_str_number` reads a leading number that fits and the next character is not `-`, so both become `0-`. `CommandLineOptionTable.SetRange` only does it when the value starts with a digit and has no dash anywhere. Measured with real curl 8.22.0 (Schannel) over `file://` on a 292-byte `seq 1 100` file: `0 - 9`, `0x10-0x20`, `0 -9` -> dash warning, exit 0, 292 bytes; `5`, `5 ` -> dash warning, 287 bytes; `0-9x` -> invalid-character warning, 10 bytes; `1-2-3`, `0-1,5-6` -> no warning, 2 bytes; `99999999999999999999 - 3` -> invalid-character warning (overflow keeps the text verbatim), exit 33; `-5 x` -> invalid-character warning, 5 bytes. Fix: in `SetRange`, append the dash when the leading digits parse as a `long` and are not followed by `-`; otherwise keep the text (with the invalid-character warning when due). Pin it in `Curl.Cli.UnitTests/Cgr131Tests.cs`.
  2. `0-1,5-6`, `0-9x`, `0-1,3`, `1-2-3`, `0-0,0-0` (exit 33 in REF): `SftpDownloadPart.Asked` uses the `ByteRangeParser` range (which, rightly for file:// and FTP, ignores trailing text) before the raw text. Fix: when `rangeText` is not null, read it with `WithinText` (Curl_ssh_range) and use the parsed range only when there is no text. `ByteRangeParser` needs no change. Pin it in `Curl.Protocol.Ssh.UnitTests/Cgr131Tests.cs` against `SftpDownloadPart.Choose`.
- 2026-10-26: Added `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` and `Curl.Cli.UnitTests/Cgr131Tests.cs` to touches for cause 1; BL-3570 (Doing on work/dark-factory) holds `CommandLineOptionTable.cs`, so it is in depends-on and the task goes back to Backlog until that is Done.
- 2026-10-28 (lane 7): fixed both causes as lane 9 planned (ADR-0867). `SetRange` appends the dash when the leading digits fit a `long` and the next character is not `-`; `SftpDownloadPart.Asked` reads the `-r` text with `WithinText` whenever it is given. `ByteRangeParser.cs` and `Curl.Core.UnitTests/Cgr131Tests.cs` needed no change.
- 2026-10-28: added `Curl.Protocol.Ssh.UnitTests/Sftp/SftpFileDownloadTests.RangeText.cs` to touches: its `Choose_RangeAndRangeText_ReadsTheRange` pinned the old order (range before text), so it is now `Choose_RangeAndRangeText_ReadsTheText`. No task in Doing on work/dark-factory names it.
- 2026-10-28 measured: throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21660 (KexAlgorithms +diffie-hellman-group14-sha256,diffie-hellman-group-exchange-sha256 and an RSA host key so the Windows preset can negotiate), 292-byte `seq 1 100` file, `-sS -k -u stewart_rogers: --key ckey --pubkey ckey.pub -r "<R>" -w "<%{size_download}>" -o out sftp://127.0.0.1:21660/tmp/bl3660/files/digits.txt`. Windows curl 8.22.0 (Git ucrt64, Schannel), Linux curl 8.18.0 (OpenSSL) and Curl (`dotnet run --project Curl.Console`) give the same results: `0-1,5-6`, `0-9x`, `0-1,3`, `1-2-3`, `0-0,0-0` -> `<0>`, exit 33, `curl: (33) Requested range was not delivered by the server`, no file written; `0 - 9`, `0x10-0x20` -> `<292>`, exit 0, 292 bytes; the variants that already matched (`2-5` -> 4 bytes, `-3` -> 3 bytes) still match. Before the fix Curl gave the PORT column. The new tests in `Curl.Cli.UnitTests/Cgr131Tests.cs` and `Curl.Protocol.Ssh.UnitTests/Cgr131Tests.cs` assert the new behaviour, which the old code did not have (old `SetRange` kept `0 - 9`, and old `Asked` read the parsed range first). scp:// ignores -r in both, unchanged.
- 2026-10-28: `dotnet build` clean, the fast tests green (46 test projects passed, none failed). `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Protocol.Ssh.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-131 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Needs Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, which BL-3570 holds; waits on BL-3570
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. sftp -r reads its text as Curl_ssh_range does (exit 33 for text after the first range) and -r appends a dash after any leading number not before a dash, as curl 8.22.0
