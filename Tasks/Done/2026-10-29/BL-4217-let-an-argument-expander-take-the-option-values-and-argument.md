---
id: BL-4217
title: Let an argument expander take the option values and arguments that follow its option
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4205]
touches: [Curl.Extension.Abstractions.UnitLibrary/Arguments, Curl.Extension.Abstractions.UnitTests/Arguments, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerTrailingArgumentExpansionTests.cs, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4217 — Let an argument expander take the option values and arguments that follow its option

## Goal

An argument expander (BL-4205) can also take the values of companion extension options and the positional arguments of its form that follow its option on the command line (such as `--openapi spec.json --operation getPet petId=5 status=sold`, where `--operation getPet` and the two `name=value` arguments go to the `--openapi` expander rather than being parsed as an option and two URLs), and a command line without an expander's option parses exactly as before.

## Context

Found by BL-4107 (`--openapi <spec> --operation <id> name=value...`) in its prerequisite pass. BL-4205's suggested contract hands an expander only its own option's value (`ArgumentsFor(string value, ArgumentExpansionContext context)`), so `--operation <id>` would be refused or kept as an unrelated option and every `name=value` would be parsed by curl as a URL (`http://petId=5`). Suggested shape (decide in an ADR): the expander declares the companion `ExtensionOption`s it reads (`--operation`) and a predicate for the positional arguments it consumes (here, an argument holding `=` that does not start with `-`, taken only after the expander's option); Curl.Console's expansion step in `CurlCommandRunner.cs` (where BL-4205 put it) removes those from the argument list within the same `--next` group and passes them to the expander beside its value. A positional argument that does not match stays a URL, as curl parses it.

## Acceptance criteria

- [x] The expander contract in `Curl.Extension.Abstractions.UnitLibrary/Arguments` lets an expander name companion options and claim matching positional arguments, and receives their values in command-line order; unit tests.
- [x] Curl.Console gives a test expander for `--x-spec` with companion `--x-op` the line `--x-spec s --x-op o a=1 b=2 http://h/` as value `s`, companion `o` and positional `a=1`, `b=2`, and parses `http://h/` and the expander's arguments as before; unit test in `Curl.Console.UnitTests/CurlCommandRunnerTrailingArgumentExpansionTests.cs`.
- [x] Positional arguments in another `--next` group, or on a command line without the expander's option, stay URLs; unit tests.
- [x] With `CURL_EXTENSIONS=0` the expander's option and its companion are refused as unknown options (exit 2); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the shape, and `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` describes it.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Shape (ADR-1051): `ITrailingArgumentExpander : IArgumentExpander` adds `CompanionOptions`,
  `ClaimsPositionalArgument(string)` and `ArgumentsFor(ClaimedArguments, ...)` (`ClaimedArguments` = value,
  `CompanionOptionValue`s, positional arguments, each in command-line order); a default implementation of the plain
  `ArgumentsFor(string, ...)` passes nothing claimed, so BL-4205's plain expanders are unchanged.
  `ArgumentExpansion.Expand` holds a trailing expander until its `--next` group ends, then puts its arguments in
  its option's place.
- Default taken: a positional argument right after an option that may take it as its value (`-d a=1`, `-v a=1`) is
  never claimed, since the pass runs before the parser knows which curl options take values; stealing `-d`'s data
  would be worse than leaving a `name=value` after a flag as a URL.
- `Curl.Console/CurlCommandRunner.cs` needed no change: its expansion step already calls `ArgumentExpansion.Expand`.
- Added `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` to touches: the ADR criterion names it. No task in
  Doing names it.
- Quality: Measure-CodeQuality.ps1 on Curl.Extension.Abstractions.UnitLibrary gave 100% line and branch, worst
  CRAP 12, one member at complexity 12 (`ExpansionPass.ExpandAt`); fixed by moving its fallback chain into
  `TakeOrKeep`, which leaves both well under 10.
- Platform check: the tests use no paths, files or OS text; fast tests green on Windows.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b3a7311c 7bceb261 4ead31c2 73b4c612 ce42cc94 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. An ITrailingArgumentExpander takes its companion options and claimed positional arguments from its --next group (ADR-1051)
