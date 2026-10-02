---
id: BL-1172
title: Refuse an unknown --ech mode with exit 43 setopt got bad argument as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1172 — Refuse an unknown --ech mode with exit 43 setopt got bad argument as curl does

## Goal

`curl --ech bogus <url>` exits 43 with `curl: (43) setopt 0x2855 got bad argument`, as curl 8.21.0's ECH build does. That build was measured on 2026-10-02 in BL-1107, with and without `ecl:`.

## Context

- ADR-0359 (BL-1107). libcurl's `setopt_ech` returns `CURLE_BAD_FUNCTION_ARGUMENT` for a value other than `false`, `grease`, `true`, `hard`, `ecl:...` or `pn:...`.
- First check what the platform builds without ECH do with `--ech bogus`: the mingw Schannel 8.21.0 build and WSL's OpenSSL 8.18.0 build. Curl matches the platform's curl. If they differ from the ECH build, pin each platform's answer and record which one Curl follows in an ADR.
- Keep `--ai-help` right.

## Acceptance criteria

- [ ] `Curl.Cli.UnitTests` pin the measured exit code and message for an unknown `--ech` mode.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
