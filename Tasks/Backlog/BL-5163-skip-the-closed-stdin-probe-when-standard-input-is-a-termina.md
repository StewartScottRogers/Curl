---
id: BL-5163
title: Skip the closed-stdin probe when standard input is a terminal off Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary/ClosedStandardInputStream.cs, Curl.Core.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-5163 — Skip the closed-stdin probe when standard input is a terminal off Windows

## Goal

Starting Curl on Linux or macOS with standard input on a terminal writes nothing to the terminal and does not block, the same as upstream curl. A closed standard input is still detected.

## Context

Found by the shift-end review of 2026-11-13 (ADR-0710), code-reviewer. Introduced with BL-5137 (commit 1c11b0d78).

`Curl.Core.UnitLibrary/ClosedStandardInputStream.cs:37-48`: `ReadsWithBadDescriptor` does `standardInput.Read(Span<byte>.Empty)` on the stream from `Console.OpenStandardInput()`, on every run (`Curl.Console/Program.cs:19`). Off Windows, when standard input is a terminal, .NET sends that stream's reads through its console line reader (`useReadLine`). That reader runs .NET's terminfo setup, which writes `keypad_xmit` (`ESC [ ? 1 h ESC =`) and never resets it. BL-5109 removed exactly this from the password prompt (`Curl.Cli.UnitLibrary/ProcessStandardError.cs`). The line reader may also wait for a line on an empty read. CI never runs with a terminal on stdin, so CI cannot catch this.

Start by checking the effect on Linux: `script -q -c 'curl file:///etc/hostname' /dev/null | od -c`. A closed descriptor always reports as redirected, so skipping the probe when `!Console.IsInputRedirected` keeps the BL-5137 fix. The other option is to probe descriptor 0 directly through a `SafeFileHandle` rather than the console stream.

## Acceptance criteria

- [ ] When standard input is a terminal, `InPlaceOfNull` does not read from the console stream.
- [ ] A closed standard input (EBADF) is still replaced with `ClosedStandardInputStream`, so `-d @-` with stdin closed still exits 26.
- [ ] A unit test in `Curl.Core.UnitTests` pins that a terminal standard input is not probed, using an injected "is redirected" seam or something similar.
- [ ] `dotnet build` is clean, the fast tests pass, and `Curl.Core.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-13: Created.
