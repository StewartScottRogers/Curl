---
id: BL-624
title: Parse --expect100-timeout and wait that long for 100 Continue
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-624 — Parse --expect100-timeout and wait that long for 100 Continue

## Goal

`--expect100-timeout <seconds>` (decimal allowed) replaces the one-second wait for `100 Continue` before a request body is sent anyway, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 21 (Major, S).
- The current one-second wait is in `Curl.Protocol.Http.UnitLibrary` (ADR-0036 frames request bodies; `Record-CurlExchange.ps1 -RespondAfterBodyBytes` documents curl's one-second wait). Parse with the other decimal-seconds options (`--connect-timeout`) in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; the value reaches the handler through `HttpRequestOptions` via `Curl.Console/HttpRequestOptionsMapping.cs`.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -ResponseDelayMilliseconds`: a large `-d @file` (curl sends `Expect: 100-continue` above its threshold) with `--expect100-timeout 0.2` and `3`, timing when the body arrives; and `--expect100-timeout abc`; request bytes, timings and exit codes copied into Notes.
- [x] `Curl.Protocol.Http.UnitTests` on a fake `TimeProvider` pin when the body is sent for each timeout; `Curl.Cli.UnitTests` pin parsing and the refusal.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-29 against the local curl 8.21.0 (mingw, Schannel) with
`Record-CurlExchange.ps1 -RespondAfterBodyBytes 2000000` (the server answers once the body
has arrived, which times the body better than `-ResponseDelayMilliseconds`), sending a
2,000,000-byte file with `-sS -v --trace-time --data-binary @big.bin --expect100-timeout <t>`.
The request head was the usual one ending `Content-Length: 2000000`,
`Content-Type: application/x-www-form-urlencoded`, `Expect: 100-continue`.

| `<t>` | `> POST` | `Done waiting for 100-continue` | wait | exit |
| --- | --- | --- | ---: | ---: |
| `0.2` | 46.687 | 46.887 | 200 ms | 0 |
| `3` | 47.241 | 50.247 | 3.006 s | 0 |
| `0` | 50.431 | 51.436 | 1.005 s (the default) | 0 |
| `0.0001` | 51.607 | 52.608 | 1.001 s (0 ms, so the default) | 0 |
| `abc`, `-1` | - | - | - | 2, `curl: option --expect100-timeout: expected a proper numerical parameter` |

With `--libcurl` the value is read exactly as `--connect-timeout` is: `2147482` and
`2147482.999` accepted, `2147483` refused (exit 2, same message) on Windows; `1,5` accepted.

What changed:
- `Curl.Cli.UnitLibrary`: `--expect100-timeout` parsed with `CommandLineNumber.ParseSeconds`
  into `CommandLineOptions.Expect100Timeout`; per-group at `--next`, as `--connect-timeout` is.
  `--ai-help` picks it up from the option table (no longer "Not supported by this build yet").
- `Curl.Protocol.Abstractions.UnitLibrary`: `HttpRequestOptions.ContinueWait`, default
  `HttpRequestOptions.DefaultContinueWait` (1 s). Added to `touches`: the value needs a slot
  on the options the handler reads, and no task in Doing touches this project.
- `Curl.Console`: `HttpRequestOptionsMapping.ContinueWaitOf` maps none or 0 to the 1 s default.
- `Curl.Protocol.Http.UnitLibrary`: `HttpContinueWaitConnection.ContinueWait` is now set per
  request from the options.

Choice (rule 1): a wait longer than a .NET timer accepts (about 49.7 days, possible from
2147482 s) is waited out without a timer instead of throwing; it outlasts any transfer, so
the behaviour matches curl. No ADR: the one behaviour question (0 means the default) was
measured, not decided.

End-to-end run of our own binary through the recorder was refused by the session sandbox
(`bin/` path); the handler test drives the same path on a fake clock.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --expect100-timeout sets the wait for 100 Continue (0 or none: curl's 1 s), parsed as --connect-timeout is
