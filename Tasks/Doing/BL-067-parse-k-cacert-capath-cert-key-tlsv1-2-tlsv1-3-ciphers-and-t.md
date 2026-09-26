---
id: BL-067
title: Parse -k, --cacert, --capath, --cert, --key, --tlsv1.2, --tlsv1.3, --ciphers and --tls13-ciphers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-067 — Parse -k, --cacert, --capath, --cert, --key, --tlsv1.2, --tlsv1.3, --ciphers and --tls13-ciphers

## Goal

`CommandLineParser` recognises `-k`/`--insecure`, `--cacert`, `--capath`, `-E`/`--cert`,
`--key`, `--tlsv1.2`, `--tlsv1.3`, `--ciphers` and `--tls13-ciphers`, records them on
`CommandLineOptions`, and refuses them with curl 8.21.0's exact lines where curl does.

## Context

- Add rows to `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` and properties to
  `CommandLineOptions.cs`, as `Curl.Cli.UnitLibrary/CLAUDE.md` requires; never
  special-case an option in `CommandLineParser`. Text options use
  `CommandLineOption.Text`, which refuses an empty value as blank.
- The values are recorded, not used: `Curl.Cli.UnitLibrary` does not reference
  `Curl.Networking.UnitLibrary`, and `Curl.Console` maps them onto `TlsClientOptions`
  (BL-071, BL-072). Record `--cert`, `--ciphers` and `--tls13-ciphers` verbatim; the
  `certificate:password` split is BL-065's.
- Upstream (<https://curl.se/docs/manpage.html>, as published for curl 8.23.0 on
  2026-09-26): `-k, --insecure`; `--cacert <file>`; `--capath <dir>`;
  `-E, --cert <certificate[:password]>`; `--key <key>`; `--tlsv1.2` "means version 1.2
  or later"; `--tlsv1.3` "means version 1.3 or later"; `--ciphers <list>`;
  `--tls13-ciphers <list>`.
- Measured 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32, Schannel,
  Release-Date 2026-06-24):
  - `--tlsv1.3 --tlsv1.2` behaves as `--tlsv1.2` and `--tlsv1.2 --tlsv1.3` as
    `--tlsv1.3`: the last one given wins.
  - `--capath ''`, `--cert ''` and `--ciphers ''` are each refused, exit 2, with
    `curl: option --<name>: blank argument where content is expected` and the try-help
    line: the existing `Text` behaviour.
  - `--cacert nonexist.pem` and `--cacert ''` are refused during parsing, exit 2, with
    three lines:
    `curl: The file 'nonexist.pem' provided to --cacert does not exist`,
    `curl: option --cacert: is badly used here`, and the try-help line. The file is
    checked for existence, so an empty value gives
    `curl: The file '' provided to --cacert does not exist`, not the blank refusal.
  - `--cacert .` (an existing directory) passes parsing; the failure comes later, at
    handshake (BL-063).
- `CommandLineRefusal` today builds only the two-line `curl: option <spelled>: <reason>`
  shape; the `--cacert` refusal needs a factory with a leading third line. BL-074 adds a
  second shape (`curl: (2) no URL specified`) in the same type; follow whichever pattern
  has landed.
- The existence check needs the file system, and the parser is a static, pure function
  today. Give it an injected file-existence seam (a parameter or an overload of
  `Parse` taking, for example, `Func<string, bool>`), with the production default using
  `System.IO.File.Exists`. If BL-080 has already added an injected file reader to the
  parser, extend that seam instead of adding a second one. Tests use a fake, never the
  real disk.
- Not measured: `--key ''` and `--tls13-ciphers ''`, and whether `-k` bundles with
  other short flags (`-sk`). Measure each with the local curl, record the results in
  `Notes`, and pin them in tests.

## Acceptance criteria

- [ ] `CommandLineOptions` has `Insecure` (`bool`), `CaCertificateFile`,
      `CaCertificateDirectory`, `ClientCertificate`, `PrivateKey`, `Ciphers`,
      `Tls13Ciphers` (each `string?`, `null` when not given) and a minimum TLS version
      (not given, 1.2, 1.3) where the last of `--tlsv1.2`/`--tlsv1.3` wins.
- [ ] A named test per option in `Curl.Cli.UnitTests` asserts the property it sets, for
      the long spelling and, for `-k` and `-E`, the short one.
- [ ] Named tests assert the last-wins rule for `--tlsv1.2`/`--tlsv1.3` in both orders.
- [ ] Named tests assert the exact two-line blank refusals, exit 2, for `--capath ''`,
      `--cert ''` and `--ciphers ''`, and the measured behaviour for `--key ''` and
      `--tls13-ciphers ''`.
- [ ] Named tests assert the exact three-line `--cacert` refusal, exit 2
      (`CurlExitCode.FailedInit`), for a file the injected seam reports missing and for
      `--cacert ''`; and acceptance when the seam reports it present.
- [ ] `Curl.Cli.UnitLibrary/README.md` lists the new options and properties.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

`--tls-max`, `--tlsv1.0`/`--tlsv1.1`, `--cert-type`, `--key-type` and `--pass` are not in
scope. For the record, curl 8.21.0 refuses `--tlsv1.2 --tls-max 1.1` with
`curl: --tls-max set lower than minimum accepted version` and
`curl: option --tls-max: is badly used here` (measured 2026-09-26), for whoever adds
`--tls-max`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
