---
id: BL-5201
title: Fix AF-0752: Surviving mutant: the sftp/scp directory-listing test that picks the -v closing line can be inverted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ssh.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5201 — Fix AF-0752: Surviving mutant: the sftp/scp directory-listing test that picks the -v closing line can be inverted

## Goal

The defect the audit office reported as AF-0752 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0752 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:370`

Location: `Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:370`

Mutant 'context.Url.Scheme != ScpScheme && ...' -> 'context.Url.Scheme == ScpScheme && ...' survived. ListsDirectory decides, after a failed transfer, between the 'Closing connection' and 'Connection #n left intact' info lines (line 310) and feeds FailedWhileTransferring's SSH-layer case. Inverting it changes -v output bytes for a failed sftp directory listing and scp of a directory path. No test drives a failing sftp listing through the handler.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs line 370` as the evidence above describes (the mutant operator is `!=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs line 370`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 57d5291a 9742157d.
