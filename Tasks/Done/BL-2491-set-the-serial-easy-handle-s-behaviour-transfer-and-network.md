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
completed: 2026-10-16
---
# BL-2491 — Set the serial easy handle's behaviour, transfer and network options through CurlEasy.SetOption

## Goal

A serial transfer's EasyBehaviourOptions, EasyTransferOptions and EasyNetworkOptions reach its Libcurl CurlEasy as curl_easy_setopt calls (CurlEasy.SetOption, one per CURLOPT_* the command line sets), as upstream's config2setopts.c sets them, instead of being written whole onto the engine transfer's Options.

## Context

ADR-0506, ADR-0624; BL-2451 routed serial transfers through CurlEasy (ToolCurlEngine, ToolEasyHandle) and sets only CURLOPT_URL through SetOption; CurlCommandRunner.SetUpToolEasy still assigns ToolEasyHandle.Hooks.Options = EasyOptionsFromCommandLine.EasyOptionsOf(...). Turn BehaviourOf, TransferOf (with the per-transfer UploadTransferOf) and NetworkOf into SetOption calls for every field libcurl has a CURLOPT_* for, using Libcurl's setters (Libcurl/Options/<Group>) as they are. A field with no CURLOPT_* (a Curl extension such as the --httpsig-* options, or a value a setter would refuse or clamp differently from what the tool needs today) stays on the hooks' Options; record each such field and why in an ADR. A setter Libcurl lacks or that answers differently from the record is filed as a Libcurl task (touches Libcurl/Options/<Group>), not changed here. A non-Ok answer from SetOption must end the transfer as curl's tool does (its setopt failure path), measured on real curl where reachable.

## Acceptance criteria

- [x] Every EasyBehaviourOptions, EasyTransferOptions and EasyNetworkOptions field the command line sets with a CURLOPT_* equivalent is set through CurlEasy.SetOption; CurlCommandRunnerEasyTransferTests pins it for a representative option of each group.
- [x] The output is byte for byte unchanged: dotnet build is clean and the fast tests pass with no test deleted, skipped or weakened, and Curl.Conformance.UnitTests/PassingUpstreamCases.txt is unchanged or grown.
- [x] Measure-CodeQuality.ps1 -Library Curl.Console reports 0 failing members.
- [x] Curl.Console/CLAUDE.md says which fields go through SetOption and which stay on the hooks.

## Notes

- `EasySetoptsFromCommandLine.Of` gives one `EasySetopt` per behaviour, transfer and network field off libcurl's default (one per `CURLOPT_*` the command line sets); `ToolEasyHandle.SetOptions` makes them through `CurlEasy.SetOption` after the hooks get `KeptOnHooks` (ADR-0631). Default taken: a field at libcurl's default makes no call, so a plain command line makes only `CURLOPT_URL` and `CURLOPT_NOPROGRESS 0`.
- Kept on the hooks (ADR-0631): `--ip-tos`, `--vlan-priority`, `--mptcp` (no `CURLOPT_*`), a buffer below 1024 or past 10 MB (`--limit-rate 100` makes it 100; `CURLOPT_BUFFERSIZE` would raise it), and times that are not whole setter units or past `INT_MAX`. No Libcurl setter needed a change, so no Libcurl task was filed.
- Setopt failure: unreachable from any command line - measured on curl 8.21.0 and Curl alike, `-C -5`, `--max-filesize -1`, `-Y -1`, `-y -1`, `--keepalive-cnt -1`, `--connect-timeout -1`, `--proto -all` and `--proto =ipfs` are all refused while parsing with exit 2. It ends the transfer with the call's code and `setopt 0x<option> got bad argument` (BL-600's measured form) and ends the run; the runner's `easySetoptsOf` parameter is the test seam.
- Found while measuring: `--proto =ipfs,http` is accepted by curl 8.21.0 (exit 7) and refused by Curl (exit 2); filed as BL-2496.
- ADR-0630 was taken on the shared branch, so this is ADR-0631.
- Measured: full fast tests green (Curl.Console.UnitTests 2806 passed); Measure-CodeQuality -Library Curl.Console 0 failing members after moving the runner's `??` into `GivenOrCommandLineSetopts` and dropping cached method groups from `NetworkKeptOnHooks`.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Serial transfers set their behaviour, transfer and network options through CurlEasy.SetOption (ADR-0631)
