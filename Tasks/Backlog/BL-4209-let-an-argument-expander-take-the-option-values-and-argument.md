---
id: BL-4209
title: Let an argument expander take the option values and arguments that follow its option
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4205]
touches: [Curl.Extension.Abstractions.UnitLibrary/Arguments, Curl.Extension.Abstractions.UnitTests/Arguments, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerTrailingArgumentExpansionTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4209 — Let an argument expander take the option values and arguments that follow its option

## Goal

An argument expander (BL-4205) can also take the values of companion extension options and the positional arguments of its form that follow its option on the command line (such as `--openapi spec.json --operation getPet petId=5 status=sold`, where `--operation getPet` and the two `name=value` arguments go to the `--openapi` expander rather than being parsed as an option and two URLs), and a command line without an expander's option parses exactly as before.

## Context

Found by BL-4107 (`--openapi <spec> --operation <id> name=value...`) in its prerequisite pass. BL-4205's suggested contract hands an expander only its own option's value (`ArgumentsFor(string value, ArgumentExpansionContext context)`), so `--operation <id>` would be refused or kept as an unrelated option and every `name=value` would be parsed by curl as a URL (`http://petId=5`). Suggested shape (decide in an ADR): the expander declares the companion `ExtensionOption`s it reads (`--operation`) and a predicate for the positional arguments it consumes (here, an argument holding `=` that does not start with `-`, taken only after the expander's option); Curl.Console's expansion step in `CurlCommandRunner.cs` (where BL-4205 put it) removes those from the argument list within the same `--next` group and passes them to the expander beside its value. A positional argument that does not match stays a URL, as curl parses it.

## Acceptance criteria

- [ ] The expander contract in `Curl.Extension.Abstractions.UnitLibrary/Arguments` lets an expander name companion options and claim matching positional arguments, and receives their values in command-line order; unit tests.
- [ ] Curl.Console gives a test expander for `--x-spec` with companion `--x-op` the line `--x-spec s --x-op o a=1 b=2 http://h/` as value `s`, companion `o` and positional `a=1`, `b=2`, and parses `http://h/` and the expander's arguments as before; unit test in `Curl.Console.UnitTests/CurlCommandRunnerTrailingArgumentExpansionTests.cs`.
- [ ] Positional arguments in another `--next` group, or on a command line without the expander's option, stay URLs; unit tests.
- [ ] With `CURL_EXTENSIONS=0` the expander's option and its companion are refused as unknown options (exit 2); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the shape, and `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` describes it.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
