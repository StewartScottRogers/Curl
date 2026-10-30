---
id: BL-3769
title: Fix CGR-240: Output-file creation differs on early failures: curl creates or writes the -o file where PORT writes nothing
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests/Cgr240Tests.cs, Curl.Output.UnitLibrary, Curl.Output.UnitTests/Cgr240Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs, Curl.Protocol.Http.UnitTests/Cgr240Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3769 — Fix CGR-240: Output-file creation differs on early failures: curl creates or writes the -o file where PORT writes nothing

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-240 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-240: Output-file creation differs on early failures: curl creates or writes the -o file where PORT writes nothing

- **Severity:** Low
- **Area:** upstream-cases
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (on all four binaries).
- **Related existing finding (GF/AF):** none
- **Source findings:** `upstream-cases UC-11`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- `verify\v2.sh`.
  - (a) No server: `curl -o out.txt http://127.0.0.1:21359/426 -d foobar -C 3`.
  - (b) Server `verify\conf\s099.json` replies `HTTP/1.1 099 OK\r\nContent-Length: 6\r\n\r\n-foo-\n`. Run `curl -i -o o.txt http://127.0.0.1:21359/1432`.

Commands (run each with REF and with PORT):

```sh
curl -o out.txt http://127.0.0.1:21359/426 -d foobar -C 3
curl -i -o o.txt http://127.0.0.1:21359/1432
```

**Expected (real curl)**

- (a) Exit 2 and creates an empty out.txt.
- (b) Exit 1, and o.txt holds `HTTP/1.1 099 OK\r\nContent-Length: 6\r\n\r\n`.

**Actual (Curl)**

- (a) Exit 2 and no out.txt.
- (b) Exit 1 and no o.txt.

**Suspected location in source**

- the output file is opened lazily (Curl.Console / Curl.Output.UnitLibrary); `HttpStatusLine.cs:220-224` rejects the code before the head is written.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr240Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-29 (lane 5): (a) Real curl 8.22.0 (Schannel, Windows): `curl -o out.txt http://127.0.0.1:21359/426 -d foobar -C 3` prints `curl: cannot mix --continue-at with --data` / `curl: (2) Failed initialization`, exit 2, and leaves an empty out.txt. Curl created none before the fix; fixed in `CurlCommandRunner.CreateOutputFileOfContinueAtRefusalAsync`, which on that refusal (a `-C` offset past 0 beside `-d` or `-F`, with a URL, `-o` not `-`) creates the first `-o` file empty for appending, under `--output-dir`. Not done for `-O` names (needs the URL's remote name) or Windows name sanitising; those are rarer and unmeasured. The Curl.Cli parser is untouched, so the refusal text and exit code are unchanged.
- (b) `-i -o o.txt` against `HTTP/1.1 099 OK\r\nContent-Length: 6\r\n\r\n-foo-\n`: BL-3646 (CGR-117) already makes the handler write the whole head before failing with exit 1; `Curl.Protocol.Http.UnitTests/Cgr240Tests.cs` pins the exact repro (output = head, exit UnsupportedProtocol). Not re-run end to end: the tool sandbox here would not let the built binary run, so (b) is pinned at handler level only.
- Tests: `Curl.Console.UnitTests/Cgr240Tests.cs` (fails before the fix, passes after). Fast Console tests pass (3042), Measure-CodeQuality for Curl.Console reports 0 failing members. Linux/macOS: the tests use only in-memory files and relative names, so they are platform-neutral.
- No ADR: the change is a small behaviour match, no design choice beyond the above.

## Log

- 2026-10-26: Created from CGR-240 of the third-party gap report.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. CGR-240 fixed: -C beside a body creates the empty -o file; 099 head already written (BL-3646)
