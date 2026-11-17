---
id: BL-5318
title: Blank --tlsuser, --tlspassword and their proxy forms in the Linux process list
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/NativeArgumentBlanking.cs, Curl.Console.UnitTests/ArgumentBlankingTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5318 — Blank --tlsuser, --tlspassword and their proxy forms in the Linux process list

## Goal

On Linux, the values of `--tlsuser`, `--tlspassword` and their proxy forms (`--proxy-tlsuser`, `--proxy-tlspassword`) are blanked in the process list the way upstream curl blanks them (`--tlsuser *` and `--tlspassword *******`), like the options the port already blanks.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-078 (Low, Confirmed: 2 runs). Area: CLI argument secrecy, Linux build. A credential readable by other local users; the port does not implement TLS-SRP and ignores the values, but they still sit in clear text in `/proc/<pid>/cmdline`.

**Repro (Linux).** Start a server on 127.0.0.1:35090 that accepts and never answers (PowerShell or a C# file-based app, never Python), then:
`curl -sS -m 3 --tlsuser u --tlspassword S18cret http://127.0.0.1:35090/ & sleep 1.2; tr '\0' ' ' </proc/$!/cmdline; echo`

**Expected (reference curl 8.18).** `curl -sS -m 3 --tlsuser * --tlspassword ******* http://127.0.0.1:35090/`

**Actual (port).** `... --tlsuser u --tlspassword S18cret http://127.0.0.1:35090/`. (`-u`, `-U`, `--proxy-user`, `--oauth2-bearer`, `--pass`, `--proxy-pass`, `-E`, `--proxy-cert` are now blanked correctly.)

**Where to start.** `Curl.Console/NativeArgumentBlanking.cs`: the `SecretLongNames` set (line 22) lists `["user", "proxy-user", "oauth2-bearer", "pass", "proxy-pass", "cert", "proxy-cert"]`. Add `tlsuser`, `tlspassword`, `proxy-tlsuser` and `proxy-tlspassword`, after checking upstream's `tool_getparam.c` (the version `Gap/Baselines/target.json` targets) for exactly which option values `curl` blanks (the `ARGTYPE`/secret flag, `-E` and `--cert`, and the proxy TLS-auth options), and confirm none of the four takes a value that must survive for the run (the arguments are copied out of argv first, BL-5093). Also update the class's summary comment that lists the blanked options.

## Acceptance criteria

- [ ] New tests in `Curl.Console.UnitTests/ArgumentBlankingTests.cs` (the existing file; add one test per option, platform-neutral, calling `NativeArgumentBlanking.SecretValues`) prove that the values of `--tlsuser`, `--tlspassword`, `--proxy-tlsuser` and `--proxy-tlspassword` are reported as secrets, both as a separate argument and attached (`--tlsuser=u`).
- [ ] A test proves a non-secret option after them (for example `--user-agent` or the URL) is not reported, and scanning still stops at `--`.
- [ ] The class summary comment lists the new options, and the option set matches upstream's (the Log states the curl version checked).
- [ ] The Log records the review's hand check on Linux, or says it could not be run: `/proc/$!/cmdline` shows `--tlsuser * --tlspassword *******`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell or a C# file-based app, never Python (CLAUDE.md). The blanking replaces each value with `*` characters of the same length as upstream does (`--tlsuser u` shows `*`, `--tlspassword S18cret` shows `*******`); reuse the existing replacement.

## Log

- 2026-11-16: Created.
