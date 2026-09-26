---
id: BL-072
title: Map --capath, --cert/--key and --ciphers/--tls13-ciphers onto the TLS provider in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-071, BL-064, BL-065, BL-066]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-072 — Map --capath, --cert/--key and --ciphers/--tls13-ciphers onto the TLS provider in Curl.Console

## Goal

`Curl.Console` maps `--capath`, `--cert`/`--key`, `--ciphers` and `--tls13-ciphers` from
the parsed command line onto `TlsClientOptions`, and prints any warning lines the
provider reports for them on standard error, as the TLS decisions require.

## Context

- BL-067 parses the options into `CommandLineOptions` (`CaCertificateDirectory`,
  `ClientCertificate`, `PrivateKey`, `Ciphers`, `Tls13Ciphers`).
- BL-064 (`--capath`, following the ADR from BL-059), BL-065 (`--cert`/`--key`) and
  BL-066 (`--ciphers`/`--tls13-ciphers`, following the ADR from BL-060) add the matching
  `TlsClientOptions` members in `Curl.Networking.UnitLibrary`. Where those ADRs decide
  that an option is ignored with a warning (for example the curl 8.21.0 Schannel build's
  two lines for `--capath`, `Warning: ignoring setting the CA path for the proxy, not supported by libcurl `
  and `Warning: with Schannel`, measured 2026-09-26), the Networking task exposes the
  lines and this task prints them. Read both ADRs for when the warning appears (at
  start-up or per transfer) and follow them.
- BL-071 adds the mapping from `CommandLineOptions` to `TlsClientOptions`; extend it.

## Acceptance criteria

- [ ] The mapping copies `CaCertificateDirectory`, `ClientCertificate`, `PrivateKey`,
      `Ciphers` and `Tls13Ciphers`; a named test per option asserts the mapped value.
- [ ] For every warning the ADRs from BL-059 and BL-060 require, a named test asserts
      the exact lines on standard error, each followed by `Environment.NewLine`, and
      when they appear relative to the transfer.
- [ ] No test is tagged `Integration` and no test opens a socket.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
