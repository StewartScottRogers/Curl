---
id: BL-2233
title: Wire the gap tool to the shared SSH stand-in and certificate folder, and label unmeasured cases by their real skip reason
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2217]
touches: [Gap]
lane: no
requirement: none
created: 2026-10-14
completed: 2026-10-15
---
# BL-2233 — Wire the gap tool to the shared SSH stand-in and certificate folder, and label unmeasured cases by their real skip reason

## Goal

The gap tool runs the SFTP, SCP, HTTPS, FTPS, SMTPS, POP3S and IMAPS upstream cases (about 80) in process, and every unmeasured behaviour case carries the harness's real skip reason instead of a guessed needs-server label.

## Context

Found 2026-10-14 while filing the harness tasks: Gap/Tools/Measure-UpstreamCases.cs builds UpstreamCaseRunner with no UpstreamSshServer and no certificate folder, so every SSH and TLS case skips for a missing variable, although the stand-ins exist and run in Curl.Conformance.UnitTests. Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 sets HarnessServers = @('http','none') and labels any skipped case needs-server:<first non-http server>, whatever the real reason. BL-2217 shares the certificate folder and SSH stand-in construction; its Notes give the exact edit. Gap paths are interactive only, on the gap branch.

## Acceptance criteria

- [x] Measure-UpstreamCases.cs passes the shared SSH stand-in and certificate folder, and a gap run measures the SSH and TLS cases instead of skipping them.
- [x] Unmeasured behaviour cases carry the harness's real skip reason, and the converter's self-test proves it.
- [x] The change lands on the gap branch and its PR merges once CI is green.

## Notes

- 2026-10-15 (Claude): PR #146 (9b68f0a1a). The tool also wires the HTTP/2 and HTTP/3 stand-ins; Get-UpstreamRelease.ps1 now extracts tests/certs. Of the 116 cases the 2026-10-14_0904 run skipped for SSH/TLS/HTTP2-3 variables, 72 pass and 4 fail. Rule 5 reads the harness's reason; converter self-test cases 21 and 22.

## Log

- 2026-10-14: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Merged in PR #146.
