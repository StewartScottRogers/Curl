---
id: BL-192
title: Parse the HTTP auth options, -x, -U, --noproxy, -p and the --socks options
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-192 — Parse the HTTP auth options, -x, -U, --noproxy, -p and the --socks options

## Goal

`--basic`, `--digest`, `--anyauth`, `--oauth2-bearer`, `-x`/`--proxy`, `-U`/`--proxy-user`, `--noproxy`, `-p`/`--proxytunnel` and `--socks4`, `--socks4a`, `--socks5`, `--socks5-hostname` parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Proxy URL schemes (`http://`, `https://`, `socks4://`, `socks4a://`, `socks5://`, `socks5h://`, none) parse; an unknown scheme gives the measured curl 8.21.0 exit and message.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- From BL-216 (ADR-0022): `--oauth2-bearer` sets the wanted schemes from nothing, as curl's tool does, so with no other scheme option it must give `AuthSchemes = Bearer`, not `Basic | Bearer`; measured `-u u:p --oauth2-bearer tok` sends `Bearer tok`, and `--oauth2-bearer tok --anyauth` sends nothing until challenged.
- Touches widened to `Documentation/Planning/Decisions` for ADR-0026 (the decision below needed an ADR). No task in Doing named that folder. ADR-0023 to ADR-0025 are already taken in other lanes (BL-212, BL-206, BL-217), so this one is 0026.
- Decided (ADR-0026, Decided by Claude under Stewart's delegation): scheme options keep curl's bit set, so `--basic`/`--digest` add their scheme and `--no-basic`/`--no-digest` remove it. `--anyauth` sets every scheme, Bearer included, and `--oauth2-bearer` adds Bearer. `AuthSchemes` drops Bearer when there is no token and gives Basic when nothing is left. `-x` and the `--socks` options share one `CommandLineProxy` slot where the last one wins. An unsupported proxy scheme is not a parse refusal: `CommandLineProxy.TryGetKind` returns the exit-7 `TransferResult` for the transfer to report.
- Measured, curl 8.21.0 mingw, 2026-09-26, `curl <args> http://127.0.0.1:1/`: `--no-basic`, `--no-digest`, `--no-proxytunnel` (and `--no-ntlm`, `--no-negotiate`) accepted. `--no-anyauth`, `--no-oauth2-bearer`, `--no-proxy`, `--no-proxy-user`, `--no-noproxy`, `--no-socks4`, `--no-socks4a`, `--no-socks5` and `--no-socks5-hostname`, each bare and with `=x`, exit 2 with `curl: option <as typed>: the given option cannot be reversed with a --no- prefix` + try-help. `--basic=x`, `--digest=x`, `--anyauth=x` and `--proxytunnel=x` are accepted.
- Measured blanks: `--oauth2-bearer ''` and each `--socks*` with `''` exit 2 with `curl: option <opt>: blank argument where content is expected` + try-help. `-x ''`, `--proxy ''` and `--noproxy ''` are accepted. `-U ''` prompts `Enter proxy password for user '':`, `-U u` prompts for `'u'`, `-U 'p;o'` prompts for `'p'`, and `-U ';x'` and `-U u:p` do not prompt. `-U p` with no URL prompts before the no-URL refusal. `-U p -u h` prompts host first. `-u u --oauth2-bearer tok` never prompts. `-x -s` and `--noproxy -s` give no looks-like-a-flag warning.
- Measured schemes: `-x bogus://h:1`, `-x socks6://h:1`, `-x ftp://127.0.0.1:1` and `--socks5 bogus://h:1` exit 7 with `curl: (7) Unsupported proxy scheme for '<value>'`. `-x ://h:1` exits 5 (`Unsupported proxy syntax in '://h:1': Port number was not a decimal number between 0 and 65535`); that is URL syntax and belongs to BL-206's parser. `-x bogus://h` with no URL gives the no-URL refusal (exit 2). With `Record-CurlExchange.ps1`: `-x A --socks5 A` sends `05 02 00 01`, `--socks5 A -x A` sends `GET http://...`, `--socks4 A -x socks5://A` sends `05 ...`, `--socks5 http://A` sends `GET`, and `-x socks://A` and `--socks4 A` send `04 01 ...`.
- Measured auth (Record-CurlExchange.ps1, 401 offering Bearer and Basic): the table is in ADR-0026.
- Default choices: `--ntlm`/`--negotiate` were left out because they are not in this task's goal; filed as BL-270. The two scheme readers (this one and BL-206's `ProxyUrlParser`, which lacks `socks://` and the option's kind) are reconciled by BL-269. Both were filed with the board script directly.
- Results: `Curl.Cli.UnitTests` passes 1188 tests. The full fast run is green. `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` gives 100% line, 100% branch and 0 failing members out of 509.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. The HTTP auth options, -x, -U, --noproxy, -p and the --socks options parse into CommandLineOptions, with curl 8.21.0's refusals, prompts and proxy-scheme failure pinned
