# Curl.Extension.Abstractions.UnitLibrary

The contract for extensions that need more than a URL scheme (ADR-0999). Namespace
`Curl.Extension.Abstractions`. Every extension may reference it; it references nothing of Curl's.

- `ExtensionOption` is one long command-line option an extension adds: its name without `--`, its
  `ExtensionOptionArgument` (`None` for a flag, `Value` for one value) and its one-line help. An
  extension declares it in one file and adds one line for it to `Curl.Extension.Composition`'s
  `ExtensionOptionCatalogue` (ADR-1006).
- `ExtensionOptionValues` is what one option group gave those options, which the extension reads at
  transfer time: `IsGiven` and `ValueOf` (the empty string for a flag, the last value given wins).

Later groundwork adds transfer, connection, resolver and certificate hooks in their own subfolders.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.Abstractions.UnitTests`.
