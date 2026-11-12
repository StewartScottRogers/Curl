---
id: BL-5027
title: Refuse -J with -C - in one option group with exit 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitTests/CommandLineRefusalRemoteHeaderNameTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5027 — Refuse -J with -C - in one option group with exit 2

## Goal

`-J` (`--remote-header-name`) together with `-C -` (`--continue-at -`) in one option group is refused before any request, with REF's two stderr lines and exit 2.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-031 (Medium, Confirmed).

- **Repro:** against a server that answers every GET with `Content-Disposition: attachment; filename="fromcd.txt"` and body `cd body\n`, in an empty directory:
  - `curl -s -OJ -C - http://127.0.0.1:41000/cd; echo rc=$?; ls`
  - `curl -sS -J -C - -o o.txt http://127.0.0.1:41000/cd; echo rc=$?`
  - a config file `remote-header-name`, `remote-name`, `continue-at = -` run as `curl -sS -K cfg http://127.0.0.1:41000/cd`
- **Expected (REF):** stderr is `curl: --continue-at and --remote-header-name cannot be combined`, then `curl: try 'curl --help' or 'curl --manual' for more information`. These lines print even with `-s`. Exit 2. No request is made and no file is written. The check applies within one option group only: `-OJ URL --next -C - -o y URL` is accepted.
- **Actual (PORT):** all three make the request and exit 0, writing `fromcd.txt`, `o.txt` and `fromcd.txt`. If a 5-byte file named `cd` already exists, PORT sends `Range: bytes=5-` and fails with `curl: (23) client returned ERROR on write of 56 bytes`.
- **Where to start:** `Curl.Cli.UnitLibrary/CommandLineRefusal.cs`, next to `ContinueAtExclusiveWith` and `ContinueAtExclusiveWithNoClobber`. Add a per-group rule: `-C -` together with `-J` exits 2 with REF's two lines. Check whether a numeric `-C 10` is refused too in REF before deciding; the review only covers `-C -`.

## Acceptance criteria

- [ ] A new `Curl.Cli.UnitTests/CommandLineRefusalRemoteHeaderNameTests.cs` asserts that `-OJ -C - URL`, `-J -C - -o o.txt URL` and the config-file form each refuse with `CurlExitCode` 2 and the message `curl: --continue-at and --remote-header-name cannot be combined` followed by `curl: try 'curl --help' or 'curl --manual' for more information`.
- [ ] The refusal text is written even under `-s`.
- [ ] Regression: `-OJ URL --next -C - -o y URL` is accepted (the rule is per option group), and `-J` alone and `-C -` alone are accepted.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text.

## Log

- 2026-11-12: Created.
