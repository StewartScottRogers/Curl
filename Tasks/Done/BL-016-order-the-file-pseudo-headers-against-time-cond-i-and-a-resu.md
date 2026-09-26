---
id: BL-016
title: Order the file:// pseudo-headers against --time-cond, -I and a resume failure
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-25
completed: 2026-09-26
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

- [x] A test named `ExecuteAsync_UnmetTimeCondition_WritesNoHeaders` gives a
      `FakeTransferContext` both an `Output` and a `HeaderOutput`
      (`ChunkRecordingStream`) and an `IfModifiedSince` condition later than the fake
      file's `LastWriteTimeUtc`, and asserts nothing was written to either stream,
      `BytesTransferred` is 0 and `ExitCode` is `CurlExitCode.Ok`.
- [x] A test named `ExecuteAsync_NoBodyWithHeaderOutput_WritesHeadersOnly` asserts the
      exact pseudo-header bytes reach `HeaderOutput`, `Output` receives nothing, and the
      result is `CurlExitCode.Ok` with `BytesTransferred` 0.
- [x] A test named `ExecuteAsync_ResumePastEndWithHeaderOutput_WritesHeadersThenFails`
      sets `ResumeFrom` strictly past the fake file's length and asserts the full header
      block was written to `HeaderOutput` **and** the result is
      `CurlExitCode.BadDownloadResume` (36) with `ErrorMessage`
      `failed to resume file:// transfer`.
- [x] `MeetsTimeCondition` is evaluated before any write to `HeaderOutput` in
      `DownloadFromAsync`, and the class remarks list the order actually implemented.
- [x] Every existing test in `Curl.Protocol.File.UnitTests` still passes, or is
      corrected in this task when it pinned the old ordering; the commit message names
      any test whose expectations changed.
- [x] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
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

Delivery (2026-09-26, dark factory lane 4): the `feature` pipeline was run in-session
rather than through the architect/implementer agents, because the change is one
reordering inside `DownloadFromAsync` fully specified by the task's Context. The method
now evaluates `MeetsTimeCondition` first, then `WriteHeadersAsync`, then returns for
`NoBody`, then resolves the window. The class remarks and the method summary state that
order. No existing test pinned the old ordering, so no expectations changed; 127 tests
pass (124 before plus the three named above). Range validation stays with BL-013.

## Log

- 2026-09-25: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. file:// writes no headers for an unmet -z, headers only for -I, and headers before a resume-past-end exit 36
