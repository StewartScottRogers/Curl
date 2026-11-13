---
id: BL-5143
title: Refuse unsafe batch arguments in Plugin helpers when the program name ends in dots, whitespace or a control character
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Plugin.UnitLibrary/SystemHelperProcessStarter.cs, Curl.Extended.Protocol.Plugin.UnitTests]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5143 — Refuse unsafe batch arguments in Plugin helpers when the program name ends in dots, whitespace or a control character

## Goal

A .cmd or .bat Plugin helper whose program name ends in dots, whitespace or control characters still has an unsafe URL refused.

## Context

Same hole as BL-5129 (MCP), in SystemHelperProcessStarter.RefuseUnsafeBatchArgument (ADR-1248).

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

Criterion met: RefuseUnsafeBatchArgument sees through trailing dots, whitespace and control characters, pinned by BatchHelperArgumentTests. Mirrored BL-5129 (IsBatchFile trims trailing . / whitespace / control). Quality run: library 100% line and branch, no failing members. Platform-neutral tests.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-13: Doing -> Done. Plugin batch-argument refusal now sees through trailing dots, whitespace and control characters
