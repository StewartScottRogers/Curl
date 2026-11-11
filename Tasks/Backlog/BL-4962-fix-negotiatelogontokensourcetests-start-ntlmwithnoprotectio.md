---
id: BL-4962
title: Fix NegotiateLogonTokenSourceTests.Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal failing on lane machines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitTests/NegotiateLogonTokenSourceTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4962 — Fix NegotiateLogonTokenSourceTests.Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal failing on lane machines

## Goal

`Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal` passes on the lane machines again.

## Context

BL-4913 (2026-11-11) saw it fail in `Curl.Original.Protocol.Ldap.UnitTests` with `Assert.AreEqual(0, token[12] & 0x30)`, built with SDK 11.0.100 because the pinned 10.0.4xx SDK was missing; BL-4913 did not change NegotiateLogonTokenSource. Find whether the OS NTLM package or the runtime now sets the sign/seal flags, and pin what the platform answers.

## Acceptance criteria

- [ ] The test passes on Windows, or is replaced by tests pinning each platform's answer.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-11: Created.
