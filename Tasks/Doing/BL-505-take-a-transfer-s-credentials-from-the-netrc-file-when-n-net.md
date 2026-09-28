---
id: BL-505
title: Take a transfer's credentials from the netrc file when -n, --netrc-file or --netrc-optional asks
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-503, BL-504]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-505 — Take a transfer's credentials from the netrc file when -n, --netrc-file or --netrc-optional asks

## Goal

With `-n`, `--netrc-optional` or `--netrc-file`, `Curl.Console` looks up each URL's host in the netrc file (the default location when no file is named) and uses the login and password it finds, with curl 8.21.0's precedence against `-u` and URL user information, and curl's message and exit code when a required file is missing or broken.

## Context

- Conformance audit 2026-09-28, row 6 (Blocker). The reader is BL-503 (`Curl.Authentication.UnitLibrary`), the options BL-504.
- Credentials reach the transfer through `Curl.Console/TransferContextFactory.cs` and `HttpRequestOptionsMapping.cs` (`ITransferContext.Credentials`); every scheme that takes `-u` (HTTP, FTP, MQTT, TFTP) gets them the same way.
- Default location: `$HOME/.netrc`, and on Windows `%USERPROFILE%` with `_netrc` also tried; the exact order must be measured on each platform. Read the environment through the injected environment seam the proxy selection uses (ADR-0024), not `Environment` directly.
- Redirects to another host must not carry the first host's netrc password (compare FR-089).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `-n` with the file in the default location (both names on Windows), `-n` with no file, `--netrc-optional` with no file, `-u a:b -n`, a URL with `user@`, and an `ftp://` URL using `-Ftp`; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Console.UnitTests` tests through fake file and environment seams pin each measured case, including the missing-file message and exit code.
- [ ] New tests are platform-neutral; the Windows default-location order is pinned under `[OSCondition(OperatingSystems.Windows)]` and the other in its own excluded-Windows test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
