---
id: BL-192
title: Parse the HTTP auth options, -x, -U, --noproxy, -p and the --socks options
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-192 — Parse the HTTP auth options, -x, -U, --noproxy, -p and the --socks options

## Goal

`--basic`, `--digest`, `--anyauth`, `--oauth2-bearer`, `-x`/`--proxy`, `-U`/`--proxy-user`, `--noproxy`, `-p`/`--proxytunnel` and `--socks4`, `--socks4a`, `--socks5`, `--socks5-hostname` parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Proxy URL schemes (`http://`, `https://`, `socks4://`, `socks4a://`, `socks5://`, `socks5h://`, none) parse; an unknown scheme gives the measured curl 8.21.0 exit and message.
- [ ] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- From BL-216 (ADR-0022): `--oauth2-bearer` sets the wanted schemes from nothing, as curl's tool does, so with no other scheme option it must give `AuthSchemes = Bearer`, not `Basic | Bearer`; measured `-u u:p --oauth2-bearer tok` sends `Bearer tok`, and `--oauth2-bearer tok --anyauth` sends nothing until challenged.

## Log

- 2026-09-26: Created.
