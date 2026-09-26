---
id: BL-016
title: Order the file:// pseudo-headers against --time-cond, -I and a resume failure
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008]
requirement: none
created: 2026-09-25
completed:
---
# BL-016 — Order the `file://` pseudo-headers against `--time-cond`, `-I` and a resume failure

## Goal

`FileProtocolHandler` writes the synthesised header block only where curl 8.21.0 writes
it: after the `-z`/`--time-cond` decision and before the range or resume decision, so an
unmet time condition emits no headers at all.

## Context

Measured on this machine against curl 8.21.0 (Release-Date 2026-06-24):

- `curl -i -z <a date in the future> <an older file>` prints **nothing** — no header
  block, no body — and exits 0. The header block is suppressed when the time condition
  is not met.
- Headers *are* written before a range or resume failure: `curl -i -r -50 <file>`
  printed the three pseudo-headers (`Content-Length`, `Accept-ranges`, `Last-Modified`)
  and then exited 36.
- `-I`/`--head` prints the header block and no body, exit 0.

`Curl.Protocol.File.UnitLibrary\FileProtocolHandler.cs`, in `DownloadFromAsync`, calls
`TryWriteHeadersAsync` first and only then evaluates `context.NoBody` and
`MeetsTimeCondition`, so for an unmet `-z` it emits a block curl suppresses. The order
the method needs is: open, evaluate the time condition (unmet is
`TransferResult.Success(0)` with nothing written anywhere), write the headers, return for
`NoBody`, resolve the window, copy.

The remarks on the `FileProtocolHandler` class state the current, wrong order ("open,
write the pseudo-headers, return early for `-I`/`--head`, apply `-z`/`--time-cond`") and
change with the code.

## Acceptance criteria

- [ ] A test named `ExecuteAsync_UnmetTimeCondition_WritesNoHeaders` gives a
      `FakeTransferContext` both an `Output` and a `HeaderOutput`
      (`ChunkRecordingStream`) and an `IfModifiedSince` condition later than the fake
      file's `LastWriteTimeUtc`, and asserts nothing was written to either stream,
      `BytesTransferred` is 0 and `ExitCode` is `CurlExitCode.Ok`.
- [ ] A test named `ExecuteAsync_NoBodyWithHeaderOutput_WritesHeadersOnly` asserts the
      exact pseudo-header bytes reach `HeaderOutput`, `Output` receives nothing, and the
      result is `CurlExitCode.Ok` with `BytesTransferred` 0.
- [ ] A test named `ExecuteAsync_ResumePastEndWithHeaderOutput_WritesHeadersThenFails`
      sets `ResumeFrom` strictly past the fake file's length and asserts the full header
      block was written to `HeaderOutput` **and** the result is
      `CurlExitCode.BadDownloadResume` (36) with `ErrorMessage`
      `failed to resume file:// transfer`.
- [ ] `MeetsTimeCondition` is evaluated before any write to `HeaderOutput` in
      `DownloadFromAsync`, and the class remarks list the order actually implemented.
- [ ] Every existing test in `Curl.Protocol.File.UnitTests` still passes, or is
      corrected in this task when it pinned the old ordering; the commit message names
      any test whose expectations changed.
- [ ] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

The measured `curl -i -r -50` case exits 36 from a *range*, where this handler's
`TryResolveRange` cannot fail for a suffix form (`Math.Max(0, length - suffix)` is never
past the end), so the ordering test above uses `ResumeFrom`, which does reach exit 36.
Whether a suffix range should ever produce exit 36 — and what `-r -0`, `-r 3-1` and
`-r abc` do — is range validation, and belongs to BL-013, whose acceptance criteria now
name those cases. Do not widen this task into range parsing.

This task touches `Curl.Protocol.File.UnitLibrary` and its tests only. No
`Curl.Protocol.Abstractions.UnitLibrary` change and no ADR change.

## Log

- 2026-09-25: Created.
