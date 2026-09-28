---
id: BL-535
title: Parse --mail-from, --mail-rcpt, --mail-auth, --mail-rcpt-allowfails, --upload-flags, --sasl-authzid, --sasl-ir and --login-options
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-533]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-535 — Parse --mail-from, --mail-rcpt, --mail-auth, --mail-rcpt-allowfails, --upload-flags, --sasl-authzid, --sasl-ir and --login-options

## Goal

The eight mail and SASL options parse into `CommandLineOptions` as curl 8.21.0 reads them (`--mail-rcpt` repeatable and kept in order, `--sasl-ir` and `--mail-rcpt-allowfails` with their `--no-` forms, `--upload-flags` with curl's flag syntax), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, rows 31 and 23. The ADR from BL-533 names where each value ends up.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; texts in `CurlManual.txt`. `--upload-flags` takes a comma list of IMAP flags (`answered`, `deleted`, `draft`, `flagged`, `seen`, with `-` to unset); its refusal for an unknown flag must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--upload-flags bogus`, `--upload-flags seen,-draft`, `--mail-rcpt ""`, `--login-options ""` with a loopback URL; stderr and exit code copied into Notes.
- [x] Every option and `--no-` form is covered by `Curl.Cli.UnitTests` data rows, including repeated `--mail-rcpt` order and the measured refusals byte for byte.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-09-28 with curl 8.21.0 (Schannel) and `Record-CurlExchange.ps1`, `-s -S http://127.0.0.1:18535/`:
  - `--upload-flags bogus`: exit 2, `curl: option --upload-flags: is unknown` + `curl: try 'curl --help' or 'curl --manual' for more information`. The same for `""`, `Seen` (case-sensitive), `seen,,draft`, `seen,`, `-`, `-bogus`, `seen bogus`.
  - `--upload-flags seen,-draft`: exit 0, empty stderr (also `-seen,answered`).
  - `--mail-rcpt ""`: exit 0, empty stderr. `--login-options ""`: exit 0, empty stderr.
  - `--mail-from ""`, `--mail-auth ""`, `--sasl-authzid ""`: exit 2, `curl: option <opt>: blank argument where content is expected` + try-help.
  - `--no-mail-from`, `--no-mail-rcpt`, `--no-mail-auth`, `--no-upload-flags`, `--no-login-options`, `--no-sasl-authzid`: exit 2, `... the given option cannot be reversed with a --no- prefix`. `--no-sasl-ir` and `--no-mail-rcpt-allowfails`: exit 0.
  - Each value option given last: exit 2, `requires parameter`.
- Upload flags' effect, measured with `Record-CurlExchange.ps1 -Imap -u u:p -T msg imap://.../INBOX`: no option gives `APPEND INBOX (\Seen) {12}`; `-seen` gives `APPEND INBOX {12}`; `answered,deleted,draft,flagged` gives `(\Answered \Deleted \Draft \Flagged \Seen)`; `--upload-flags draft --upload-flags flagged` gives `(\Draft \Flagged \Seen)` (repeats accumulate); `flagged,-flagged` gives `(\Seen)`. The flags always print in that fixed order.
- Decision (sensible default): the parser resolves `--upload-flags` into `CommandLineOptions.UploadFlags`, an `ImapUploadFlags` bit set starting at `Seen`, rather than keeping the raw text, because the default, the `-` clearing and the accumulation across repeats are the command line's semantics, not IMAP's. BL-539 maps the set to `MailRequestOptions.UploadFlags` as the flag names in curl's fixed order (answered, deleted, draft, flagged, seen), so the IMAP handler (BL-557) only prints them.
- All eight options are per-group (curl's `OperationConfig`), so they reset after `--next`; added to `CommandLineNextGroupTests`' per-group list.
- The flag-name `switch` measured cyclomatic complexity 20; replaced with a `FrozenDictionary` lookup (10 worst CRAP in the library afterwards).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The eight mail and SASL options parse into CommandLineOptions as curl 8.21.0 reads them, measured refusals included
