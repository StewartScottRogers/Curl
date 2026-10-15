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
completed:
---
# BL-2233 — Wire the gap tool to the shared SSH stand-in and certificate folder, and label unmeasured cases by their real skip reason

## Goal

The gap tool runs the SFTP, SCP, HTTPS, FTPS, SMTPS, POP3S and IMAPS upstream cases (about 80) in process, and every unmeasured behaviour case carries the harness's real skip reason instead of a guessed needs-server label.

## Context

Found 2026-10-14 while filing the harness tasks: Gap/Tools/Measure-UpstreamCases.cs builds UpstreamCaseRunner with no UpstreamSshServer and no certificate folder, so every SSH and TLS case skips for a missing variable, although the stand-ins exist and run in Curl.Conformance.UnitTests. Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 sets HarnessServers = @('http','none') and labels any skipped case needs-server:<first non-http server>, whatever the real reason. BL-2217 shares the certificate folder and SSH stand-in construction; its Notes give the exact edit. Gap paths are interactive only, on the gap branch.

## Acceptance criteria

- [ ] Measure-UpstreamCases.cs passes the shared SSH stand-in and certificate folder, and a gap run measures the SSH and TLS cases instead of skipping them.
- [ ] Unmeasured behaviour cases carry the harness's real skip reason, and the converter's self-test proves it.
- [ ] The change lands on the gap branch and its PR merges once CI is green.

## Notes

## Log

- 2026-10-14: Created.
