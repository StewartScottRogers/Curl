# Curl.Extension.Har.UnitLibrary

The `--har <file>` extension (ADR-0999, ADR-1127), tested against HAR 1.2 (W3C draft). Namespace
`Curl.Extension.Har`. It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared layers it brings.

- `HarExtension` gives the `--har` option, its description and its hook activator, which
  `Curl.Extension.Composition` registers in its option, description and transfer hook catalogues.
- `HarTransferHook` copies each HTTP transfer's response body through `BodyCopyingStream`, records a `HarExchange`
  when the transfer ends and writes the whole archive to the file again; a file it cannot write fails the transfer
  with exit 23.
- `HarDocument` writes the archive (`log`, `creator`, `entries`, `request`, `response`, `content`) with
  `Utf8JsonWriter`; `HarTimings` reads an entry's `timings` from the transfer's `TransferTimings`.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.Har.UnitTests`.
