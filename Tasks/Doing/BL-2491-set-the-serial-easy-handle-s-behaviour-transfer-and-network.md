---
id: BL-2491
title: Set the serial easy handle's behaviour, transfer and network options through CurlEasy.SetOption
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2451]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2491 — Set the serial easy handle's behaviour, transfer and network options through CurlEasy.SetOption

## Goal

A serial transfer's EasyBehaviourOptions, EasyTransferOptions and EasyNetworkOptions reach its Libcurl CurlEasy as curl_easy_setopt calls (CurlEasy.SetOption, one per CURLOPT_* the command line sets), as upstream's config2setopts.c sets them, instead of being written whole onto the engine transfer's Options.

## Context

ADR-0506, ADR-0624; BL-2451 routed serial transfers through CurlEasy (ToolCurlEngine, ToolEasyHandle) and sets only CURLOPT_URL through SetOption; CurlCommandRunner.SetUpToolEasy still assigns ToolEasyHandle.Hooks.Options = EasyOptionsFromCommandLine.EasyOptionsOf(...). Turn BehaviourOf, TransferOf (with the per-transfer UploadTransferOf) and NetworkOf into SetOption calls for every field libcurl has a CURLOPT_* for, using Libcurl's setters (Libcurl/Options/<Group>) as they are. A field with no CURLOPT_* (a Curl extension such as the --httpsig-* options, or a value a setter would refuse or clamp differently from what the tool needs today) stays on the hooks' Options; record each such field and why in an ADR. A setter Libcurl lacks or that answers differently from the record is filed as a Libcurl task (touches Libcurl/Options/<Group>), not changed here. A non-Ok answer from SetOption must end the transfer as curl's tool does (its setopt failure path), measured on real curl where reachable.

## Acceptance criteria

- [ ] Every EasyBehaviourOptions, EasyTransferOptions and EasyNetworkOptions field the command line sets with a CURLOPT_* equivalent is set through CurlEasy.SetOption; CurlCommandRunnerEasyTransferTests pins it for a representative option of each group.
- [ ] The output is byte for byte unchanged: dotnet build is clean and the fast tests pass with no test deleted, skipped or weakened, and Curl.Conformance.UnitTests/PassingUpstreamCases.txt is unchanged or grown.
- [ ] Measure-CodeQuality.ps1 -Library Curl.Console reports 0 failing members.
- [ ] Curl.Console/CLAUDE.md says which fields go through SetOption and which stay on the hooks.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
