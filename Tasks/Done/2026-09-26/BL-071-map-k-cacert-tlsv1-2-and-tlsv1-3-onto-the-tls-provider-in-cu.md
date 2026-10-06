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
completed: 2026-09-26
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

- [x] A mapping from `CommandLineOptions` to `TlsClientOptions` in `Curl.Console` copies
      `Insecure`, `CaCertificateFile` and the minimum TLS version.
- [x] Named tests assert the mapped `TlsClientOptions` for: no TLS options; `-k`;
      `--cacert x.pem`; `--tlsv1.2`; `--tlsv1.3`; and `--tlsv1.3 --tlsv1.2`.
- [x] A named test asserts the `SslStreamTlsProvider` the composition builds receives the
      mapped options.
- [x] No test is tagged `Integration` and no test opens a socket.
- [x] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

- Plan: `TlsClientOptionsMapping.FromCommandLine(CommandLineOptions)` in `Curl.Console` copies
  `Insecure` and `CaCertificateFile` verbatim and maps `MinimumTlsVersion` (`SslProtocols.Tls12` ->
  `TlsMinimumVersion.Tls12`, `Tls13` -> `Tls13`, anything else including null -> `SystemDefault`).
  `CurlComposition.CreateTransports` now takes the parsed `CommandLineOptions` and builds the
  `SslStreamTlsProvider` from the mapped options.
- Choice: the mapping is its own static class rather than a private method on `CurlComposition`,
  so its six cases are tested directly and the composition test only checks the wiring.
- Choice: tests parse real command lines with `CommandLineParser.Parse(arguments, _ => true)` so
  `--cacert x.pem` needs no file on disk; `CommandLineOptions` setters are internal to Curl.Cli.
- Choice: the pipeline is `feature`, but the change is one small mapping in one project, so it
  was planned, tested and implemented in-session rather than through the separate agents.
- `CreateTransports` is still not called by `Program`/`CurlCommandRunner`: no network protocol
  handler is registered yet. Wiring it into the run belongs to the task that registers them.
- Tests: `TlsClientOptionsMappingTests` (6) and
  `CurlCompositionTests.CreateTransports_InsecureCaCertificateAndTlsv13_SslStreamTlsProviderReceivesMappedOptions`;
  Curl.Console.UnitTests 44/44 green.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -k, --cacert, --tlsv1.2 and --tlsv1.3 now set the TlsClientOptions the composition's SslStreamTlsProvider uses
