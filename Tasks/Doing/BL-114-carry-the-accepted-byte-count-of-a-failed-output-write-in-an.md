---
id: BL-114
title: Carry the accepted byte count of a failed output write in an IOException subtype
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-090]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-114 — Carry the accepted byte count of a failed output write in an IOException subtype

## Goal

When a write to standard output fails, `StandardOutputFailureDeferringStream` throws an
`OutputWriteFailedException` (a new `IOException` subtype in
`Curl.Protocol.Abstractions.UnitLibrary`) whose `BytesAccepted` property is how many bytes
of that write curl 8.21.0's 4096-byte stdio buffer would have taken, so protocol handlers
can report curl's `passed N returned M` with the right `M`.

## Context

BL-099 (see its Notes, "Measurements, 2026-09-26") measured curl 8.21.0
(x86_64-w64-mingw32) with standard output closed (`2>&1 >&-`). For `telnet://`, a
loopback listener sending `count` lines of `size` bytes, 5 ms apart:

| size x count | curl 8.21.0 stderr | exit | ours |
| --- | --- | --- | --- |
| 100 x 100 | `curl: (23) Failure writing output to destination, passed 100 returned 96` | 23 | `... passed 100 returned 0` |
| 300 x 100 | `curl: (23) Failure writing output to destination, passed 300 returned 196` | 23 | `... passed 300 returned 0` |
| 1000 x 20 | `curl: (23) Failure writing output to destination, passed 1000 returned 96` | 23 | `... passed 1000 returned 0` |
| 30 x 400 | `curl: (23) Failure writing output to destination, passed 30 returned 16` | 23 | `... passed 30 returned 0` |
| 5000 x 5 | `curl: (23) Failure writing output to destination, passed 4096 returned 0` | 23 | `... passed 10000 returned 0` |

and for `file://`, single large writes:

| N | curl 8.21.0 stderr | exit |
| --- | --- | --- |
| 4096 | `curl: (23) Failure writing output to destination, passed 4096 returned 0` | 23 |
| 16385 | `curl: (23) Failure writing output to destination, passed 16384 returned 0` | 23 |

BL-099's rule: `M` is the room left in the 4096-byte stdio buffer when the overflowing
write arrives (4096 = 40x100 + 96, 13x300 + 196, 4x1000 + 96, 136x30 + 16). A write that
fails on its own, with nothing absorbed before it (the 4096-byte and larger single
writes above), reports `M = 0`.

Today `Curl.Console/StandardOutputFailureDeferringStream.cs` (from BL-090), after the
first `IOException` from the inner stream, absorbs writes and counts them in
`bytesOfferedSinceFailure`, the failing write included; in `AbsorbIntoStdioBuffer`, once
that count reaches `StdioBufferSize` (4096) it throws a plain `IOException`. In the 100 x
100 case write 1 fails, writes 1..40 are absorbed (4000 bytes), and write 41 throws: the
room before it is 4096 - 4000 = 96, which is curl's `M`.

This task adds the contract and makes the stream throw it. It does not change any
protocol library; BL-115 (telnet), BL-116 (file), BL-117 (gopher) and BL-118 (MQTT) read
`BytesAccepted` afterwards. Because the new type derives from `IOException`, every
existing `catch (IOException)` in the handlers keeps working unchanged.

Rules for `BytesAccepted` on the throwing write, with `before` = `bytesOfferedSinceFailure`
before this write is added:

- If the write is the one whose inner write just failed (`before` is 0 and nothing was
  absorbed yet), `BytesAccepted` is 0.
- Otherwise `BytesAccepted = max(0, StdioBufferSize - before)`.
- Every later write (after the buffer is already full) has `BytesAccepted` 0.

## Acceptance criteria

- [ ] `Curl.Protocol.Abstractions.UnitLibrary/OutputWriteFailedException.cs` declares a
      public sealed `OutputWriteFailedException : IOException` with a public `int
      BytesAccepted` (read-only, set by a constructor taking the count and a message;
      a negative count throws `ArgumentOutOfRangeException`), and XML doc comments that
      state what `BytesAccepted` means.
- [ ] `Curl.Protocol.Abstractions.UnitTests` has tests pinning the constructor, the
      property, the negative-count guard, and that the type is an `IOException`.
- [ ] `StandardOutputFailureDeferringStream` throws `OutputWriteFailedException` instead of
      a plain `IOException`, with `BytesAccepted` per the rules in Context; its remarks and
      `<exception>` docs say so.
- [ ] `Curl.Console.UnitTests` has tests (with a fake inner stream that throws
      `IOException`) showing: 41 writes of 100 bytes after the failing first write gives
      `BytesAccepted` 96 on write 41; 300-byte writes give 196 on write 14; 1000-byte
      writes give 96 on write 5; 30-byte writes give 16 on write 137; a single 4096-byte
      failing write gives 0; a single 16384-byte failing write gives 0; a write after the
      throwing one gives 0. Both `Write` and `WriteAsync` are covered.
- [ ] The existing `Curl.Console.UnitTests` for BL-090's `curl: Failed writing body` and
      `passed N returned 0` cases still pass unchanged.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` and
      `dotnet build Curl.Console -warnaserror` are clean, and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
