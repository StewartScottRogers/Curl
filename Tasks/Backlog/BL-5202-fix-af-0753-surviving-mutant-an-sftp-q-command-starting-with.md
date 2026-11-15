---
id: BL-5202
title: Fix AF-0753: Surviving mutant: an SFTP -Q command starting with a space can be refused as missing its parameter
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ssh.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5202 — Fix AF-0753: Surviving mutant: an SFTP -Q command starting with a space can be refused as missing its parameter

## Goal

The defect the audit office reported as AF-0753 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0753 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs:99`

Location: `Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs:99`

Mutant 'ArgumentReader arguments = space >= 0' -> 'space > 0' survived. A quote command whose first space is at index 0 (e.g. -Q ' rm x', not trimmed by Parse) then fails with 'Syntax error command ..., missing parameter' instead of reading its arguments as curl does. The stderr text changes; no Parse_Malformed DataRow starts with a space.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs line 99` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs line 99`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
