---
id: BL-4367
title: Split Curl.Protocol.Ssh.UnitTests so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests, Curl.Protocol.Ssh.IntegrationTests/Curl.Protocol.Ssh.IntegrationTests.csproj, Tasks/Backlog/BL-4329-move-ssh-keys-host-keys-and-user-authentication-into-curl-ss.md, Tasks/Backlog/BL-4330-move-the-ssh-connection-layer-into-curl-ssh-unitlibrary-and.md, Tasks/Backlog/BL-4362-create-curl-ssh-unitlibrary-and-move-the-shared-ssh-root-typ.md, Tasks/Deferred/BL-4363-split-sshdiagnosticlog-into-a-transport-log-and-protocol-onl.md, Tasks/Deferred/BL-4364-move-ssh-compression-packet-protection-and-negotiation-into.md, Tasks/Deferred/BL-4365-move-ssh-key-exchange-transport-and-host-key-verifiers-into.md]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4367 — Split Curl.Protocol.Ssh.UnitTests so tasks touching it can run in parallel

## Goal

`Curl.Protocol.Ssh.UnitTests` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 3 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Curl.Protocol.Ssh.UnitTests`: BL-4329, BL-4330, BL-4362, BL-4363, BL-4364, BL-4365. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Protocol.Ssh.UnitTests` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-4329, BL-4330, BL-4362, BL-4363, BL-4364, BL-4365) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Protocol.Ssh.UnitTests`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The project already held one file per test class and fake (49 entries, partial classes split by concern); what tied every move task to the whole project was 159 `using Curl.Protocol.Ssh.<Folder>;` lines in 84 files. Replaced them with one `GlobalUsings/Curl.Protocol.Ssh.<Folder>.cs` per moving namespace (Authentication, Compression, Connection, HostKeys, KeyExchange, Keys, Negotiation, PacketProtection, Transport), so each move edits one file; recorded in ADR-1162 (decided by Claude under Stewart's delegation).
- `Curl.Protocol.Ssh.IntegrationTests.csproj` links three unit-test fakes, which lost their usings, so it now links the `GlobalUsings` folder too; added to `touches` because no task in Doing on origin/work/dark-factory names it. The six tasks' front matter was also edited (criterion 2), so their files are in `touches`.
- Narrowed BL-4329, BL-4330, BL-4362, BL-4363, BL-4364 and BL-4365 to their own folders, global using files and `Fakes`. `task-board.ps1 hotspots -Min 1` now shows at most 4 tasks on any Curl.Protocol.Ssh.UnitTests path, BL-4367 included (3 once it is Done); plain `hotspots` (min 5) prints none.
- `dotnet build` clean (0 warnings); fast tests: all 112 test projects passed (Curl.Protocol.Ssh.UnitTests 1942). No production library changed, so no quality run.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 8c36617f 4b1cb027 a0b49a29.
- 2026-10-30: Doing -> Done. Curl.Protocol.Ssh.UnitTests imports each moving SSH namespace through its own global using file, and the six SSH move tasks touch only their own files there
