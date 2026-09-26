---
id: BL-027
title: Refuse a negative or non-numeric --continue-at in the option parser with exit 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
requirement: none
created: 2026-09-25
completed:
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

- [ ] The option parser in `Curl.Cli.UnitLibrary` rejects a `-C`/`--continue-at` value
      that is negative or not a whole number, returning `CurlExitCode` 2 with no URL
      parsed and nothing written to the output stream.
- [ ] The two stderr lines match upstream byte for byte, including the trailing newline
      of each, with `option -C:` for the short form and `option --continue-at:` for the
      long form. Tests assert the exact bytes.
- [ ] `-C -5`, `-C -1`, `-C -0`, `-C abc` and `-C 1e3` are each covered by a test.
- [ ] A test proves the refusal precedes URL handling: `-C -5` with a malformed URL
      returns exit 2, not exit 3.
- [ ] `FileProtocolHandler`'s negative-`ResumeFrom` branch is then either deleted, or
      kept and recorded in an ADR as an unreachable defensive default with the reason.
      Whichever is chosen, the XML comment stops being the only record of it.
- [ ] `dotnet build -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Depends on a command-line option parser existing in `Curl.Cli.UnitLibrary`, which is not
written yet - this task cannot start before there is somewhere to put the check. It is
filed now so the divergence is on the board rather than living in a code comment.

`--continue-at -` (a literal dash, meaning "resume from wherever the local file ends") is
a different thing and is accepted upstream; do not reject it.

## Log

- 2026-09-25: Created.
