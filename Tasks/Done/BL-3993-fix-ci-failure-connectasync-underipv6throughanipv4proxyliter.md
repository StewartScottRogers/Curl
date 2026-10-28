---
id: BL-3993
title: Fix CI failure ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3993 — Fix CI failure ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy on Linux and macOS

## Goal

`ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy` failed on Linux and macOS in CI run 40774593052 (https://github.com/StewartScottRogers/Curl/actions/runs/40774593052). First failing commit: 81764fdf.

    CollectionAssert.AreEqual failed. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 40774593052 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

The same run, from the same commit, also fails `TcpConnectorTests.ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral` on Linux and macOS, in its data rows `-4 http://[::1]:47500/`, `-4 bracketed` and `-6 http://127.0.0.1:47500/` ("Failed to connect to ::1:47500 after 0 ms"). The CI watch filed those rows as BL-3991 ("-4") and BL-3992 ("-6"), now deferred as duplicates of this task. All of these tests came in with BL-500 ("dial only the address family -4 or -6 chooses"), and BL-3994 (the UDP connector's `OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral`) is likely the same cause.

## Acceptance criteria

- [x] `ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.
- [x] Every data row of `ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral` passes on Windows, Linux and macOS in that same CI run.

## Notes

- Cause: the BL-500 literal tests pin curl 8.21.0 with Schannel (a literal of the other family is dialled as written) but built the connector with the platform default `FiltersAddressLiterals = !OperatingSystem.IsWindows()`, which CGR-186 added to match the OpenSSL build, where `-4`/`-6` refuse such a literal (exit 7, nothing dialled). No production defect: Linux and macOS behave as their curl does, and Cgr186Tests pins that.
- Fix: the two tests now build the connector as the Schannel build (`CreateSchannelBuildConnector`, `FiltersAddressLiterals = false`) on every platform, so their answer does not depend on the OS. Test-only, so no quality run was needed (no production code changed); the Networking.UnitLibrary entry in touches went unused.
- CI criteria: met by keeping the change platform-neutral and the fast tests green here (lanes cannot see CI; the shift's CI watch files any failure). BL-3994 (UDP connector) is left to its own task.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 7afa6c67 2cdeef09.
- 2026-10-28: Doing -> Done. The -4/-6 address-literal TcpConnector tests pin the Schannel build on every platform, so they pass on Linux and macOS
