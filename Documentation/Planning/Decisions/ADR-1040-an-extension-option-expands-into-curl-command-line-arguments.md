# ADR-1040: An extension option expands into curl command-line arguments

- Status: Accepted
- Date: 2026-10-29
- Decision: Decided by Claude under Stewart's delegation (BL-4205)

## Context

`--http-file` (BL-4154, the REST Client `.http` format) must supply whole requests: URLs, methods, headers, bodies
and several transfers. The extension hooks (ADR-1006 options, ADR-1007 transfer, ADR-1010 connection, ADR-1012
resolver, ADR-1016 certificate) act on a transfer that already exists, and a command line holding only
`--http-file f` stops at curl's "no URL specified" (exit 2) before any transfer exists.

## Decision

- `Curl.Extension.Abstractions.UnitLibrary/Arguments` holds the contract: `IArgumentExpander` names the
  `ExtensionOption` it expands and gives `IReadOnlyList<string> ArgumentsFor(string value, ArgumentExpansionContext context)`;
  `ArgumentExpansionContext` reads environment variables and files through delegates the host passes, so expanders
  are tested without a disk; `ArgumentExpansionException` carries the message and `CurlExitCode` the run ends with
  (26 for a file that cannot be read, as `-K` does; 2 for a value it cannot make sense of).
- `ArgumentExpansion.Expand`, in the same folder, replaces each expander's option and its value with the
  expander's arguments, reading the option as the parser reads a long option: `--name value` or `--name=value`, a
  flag taking no value. Nothing after a bare `--` is expanded (curl reads everything after it as a URL); an option
  with no value after it is left for the parser, which refuses it as curl refuses a missing parameter; an
  expansion's arguments are not expanded again, so no expander can recurse. Only the top-level arguments are
  expanded, not lines of a `-K` config file, and a token that is the value of a preceding curl option (`-d
  --http-file`) is still taken as the expander's option, since the pass runs before the parser knows which curl
  options take values; neither case is curl behaviour, as curl has no such option.
- `Curl.Extension.Composition.UnitLibrary/ExtensionArgumentExpanderCatalogue.cs` lists the expanders one per line,
  `merge=union` in `.gitattributes` and in `Tasks/SharedListFiles.txt`, as the other catalogues are. The expander's
  option also stays in `ExtensionOptionCatalogue`, so `--extensions` and `--ai-help extensions` list it.
- `Curl.Console`'s `CurlCommandRunner.RunAsync` expands the arguments before `CommandLineParser.Parse`, reading files
  through the runner's `IDataFileReader`. Under `CURL_EXTENSIONS=0` nothing is expanded, and the option, which the
  parser then does not know either, is refused as an unknown option with exit 2. An `ArgumentExpansionException`
  writes `curl: <message>` to standard error and ends the run with its exit code before parsing and before any
  transfer, with no try-help line: the failure is the file's or its contents', not the command line's.

## Consequences

An extension can supply complete requests with one option, and a command line without any expander's option
parses exactly as before (`Expand` gives the same arguments back). Expansion happens in the tool only: `Libcurl`
and `Curl.Api` take no command line, so they see none of it.
