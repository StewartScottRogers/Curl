# ADR-1051: An argument expander takes its companion options and claimed positional arguments

- Status: Accepted
- Date: 2026-10-29
- Decision: Decided by Claude under Stewart's delegation (BL-4217)

## Context

`--openapi <spec> --operation <id> name=value...` (BL-4107) needs more than its own option's value: ADR-1040's
`IArgumentExpander.ArgumentsFor(string value, ...)` would leave `--operation getPet` for the parser to refuse and
every `name=value` for curl to parse as a URL (`http://petId=5`).

## Decision

- `Curl.Extension.Abstractions.UnitLibrary/Arguments/ITrailingArgumentExpander` extends `IArgumentExpander` with
  `CompanionOptions` (the `ExtensionOption`s whose values it takes), `ClaimsPositionalArgument(string)` (a
  predicate over positional arguments) and `ArgumentsFor(ClaimedArguments, ArgumentExpansionContext)`.
  `ClaimedArguments` holds the option's value, the `CompanionOptionValue`s and the claimed positional arguments,
  each list in command-line order. Its default `IArgumentExpander.ArgumentsFor(string, ...)` passes the value with
  nothing claimed. A plain `IArgumentExpander` is unchanged, so BL-4205's expanders need no edit.
- `ArgumentExpansion.Expand` holds a trailing expander's option until its `--next` group ends (`--next`, `-:`, a
  bare `--` or the end of the arguments), takes each companion option given after it in that group (`--name value`,
  `--name=value`, or a flag) and each positional argument it claims, then puts its arguments in its option's place.
  Companions given before the expander's option, in another group or after a bare `--` are left for the parser,
  which refuses them as unknown options unless an extension registers them; positional arguments it does not claim
  stay URLs, as curl parses them. When two trailing expanders share a group, the later one that names a companion
  or claims a positional argument takes it.
- A positional argument is one that does not start with `-`, and is never claimed right after a kept argument that
  may take it as its value (any argument starting with `-` but `-` itself and a long option with `=`): the pass
  runs before the parser knows which curl options take values, so `--openapi s -d a=1` leaves `a=1` to `-d` rather
  than steal it. The cost is that a positional argument right after a curl flag (`-v a=1`) stays a URL; write it
  after the expander's option or its companions.
- The companion options are registered in `ExtensionOptionCatalogue` like any extension option, so `--extensions`
  lists them; under `CURL_EXTENSIONS=0` nothing is expanded or taken and the parser refuses both as unknown (exit 2).

## Consequences

- The `--openapi` extension (BL-4107) can implement `ITrailingArgumentExpander` with `--operation` as a companion
  and a predicate claiming `name=value` arguments.
- Curl.Console needed no change: its expansion step (`CurlCommandRunner.ExpandedArguments`) already calls
  `ArgumentExpansion.Expand`, which now does the taking.
