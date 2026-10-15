# ADR-0539 — The transfer setup, context and redirect mappings read `EngineTransferSettings`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2357.

## Context

`TransferPerformer.Setup.cs`, `TransferContextFactory.cs` and `RedirectPolicyMapping.cs` still took
`CommandLineOptions`. BL-2310 needs the engine free of `Curl.Cli`.

## Decision

- **Settings with a `CURLOPT_*` come from the easy groups already passed.** `--path-as-is`, `--interface` and
  `--connect-to` come from `EasyNetworkOptions`; `--disallow-username-in-url` from
  `EasyAuthenticationOptions`. `TryParseTransferUrl` takes the two booleans it needs.
- **Everything else is an engine-owned `EngineTransferSettings` record**, which `Curl.Console` fills with
  `EasyOptionsFromCommandLine.TransferSettingsOf`: `-s`, `-S`, `--fail`/`--fail-with-body`, `--create-file-mode`,
  `-k`, `--pass`, `--engine`, the malformed `--ech` verdict, the `-d` body, `-U`, `--proto`, `--proto-redir`, the
  `--ssl*` level, `-l`, `--upload-flags`, and the values `HttpRequestOptionsMapping.Of` and
  `MailRequestOptionsMapping.FromCommandLine` read from the command line (`--proxy-header`, `-p`,
  `--unix-socket`, `--happy-eyeballs-timeout-ms`). Those two mappings take the record in place of the command
  line because `TransferContextFactory.Create` calls them.
- **The `-G`/`--url-query` query is a delegate** (`AppendUrlQuery`) rather than a copy of `QueryUrl`, so the
  rules stay in one place until `Curl.Cli` is dropped.
- Behaviour is unchanged; the existing tests now build the record from the parsed command line.
