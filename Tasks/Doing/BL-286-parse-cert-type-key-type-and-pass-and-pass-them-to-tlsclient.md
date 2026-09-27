---
id: BL-286
title: Parse --cert-type, --key-type and --pass and pass them to TlsClientOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-249]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-286 — Parse --cert-type, --key-type and --pass and pass them to TlsClientOptions

## Goal

`curl --cert-type`, `--key-type` and `--pass` are parsed and reach `TlsClientOptions.CertificateType`,
`PrivateKeyType` and `Passphrase`, so the command line gets the loading BL-249 built.

## Context

- BL-249 added `TlsClientOptions.CertificateType`, `PrivateKeyType` and `Passphrase` in
  `Curl.Networking.UnitLibrary` and honours them per ADR-0009 section 3; nothing sets them yet.
- `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` has `--cert` and `--key` (lines ~102-103) but
  not `--cert-type`, `--key-type` or `--pass`.
- `Curl.Console/TlsClientOptionsMapping.cs` maps `CommandLineOptions` to `TlsClientOptions`.
- Values pass through as given (curl compares type names case-insensitively in the TLS layer).

## Acceptance criteria

- [ ] `CommandLineOptions` has `ClientCertificateType`, `PrivateKeyType` and `Passphrase`, set by
      `--cert-type`, `--key-type` and `--pass`; named tests pin each, last one wins.
- [ ] `TlsClientOptionsMapping` copies all three; a named test asserts each.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
