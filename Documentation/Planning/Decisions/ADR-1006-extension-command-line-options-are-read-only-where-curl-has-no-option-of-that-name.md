# ADR-1006: Extension command-line options are read only where curl has no option of that name

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4092)

## Context

Most extensions Stewart chose (`--sse`, `--cache-dir`, `--npipe` ...) are long command-line options. ADR-0999
says an extension never changes curl's behaviour, and `CURL_EXTENSIONS=0` must leave exactly upstream curl.
`Curl.Cli` may not reference an extension project (`VerifyExtensionIsolation`), so it cannot see the
extensions' own option type.

## Decision

- `Curl.Extension.Abstractions.UnitLibrary` holds `ExtensionOption` (long name, `ExtensionOptionArgument`
  `None` or `Value`, one-line help) and `ExtensionOptionValues`, which an extension reads at transfer time.
- An extension registers its option with one line in `Curl.Extension.Composition`'s `ExtensionOptionCatalogue`
  (`merge=union`, a shared list file). `ExtensionCommandLineOptions.For(readEnvironmentVariable, isCurlLongOption)`
  gives them, or none when `CURL_EXTENSIONS` is `0`, and fails composition with `InvalidOperationException`,
  naming both, when one has the name of a curl option (`CommandLineParser.IsCurlLongOption`) or of another
  extension option.
- `Curl.Cli` has its own `CommandLineExtensionOption` (long name, takes a value or not); the host maps each
  `ExtensionOption` to one and passes them to the `CommandLineParser.Parse` overload that takes them. The parser
  looks one up only after `CommandLineOptionTable` has no row of that name, so curl's options always win, and
  records the value (the empty string for a flag, the last value given wins) in
  `CommandLineOptions.ExtensionOptionValues` for that option group.
- With extensions off the host passes no option, so the name is refused by the same path as any unknown
  option: exit 2 and curl's `option --x: is unknown` bytes, with no special case.
- An extension option takes no `--no-` or `--expand-` spelling and is not read from a `-K` file yet; each can be
  added later without changing this contract.

## Consequences

An extension option can never shadow a curl option, and turning extensions off cannot make any curl command
line behave differently.
