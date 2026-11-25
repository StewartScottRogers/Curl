# ADR-1402: The parser takes from its host whether it reads the options only Curl has

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation (BL-5971).

## Context

`CURL_EXTENSIONS=0` turns every extension off, and with it the options only Curl has, such as
`--ai-help` (BL-4937). The switch's name was written twice: as a public constant in
`Curl.Extension.Composition.UnitLibrary`, and as a private one in `Curl.Cli.UnitLibrary`, which
may not reference an extension project (ADR-0999). BL-5971 asks for one definition,
`ExtensionSwitch.Variable`.

## Decision

`CommandLineParser` no longer reads `CURL_EXTENSIONS`. Its overload that takes the extensions'
options also takes `knowsPortOnlyOptions` (default `true`), and the host passes `false` when its
extensions are off. `Curl.Console`'s `CurlCommandRunner` passes the same value it already hands
`CurlHelpText` and `CurlOptionManualSection`, which work the same way.

## Why

- The switch is defined once, and read where extensions are known: in the hosts and the
  composition library.
- The isolation rule of ADR-0999 stands: curl's own libraries reference no extension project.
- Moving the constant down into `Curl.Protocol.Abstractions` instead would put an extension
  concern in a contract of curl's own.

## Consequences

`curl`'s behaviour is unchanged. A caller of `CommandLineParser` that wants the options only Curl
has refused must say so; setting the environment variable in the reader it passes is not enough.
`Curl.Console` is the only caller outside tests.
