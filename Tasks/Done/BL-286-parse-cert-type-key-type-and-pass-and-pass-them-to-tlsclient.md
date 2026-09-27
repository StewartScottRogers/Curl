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
completed: 2026-09-27
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

- [x] `CommandLineOptions` has `ClientCertificateType`, `PrivateKeyType` and `Passphrase`, set by
      `--cert-type`, `--key-type` and `--pass`; named tests pin each, last one wins.
- [x] `TlsClientOptionsMapping` copies all three; a named test asserts each.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- Delivered directly (one table row and one property per option, plus the mapping); the change was too small to need a protocol-architect plan.
- Measured the local curl 8.21.0 (Schannel) on 2026-09-27: `--cert-type ""`, `--key-type ""` and `--pass ""` each print
  `curl: option <opt>: blank argument where content is expected` and exit 2, and `--pass -x` takes `-x` without a
  filename warning, so all three are `CommandLineOption.Text` rows, not `FileName` rows. Pinned by
  `Parse_EmptyTextValue_RefusesAsBlank` and `Parse_TypeOrPassGivenFlagLikeValue_AcceptsWithoutWarning`.
- Tests: `Parse_CertType_RecordsClientCertificateTypeVerbatim`, `Parse_CertTypeTwice_TheLastWins`, `Parse_KeyType_*`,
  `Parse_Pass_*` (CommandLineTlsOptionTests); `FromCommandLine_CertType_*`, `FromCommandLine_KeyType_*`,
  `FromCommandLine_Pass_*` (TlsClientOptionsMappingTests). Fast tests: Curl.Cli 1684 passed, Curl.Console 592 passed, all green.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --cert-type, --key-type and --pass are parsed and reach TlsClientOptions.CertificateType, PrivateKeyType and Passphrase
