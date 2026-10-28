---
id: BL-3639
title: Fix CGR-110: Closed stdin with @- is read as empty and the request is sent
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3570, BL-3893]
touches: [Curl.Cli.UnitLibrary/DiskDataFileReader.cs, Curl.Cli.UnitLibrary/ClosedStandardInputStream.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/VariableDefinition.cs, Curl.Cli.UnitLibrary/ConfigFileApplier.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Engine.UnitLibrary/MimePostForm.cs, Curl.Cli.UnitTests/Cgr110Tests.cs, Curl.Console.UnitTests/Cgr110Tests.cs, Curl.Console/Program.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3639 — Fix CGR-110: Closed stdin with @- is read as empty and the request is sent

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-110 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-110: Closed stdin with @- is read as empty and the request is sent

- **Severity:** Medium
- **Area:** network-robustness
- **Platform(s):** Windows. Detail: Windows. On Linux a closed fd 0 is reused by the socket and both binaries hang.
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `network-robustness NR-9`

**Reproduction**

- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- *(Git Bash):* `curl -sS -d @- $H/ok <&-`; same with `--data-binary @-`, `--json @-`, `-F f=@-`.

Commands (run each with REF and with PORT):

```sh
curl -sS -d @- $H/ok <&-
```

**Expected (real curl)**

- -d/--data-binary/--json give exit 26 `curl: option -d: error encountered when reading a file` and nothing is sent. -F gives exit 42 `operation aborted by callback`; the request head is sent and the server's response is printed before the abort.

**Actual (Curl)**

- exit 0. A POST with an empty body (an empty multipart for -F) is sent and the response `aaaaa` is printed.

**Suspected location in source**

- `Curl.Console/Program.cs:19`, `Curl.Cli.UnitLibrary/DiskDataFileReader.cs:33` (Console.OpenStandardInput gives Stream.Null for an invalid handle).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr110Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 6, first claim, no code edited): measured real curl 8.22.0 (Git for Windows ucrt64) from Git Bash with `<&-`, URL `http://127.0.0.1:1/ok`. Exit 26, two lines `curl: option <opt>: error encountered when reading a file` + the try-help line, no `Failed to open` line, nothing sent, for: `-d @-`, `--data-binary @-`, `--json @-`, `--data-urlencode @-`, `--data-urlencode x@-`, `--url-query @-`, `--variable x@-`, `-d @- -d @-`, `-d @- -K -`. Read as empty (went on to connect, exit 7): `-K -`, `-H @-`, `--url @-`, `-T -`, `-F f=@-` (the -F abort, exit 42, comes only once a server answers). With `</dev/null` (NUL) `-d @-` reads empty (exit 7), so only a closed handle fails.
- Plan (decided, record in an ADR when coding): .NET's `Console.OpenStandardInput()` gives `Stream.Null` for an invalid handle, and tests already pass `Stream.Null` meaning "empty", so `Program.cs` swaps `Stream.Null` for a new public `Curl.Cli.ClosedStandardInputStream` (reads as end of file, like `ClosedStandardOutputStream` in Curl.Console). `DiskDataFileReader.ReadStandardInput` throws `IOException` for it, as `IDataFileReader` already documents. Callers: `CommandLineOptionTable.ReadAtFile` catches it and refuses with a new `CommandLineRefusal.StandardInputUnreadable(spelledOption)` (exit 26, no `Failed to open` line, like `UrlFileUnreadable`), except the `-H @-` header path, which reads it as empty; `VariableDefinition.ReadFile` refuses the same way; `ConfigFileApplier` (`-K -`), `SetEchConfigList` and `AddUrlsFromFile` read it as empty. For `-F f=@-`, `MimePostForm.WithStandardInput`'s read callback returns the read-abort result when the stream is a `ClosedStandardInputStream`, so the transfer ends 42 after the head is sent; check the head and the printed response against real curl with a loopback server.
- Requeued: `CommandLineOptionTable.cs` is held by BL-3570 and `CommandLineRefusal.cs` by BL-3893 (both in Doing on work/dark-factory). Widened touches (this note) for the files the plan above needs.

## Log

- 2026-10-26: Created from CGR-110 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waiting on BL-3570 (holds Curl.Cli.UnitLibrary/CommandLineOptionTable.cs) and BL-3893 (holds Curl.Cli.UnitLibrary/CommandLineRefusal.cs); measurements and plan in Notes
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-3639-20261028-052757-L9.jsonl
- 2026-10-28: Backlog -> Deferred. Split into BL-3968, BL-3969 and BL-3970 (over its cost cap)
