---
id: BL-071
title: Map -k, --cacert, --tlsv1.2 and --tlsv1.3 onto the TLS provider in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-069, BL-063, BL-067]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-071 — Map -k, --cacert, --tlsv1.2 and --tlsv1.3 onto the TLS provider in Curl.Console

## Goal

`Curl.Console` builds the `SslStreamTlsProvider`'s `TlsClientOptions` from the parsed
command line, so `-k`, `--cacert`, `--tlsv1.2` and `--tlsv1.3` given to `curl` change
how a `gophers://` or `mqtts://` transfer verifies and negotiates TLS.

## Context

- BL-067 adds `Insecure`, `CaCertificateFile` and the minimum TLS version (last of
  `--tlsv1.2`/`--tlsv1.3` wins) to `CommandLineOptions` in `Curl.Cli.UnitLibrary`, and
  refuses a `--cacert` file that does not exist during parsing.
- BL-062 and BL-063 add `TlsClientOptions.Insecure`, the minimum version and
  `CaCertificateFile` in `Curl.Networking.UnitLibrary`.
- BL-069 builds the provider with `new TlsClientOptions()`. Replace that with options
  mapped from `CommandLineOptions`. The composition is built after parsing, once per
  run, so every URL on the command line shares one set of TLS options, as curl does
  without `--next` (which the parser does not support).
- `Curl.Console` is the only project referencing both `Curl.Cli.UnitLibrary` and
  `Curl.Networking.UnitLibrary`, which is why the mapping lives here.

## Acceptance criteria

- [ ] A mapping from `CommandLineOptions` to `TlsClientOptions` in `Curl.Console` copies
      `Insecure`, `CaCertificateFile` and the minimum TLS version.
- [ ] Named tests assert the mapped `TlsClientOptions` for: no TLS options; `-k`;
      `--cacert x.pem`; `--tlsv1.2`; `--tlsv1.3`; and `--tlsv1.3 --tlsv1.2`.
- [ ] A named test asserts the `SslStreamTlsProvider` the composition builds receives the
      mapped options.
- [ ] No test is tagged `Integration` and no test opens a socket.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
