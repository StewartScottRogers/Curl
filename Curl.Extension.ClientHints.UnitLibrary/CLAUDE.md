# Curl.Extension.ClientHints.UnitLibrary

The User-Agent Client Hints extension (ADR-0999, ADR-1102), tested against RFC 8942 and the WICG User-Agent
Client Hints spec. Namespace `Curl.Extension.ClientHints`. It references only `Curl.Extension.Abstractions.UnitLibrary`
and the shared layers it brings.

- `ClientHintsExtension` gives the `--client-hints` flag, its description and its hook activator, which
  `Curl.Extension.Composition` registers.
- `ClientHintsTransferHook` adds the low-entropy hints `Sec-CH-UA`, `Sec-CH-UA-Mobile` and `Sec-CH-UA-Platform` to
  every HTTP request to an `https`, `wss` or loopback URL, leaving any `-H` gave.
- `ClientHintsPlatform` names the platform for `Sec-CH-UA-Platform`.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.ClientHints.UnitTests`.
