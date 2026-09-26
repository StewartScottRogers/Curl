---
id: BL-027
title: Refuse a negative or non-numeric --continue-at in the option parser with exit 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-037]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-027 - Refuse a negative or non-numeric `--continue-at` in the option parser

## Goal

`-C`/`--continue-at` with a negative or non-numeric value is refused by the option
parser with exit 2, before any URL is looked at, exactly as upstream curl does.

## Context

Measured against curl 8.21.0 on 2026-09-25 (conformance re-audit of BL-008). Upstream
rejects the value in the option parser, not during the transfer:

```
curl: option -C: expected a proper numerical parameter
curl: try 'curl --help' or 'curl --manual' for more information
```

Exit code 2, nothing on stdout. The long form says `option --continue-at:` instead.
It fires before the URL is examined, so `-C -5` against a nonexistent file is still
exit 2, never exit 3 or 37. Rejected values include `-5`, `-1`, `-0`, `abc` and `1e3`.

`FileProtocolHandler` currently answers a negative `ResumeFrom` itself, returning exit
36 `failed to resume file:// transfer`, and only after URL parsing - so today a bad URL
combined with `-C -5` gives exit 3 where upstream gives exit 2. That fallback was added
under BL-008 because no option parser exists yet; it is a divergence recorded only in an
XML comment, which is not good enough.

## Acceptance criteria

- [x] The option parser in `Curl.Cli.UnitLibrary` rejects a `-C`/`--continue-at` value
      that is negative or not a whole number, returning `CurlExitCode` 2 with no URL
      parsed and nothing written to the output stream.
- [x] The two stderr lines match upstream byte for byte, including the trailing newline
      of each, with `option -C:` for the short form and `option --continue-at:` for the
      long form. Tests assert the exact bytes.
- [x] `-C -5`, `-C -1`, `-C -0`, `-C abc` and `-C 1e3` are each covered by a test.
- [x] A test proves the refusal precedes URL handling: `-C -5` with a malformed URL
      returns exit 2, not exit 3.
- [x] `FileProtocolHandler`'s negative-`ResumeFrom` branch is then either deleted, or
      kept and recorded in an ADR as an unreachable defensive default with the reason.
      Whichever is chosen, the XML comment stops being the only record of it.
- [x] `dotnet build -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Depends on a command-line option parser existing in `Curl.Cli.UnitLibrary`, which is not
written yet - this task cannot start before there is somewhere to put the check. It is
filed now so the divergence is on the board rather than living in a code comment.

`--continue-at -` (a literal dash, meaning "resume from wherever the local file ends") is
a different thing and is accepted upstream; do not reject it.

Run of 2026-09-26 (dark factory lane 4):

- BL-013 had already put the check in the parser. This run added the missing `-C -5`
  short-form row, plus `Parse_NegativeContinueAtWithAMalformedUrl_IsRefusedOnTheOptionWithExit2`
  and `Parse_NegativeContinueAtWithNoUrl_IsRefusedOnTheOptionNotForTheMissingUrl` in
  `Curl.Cli.UnitTests/CommandLineRangeOptionTests.cs`.
- Choice: the refusal-before-URL proof sits at the parser level, not in
  `Curl.Console.UnitTests`, because that project is outside `touches`.
  `CurlCommandRunner.RunAsync` returns the refusal's exit code before it dispatches
  anything. The existing
  `RunAsync_ParserRefusal_PrintsEachLineWithNewLineAndRunsNoHandler` already pins how
  each refusal line gets its terminator, so the parser tests assert the exact line text
  and the console test covers the trailing newline.
- Choice: the negative-`ResumeFrom` branch in `FileProtocolHandler` was kept and recorded
  as ADR-0007. Deleting it would leave `TryResolveWindow` with a negative start and the
  upload skip with a negative count for any hand-built context. Changing the
  `ITransferContext` contract is outside this task. The XML comment now points to the
  ADR, and FR-006 in `Requirements.md` no longer lists the question as open.

## Log

- 2026-09-25: Created.
- 2026-09-26: BL-013 added the `-C`/`--continue-at` row, whose value check already refuses `-5`, `-1`, `-0`, `abc` and `1e3` with `expected a proper numerical parameter` (tests in `Curl.Cli.UnitTests/CommandLineRangeOptionTests.cs`). What remains here: the negative-`ResumeFrom` branch in `FileProtocolHandler` (delete, or record in an ADR), and the console-level check that `-C -5` with a malformed URL is exit 2.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -C/--continue-at negative or non-numeric is refused by the parser with exit 2 before any URL, and the file handler's leftover guard is recorded in ADR-0007
