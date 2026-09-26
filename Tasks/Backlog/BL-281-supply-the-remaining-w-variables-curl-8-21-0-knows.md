---
id: BL-281
title: Supply the remaining -w variables curl 8.21.0 knows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-225]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-281 — Supply the remaining -w variables curl 8.21.0 knows

## Goal

`TransferWriteOutVariables` supplies every `-w` variable curl 8.21.0 knows that no other task covers, so none of them prints the unknown-variable warning.

## Context

- Found by BL-225, which supplied the response, size, count, URL, method, scheme, IP, error message and exit code variables. `%{time_*}`/`%{speed_*}` are BL-226, `%{json}`/`%{header_json}` BL-227, `%{onerror}`/`%time{}` BL-279.
- Still reported unknown today: `%{referer}`, `%{filename_effective}`, `%{url.<part>}` and `%{urle.<part>}`, `%{certs}`, `%{num_certs}`, `%{ssl_verify_result}`, `%{proxy_ssl_verify_result}`, `%{proxy_used}`, `%{num_retries}`, `%{conn_id}`, `%{xfer_id}`, `%{time_queue}`, `%{tls_earlydata}`, `%{ftp_entry_path}`. ADR-0015 ("What the report does not carry") says each needs a later ADR for its source; the ones from the command line need a constructor input, not a `TransferReport` member.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009, ADR-0018) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Each listed variable renders as curl 8.21.0 does for a file:// and an http:// transfer (measured, pinned in `TransferWriteOutVariablesTests`), or is recorded in an ADR as deliberately left unknown with the reason.
- [ ] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

## Log

- 2026-09-26: Created.
