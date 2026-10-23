---
id: BL-3170
title: Keep -w transfer values when a write error ends the transfer with exit 23
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerWriteOutAfterWriteFailureTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3170 — Keep -w transfer values when a write error ends the transfer with exit 23

## Goal

When writing the output fails (exit 23) after the response arrived, `-w` still reports the real transfer values (`%{http_code}`, `%{size_download}` and the rest), as curl does.

## Context

Stewart reported on 2026-10-22: write to a path that fails (exit 23); upstream still prints `%{http_code}` 200 and the real `%{size_download}`; Curl prints `000` and `0`. The cause is probably in `Curl.Console/CurlCommandRunner.cs`: `StandardOutputWriteFailure` (near line 477) is a fresh `TransferResult` with 0 bytes and no report, returned in place of the handler's real result near line 4553 (`result.IsSuccess && state.StandardOutput.HasWriteFailed ? StandardOutputWriteFailure : result`), and other write failures (a failing `-o` path or `-D` file) are built the same way; `FormatErrorLine` compares these by reference near line 3148, so the prefix logic must keep working. `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs` builds the values from `result.Report`. Keep the failure's exit code (23) and its message text (`curl: Failed writing body` etc.), but carry the real `Report` and bytes into the result the write-out sees. Measure curl 8.22.0 with `Record-CurlExchange.ps1` for the write-out values after a write failure for the standard-output case and the `-o` case that fails after the headers have arrived (for example a closed stdout or a path that becomes unwritable), and pin them. `CurlCommandRunner.cs` is a very large shared file; keep the change small.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests/CurlCommandRunnerWriteOutAfterWriteFailureTests.cs` forces a write failure after the response headers arrive (a fake output stream that throws on the first body write) and expects `-w '%{http_code} %{size_download}'` to print the real values curl 8.22.0 prints, with exit 23.
- [ ] A second test does the same for a failing `-o` file, and a third for a failing `-D` file, each pinned to the measured values.
- [ ] The existing `curl: Failed writing body` and `(23)` line tests still pass unchanged (reference comparison logic kept).
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; the tests use no Windows-only path.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: ff73b229 26b6e74e 9d4e79dd 6168a654.
