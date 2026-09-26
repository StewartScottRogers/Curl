---
id: BL-017
title: Compare --time-cond at whole-second resolution, strictly in both directions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008, BL-016]
requirement: none
created: 2026-09-25
completed:
---
# BL-017 — Compare `--time-cond` at whole-second resolution, strictly in both directions

## Goal

`FileProtocolHandler.MeetsTimeCondition` truncates both sides of the comparison to whole
seconds and compares strictly, so that at exact equality neither `-z <date>` nor
`-z -<date>` transfers, as curl 8.21.0 does.

## Context

Measured on this machine against curl 8.21.0 (Release-Date 2026-06-24): a local file's
modification time is whole seconds as curl sees it, and when the `-z` argument equals
that timestamp exactly, **neither** direction transfers — not `-z <date>`
(`IfModifiedSince`) and not `-z -<date>` (`IfUnmodifiedSince`). curl documents `-z` as
`Transfer based on a time condition` (`curl --help all`,
<https://curl.se/docs/manpage.html>), and an unmet condition is a success: exit 0 with no
body.

`Curl.Protocol.File.UnitLibrary\FileProtocolHandler.cs` has:

```csharp
condition is null
|| (condition.Kind == TimeConditionKind.IfModifiedSince
    ? lastWriteTimeUtc > condition.Value
    : lastWriteTimeUtc <= condition.Value);
```

Two defects: the comparison runs at full `DateTimeOffset` precision, so a file 200
milliseconds newer than the condition transfers where curl (seeing both as the same
second) does not; and `IfUnmodifiedSince` uses `<=`, so exact equality transfers where
curl does not.

The wanted rule, with `t` the file timestamp and `c` the condition value, both truncated
to whole seconds: `IfModifiedSince` transfers when `t > c`; `IfUnmodifiedSince` transfers
when `t < c`; at `t == c` neither transfers. Truncating the condition value as well as the
file timestamp is deliberate: curl's date parser yields whole seconds, so truncating both
cannot change a curl-reachable case and it makes the comparison total for a caller that
supplies fractional seconds.

`ITransferContext.TimeProvider` stays unused — `-z` compares against the timestamp the
open reported, never against now.

## Acceptance criteria

- [ ] `MeetsTimeCondition` truncates both operands to whole seconds (for example with
      `DateTimeOffset` arithmetic on `Ticks` and `TimeSpan.TicksPerSecond`) and uses `>`
      for `IfModifiedSince` and `<` for `IfUnmodifiedSince`.
- [ ] A test named `ExecuteAsync_IfModifiedSinceEqualToFileTime_TransfersNothing`
      asserts exit 0, `BytesTransferred` 0 and an empty `Output`.
- [ ] A test named `ExecuteAsync_IfUnmodifiedSinceEqualToFileTime_TransfersNothing`
      asserts the same three things: this is the `>` versus `>=` boundary, and it fails
      against the current `<=`.
- [ ] A test named `ExecuteAsync_FileNewerBySubSecondOnly_TransfersNothing` uses a file
      timestamp 200 milliseconds after an `IfModifiedSince` value in the same second and
      asserts nothing transfers.
- [ ] Tests assert each direction still transfers when it should: a file one whole second
      newer than an `IfModifiedSince` value transfers every byte; a file one whole second
      older than an `IfUnmodifiedSince` value transfers every byte.
- [ ] No `HeaderOutput` bytes are written for any of the non-transferring cases above,
      which is the behaviour BL-016 establishes.
- [ ] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Depends on BL-016 because BL-016 moves the time-condition check ahead of the header write
in the same method, and the criteria above assume that order.

`TimeCondition` in `Curl.Protocol.Abstractions.UnitLibrary` needs no change: truncation is
the handler's comparison rule, not a change to what the command line layer parses. Say so
in the `MeetsTimeCondition` remarks so a reader does not go looking for it in the
contract.

## Log

- 2026-09-25: Created.
