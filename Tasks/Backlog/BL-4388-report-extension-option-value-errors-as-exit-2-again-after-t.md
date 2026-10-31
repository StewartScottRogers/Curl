---
id: BL-4388
title: Report extension option value errors as exit 2 again after the narrowed catches in CurlComposition
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Console.UnitTests]
requirement: none
created: 2026-10-31
completed:
---
# BL-4388 — Report extension option value errors as exit 2 again after the narrowed catches in CurlComposition

## Goal

A bad value for an extension option (--ssh-jump nohost, --masque, --wireguard, --npipe, --vm-socket, --odoh-*) is again reported as a clean exit-2 command-line error, without "(Parameter 'values')" and with the try-help line.

## Context

Shift-end review 2026-10-31 (silent-failure-hunter). Commit 00c9022dc narrowed the catches at Curl.Console/CurlComposition.cs:754, 798 and 828 from ArgumentException to ExtensionHookConflictException, but the activators still throw plain ArgumentException for bad values (for example Curl.Extension.SshJump.UnitLibrary/SshJumpExtension.cs:43, MasqueExtension.cs:45, WireGuardExtension.cs:51/63/72). Those now bypass CurlCommandRunner's ExtensionOptionConflictException handler: curl.exe falls into UnhandledExceptionGuard and prints `curl: (2) --ssh-jump needs user@host[:port], not 'nohost' (Parameter 'values')` with no try-help line, and InProcessCurl lets the raw ArgumentException reach the caller. The extensions' CLAUDE.md promises "an ArgumentException the host reports as exit 2".

## Acceptance criteria

- [ ] `curl --ssh-jump nohost https://x` exits 2 printing `curl: --ssh-jump needs user@host[:port], not 'nohost'` and the `curl: try 'curl --help'` line, proven by a test in Curl.Console.UnitTests.
- [ ] Through InProcessCurl the same command returns exit 2 and throws nothing, proven by a test.
- [ ] dotnet build is clean, the fast tests pass and Curl.Console keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-31: Created.
