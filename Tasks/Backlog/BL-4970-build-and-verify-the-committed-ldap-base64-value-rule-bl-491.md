---
id: BL-4970
title: Build and verify the committed LDAP base64 value rule (BL-4913 verification)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapEntryFormatter.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4970 — Build and verify the committed LDAP base64 value rule (BL-4913 verification)

## Goal

The committed fix for BL-4913 (LDAP values written as `:: base64` when curl 8.22.0 does) builds clean, passes the fast tests and meets the quality gate on the pinned SDK.

## Context

BL-4913's code and tests are committed on a lane branch (the `fix(ldap)` commit); only its last criterion was left, because the pinned SDK was missing. SDK 10.0.404 is installed now. See BL-4913's Notes for the measurements and ADR-1242.

## Acceptance criteria

- [ ] `dotnet build` at the repository root is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes for Curl.Original.Protocol.Ldap.UnitTests (a failure in `Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal` is BL-4962's, not this task's).
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Ldap.UnitLibrary` reports `LdapEntryFormatter` inside every gate.

## Notes

Split from BL-4913 (cost cap). All of BL-4913's code work is already committed, so this only verifies and fixes what the build or gate shows.

## Log

- 2026-11-11: Created.
