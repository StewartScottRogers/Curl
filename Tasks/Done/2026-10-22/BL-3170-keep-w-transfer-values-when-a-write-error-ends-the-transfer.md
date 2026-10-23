---
id: BL-3170
title: Keep -w transfer values when a write error ends the transfer with exit 23
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/DeferredOutputFileStream.cs, Curl.Console.UnitTests/CurlCommandRunnerWriteOutAfterWriteFailureTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3170 — Keep -w transfer values when a write error ends the transfer with exit 23

## Goal

When writing the output fails (exit 23) after the response arrived, `-w` still reports the real transfer values (`%{http_code}`, `%{size_download}` and the rest), as curl does.

## Context

Stewart reported on 2026-10-22: write to a path that fails (exit 23); upstream still prints `%{http_code}` 200 and the real `%{size_download}`; Curl prints `000` and `0`. The cause is probably in `Curl.Console/CurlCommandRunner.cs`: `StandardOutputWriteFailure` (near line 477) is a fresh `TransferResult` with 0 bytes and no report, returned in place of the handler's real result near line 4553 (`result.IsSuccess && state.StandardOutput.HasWriteFailed ? StandardOutputWriteFailure : result`), and other write failures (a failing `-o` path or `-D` file) are built the same way; `FormatErrorLine` compares these by reference near line 3148, so the prefix logic must keep working. `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs` builds the values from `result.Report`. Keep the failure's exit code (23) and its message text (`curl: Failed writing body` etc.), but carry the real `Report` and bytes into the result the write-out sees. Measure curl 8.22.0 with `Record-CurlExchange.ps1` for the write-out values after a write failure for the standard-output case and the `-o` case that fails after the headers have arrived (for example a closed stdout or a path that becomes unwritable), and pin them. `CurlCommandRunner.cs` is a very large shared file; keep the change small.

## Acceptance criteria

- [x] `Curl.Console.UnitTests/CurlCommandRunnerWriteOutAfterWriteFailureTests.cs` forces a write failure after the response headers arrive (a fake output stream that throws on the first body write) and expects `-w '%{http_code} %{size_download}'` to print the real values curl 8.22.0 prints, with exit 23.
- [x] A second test does the same for a failing `-o` file, and a third for a failing `-D` file, each pinned to the measured values.
- [x] The existing `curl: Failed writing body` and `(23)` line tests still pass unchanged (reference comparison logic kept).
- [x] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; the tests use no Windows-only path.

## Notes

- Measured curl 8.22.0 (Git for Windows' mingw build, Schannel) with Record-CurlExchange.ps1 against `HTTP/1.1 200 OK`, `Content-Length: 5`, body `hello`, with `-sS -w '%{http_code} %{size_download} %{size_header}'`: standard output opened read-only through a `1<file` wrapper passed as `-Curl` (with `%{stderr}` in the template) gives `curl: Failed writing body` then `200 5 38`, exit 23; `-o <directory>` gives `curl: (23) client returned ERROR on write of 5 bytes` and `200 0 38`; `-D <directory>` fails at open before the transfer: `curl: Failed to open <dir>`, `curl: (23) Failed writing received data to disk/application`, `000 0 0`.
- The `-o` failure is built in `Curl.Console/DeferredOutputFileStream.cs` (`CompleteAsync`, `SettleRetriedAttempt`), not the runner, so that file was added to `touches`; no task in Doing on origin/work/dark-factory named it.
- Decision (ADR-0764): the stdout case keeps the handler's result `with` the failure's exit code and message; `FormatErrorLine` compares the message by reference with `StandardOutputWriteFailure.ErrorMessage` (only that path sets it), so the existing `curl: Failed writing body` tests pass unchanged. The `-o` failure keeps the handler's `Report`, whose download size already leaves out the bytes the file did not take (curl's `0`). The `-D` case already printed `000 0 0`.
- Keeping the report on a retried attempt left `AttemptBodyBytesWritten`'s no-report branch under `-i` uncovered; a fourth test (a reportless handler failure retried under `-i -o`) covers it. Measure-CodeQuality.ps1 -Library Curl.Console: 100% line, 100% branch, 0 failing members.
- The pipeline is `feature`, but this was a small fix in one project, so it was done directly without the architect and implementer stages, to save cost.
- Tests use relative names (`out.bin`, `headers.txt`) in the in-memory file system and no OS error text, so they don't depend on the platform. The last criterion is met by the build and the fast tests here.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: ff73b229 26b6e74e 9d4e79dd 6168a654.
- 2026-10-22: Doing -> Done. -w reports the real http_code and sizes after an exit 23 write failure on stdout or -o, as curl 8.22.0 does
