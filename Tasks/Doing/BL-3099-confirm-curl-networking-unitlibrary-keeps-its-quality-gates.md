---
id: BL-3099
title: Confirm Curl.Networking.UnitLibrary keeps its quality gates after the curl 8.22.0 DoH decode
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3097, BL-3098]
touches: [Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3099 — Confirm Curl.Networking.UnitLibrary keeps its quality gates after the curl 8.22.0 DoH decode

## Goal

The solution builds clean, the fast tests pass, and `Curl.Networking.UnitLibrary` keeps its coverage and complexity gates after the curl 8.22.0 DoH decode.

## Context

Gate half of BL-2969, run after BL-3097 and BL-3098. BL-3013 already measured the library once; this confirms it on the final tree. Changes nothing unless a gate fails, then add the missing test to `Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs`.

## Acceptance criteria

- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and complexity at most 10 for every member.

## Notes

- Split from BL-2969 (over its cost cap); BL-2969's third criterion.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: c5f8cef6 990cf5be.
