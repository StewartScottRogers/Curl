---
id: BL-618
title: Parse the ten TLS options and apply --engine, --dump-ca-embed and what SslStream carries
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-617]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-618 — Parse the ten TLS options and apply --engine, --dump-ca-embed and what SslStream carries

## Goal

`--curves`, `--sigalgs`, `--tls-earlydata`, `--ech`, `--ssl-sessions`, `--engine`, `--dump-ca-embed`, `--tlsuser`, `--tlspassword` and `--tlsauthtype` parse with curl 8.21.0's value checks into `CommandLineOptions` and reach `TlsClientOptions`; `--engine` and `--dump-ca-embed` produce the output BL-617's ADR gives; and whatever BL-617's ADR routes through `SslStream` is applied. The hand-built routes are BL-709 to BL-712.

## Context

- Conformance audit 2026-09-28, row 18 (Major). Decision and measurements: BL-617's ADR. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): no option is refused where some official curl build honours it; only malformed values are refused, with curl's text.
- Parse in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; value refusals built as the parser's other refusals are (`CommandLineRefusal.cs`); options flow through `Curl.Console/TlsClientOptionsMapping.cs` to `Curl.Networking.UnitLibrary/TlsClientOptions.cs`, where BL-708's routing function reads them.

## Acceptance criteria

- [x] `Curl.Cli.UnitTests` cover every option with valid and malformed values (malformed ones refused with the measured text and exit code).
- [x] Tests pin `--engine list` and `--dump-ca-embed` output as BL-617's ADR gives it, and each option reaching `TlsClientOptions`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Value checks read from curl 8.21.0's source (`src/tool_getparam.c`, `tool_parsecfg.c`, `tool_paramhlp.c`, `tool_operate.c` at tag `curl-8_21_0`), as curl behaves when built with every feature (ADR-0151: Curl accepts all ten everywhere):
  - `--curves`, `--sigalgs`, `--engine`, `--tlsuser`, `--ssl-sessions`: `DENY_BLANK`, so an empty value is refused as blank (exit 2). `--tlspassword`: `ALLOW_BLANK`. `--tlsauthtype`: blank refused, then anything but `SRP` (case-sensitive `strcmp`) refused with "the installed libcurl version does not support this" (exit 2), the text ADR-0151 measured on the OpenSSL build.
  - `--tls-earlydata` is `ARG_BOOL`, so negatable; the others are value options, so `--no-<name>` cannot be reversed, as is `--dump-ca-embed` (`ARG_NONE`).
  - `--ech`: `parse_ech`. `pn:` (value longer than 4) and `ecl:` (longer than 5) are matched case-insensitively (`curl_strnequal`) and kept apart from the mode keyword, which curl does not check while parsing. `ecl:@file` reads the file (`@-` standard input) through `file2string`, which drops CR and LF; an unreadable file prints `Warning: Could not read file "<f>" specified for "--ech ecl:" option` and returns `PARAM_BAD_USE` ("is badly used here").
  - `--ssl-sessions` is `global->ssl_sessions`, so it is global across `--next` groups; `ARG_FILE`, so it warns for a name that looks like a flag.
  - `--engine list` returns `PARAM_ENGINES_REQUESTED` and `--dump-ca-embed` `PARAM_CA_EMBED_REQUESTED`: parsing stops and the tool prints and exits 0; in a `-K` file both are ignored (`tool_parsecfg.c` skips them as it skips `-V`).
- Output, as ADR-0151 decides: `--engine list` prints `Build-time engines:` / `  <none>`; `--dump-ca-embed` prints nothing (`TlsBuildInformation`). `--engine <name>` is ignored on Windows; elsewhere every transfer fails before its upload file is opened or its URL parsed, exit 66 for `dynamic` and 53 otherwise, with the OpenSSL build's measured text (`NoCryptoEngines`). Choice: the failure is per transfer (curl fails at `setopt`); curl may stop the remaining URLs of the command line after the first, not measured — the per-transfer form was the simpler default.
- Routing (decided here, the default the backlog already implies): no row was added to `TlsClientRouting`. BL-709 to BL-712 each say "the routing row is added" when they apply their option; adding the rows now would send transfers to the hand-built client while it still ignores the option, changing today's bytes for nothing. The options reach `TlsClientOptions` (origin only; the proxy forms are BL-605/BL-712's) so those tasks only add the condition and the behaviour.
- `InformationLines` sat at complexity 10 and `TransferAsync` near it, so the two new cases went into `TlsBuildInformation.Lines` and a new `TransferOpeningUploadFileAsync` rather than raising either past 10.
- Measured: `Measure-CodeQuality.ps1` — Curl.Cli.UnitLibrary 100/100, 0 failing; Curl.Networking.UnitLibrary 100/100, 0 failing; Curl.Console 100/100, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The ten ADR-0151 TLS options parse with curl 8.21.0's value checks and reach TlsClientOptions; --engine list, --dump-ca-embed and --engine <name> behave as ADR-0151 gives them
