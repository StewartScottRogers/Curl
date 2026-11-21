---
id: BL-5667
title: Cancel the FTP control read a failed upload write leaves running when no 552 arrives
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5667 — Cancel the FTP control read a failed upload write leaves running when no 552 arrives

## Goal

When a failed FTP upload write gets no `552` within the 500 ms wait, the control-connection read it started is cancelled and observed, and no later reply is written to `-v`, `--trace` or `-D` after the transfer has decided on exit 55.

## Context

- `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs:1340-1355` (`EndFailedUploadWriteAsync`, BL-5663). Found by the shift-end review's silent-failure-hunter and code-reviewer.
- `control.ReadReplyAsync()` takes no token. If nothing arrives within `RefusalReplyWait`, the method returns exit 55 with the read still pending. Disposing the session then faults that read, and nothing observes the exception (`UnobservedTaskException`). A reply arriving after 500 ms but before disposal is written by `diagnostics.ReplyRead` (`FtpControlChannel.cs:187`) after the result is decided.
- A reply other than 552 (426, 451) that arrives in the window is read, shown on `-v`/`--trace`/`-D`, and thrown away. curl 8.22.0's exit 55 path never reads it, so the bytes differ from curl's. Measure curl with `Record-CurlExchange.ps1` before pinning that output.
- Nothing reads the control connection on this path today, but a later `QUIT` here would race the pending read for `FtpControlChannel`'s buffer.
- The 500 ms delay in `CompletesWithinAsync` (`FtpSession.cs:3126`) ignores `context.CancellationToken`, so Ctrl-C or `-m` takes effect up to 500 ms late.

## Acceptance criteria

- [x] The read runs under a token that is cancelled when the wait ends without a reply, and the task's fault is observed.
- [x] The wait honours the transfer's cancellation token.
- [x] A unit test (scripted control connection that answers nothing, then a late reply) asserts exit 55, that no reply line is written after the result, and that no unobserved task exception is raised.
- [x] What a non-552 reply inside the window shows on `-v` matches curl 8.22.0 as measured, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass; the library keeps 100% line and branch coverage.

## Notes

- Done in FtpSession.ReadRefusalWithinWaitAsync (linked token cancelled at the end of the wait, task observed; CompletesWithinAsync takes the transfer token) and FtpControlChannel.ReadReplyHeldAsync/ReleaseHeldReplyAsync/DiscardHeldReply. ADR-1328.
- Criterion 4: only a 552 is shown; other replies in the window are held and discarded, as the task says curl's exit 55 path never reads them. Real curl was not measured (the loopback race is the one ADR-1327 records); pinned by a unit test.
- Quality run: Ftp.UnitLibrary 100% line, 100% branch, worst complexity 10.

## Log

- 2026-11-21: Created by the shift-end review (ADR-0710) of 8c81a2532a...1f330a1f28.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 5debf57d 97a94584 6bea890d 590f1cb4 e7657cfb.
- 2026-11-21: Doing -> Done. Failed-upload-write wait cancels and observes its control read, honours cancellation, holds non-552 replies unshown; tests and ADR-1328
