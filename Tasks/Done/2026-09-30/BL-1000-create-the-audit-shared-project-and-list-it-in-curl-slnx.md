---
id: BL-1000
title: Create the Audit shared project and list it in Curl.slnx
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-996, BL-997, BL-998]
touches: [Audit/Audit.shproj, Audit/Audit.projitems, Audit/README.md, Curl.slnx]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1000 — Create the Audit shared project and list it in Curl.slnx

## Goal

`Audit/` is a Visual Studio shared project listed in `Curl.slnx` beside `Documentation`, `.claude/Claude` and `Tasks`, with a `README.md` that maps its folders, and the solution still builds clean.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1000`. It
depends on all three guards (BL-996, BL-997, BL-998) so the folder exists only once the
factory is kept out of it.

Mirror `Tasks/`: `Tasks/Tasks.shproj` (a `.shproj` whose Visual Studio imports are each
guarded by `Condition="Exists(...)"` so an SDK-only machine can load it) and
`Tasks/Tasks.projitems` (a single recursive `None` glob, with the comment saying why the
glob is load-bearing). Use a new `ProjectGuid`/`SharedGUID` pair (generate one with
`[guid]::NewGuid()`), `Import_RootNamespace` `Audit`. In `Curl.slnx`, add
`<Project Path="Audit/Audit.shproj" />` with the other shared projects at the end, and
add Audit to the comment that lists the shared projects. `Audit/Guard/` already exists
(BL-998); the glob picks it up.

`Audit/README.md` states, as the audit office's front page:

- what the office is and that it audits the dark factory and the code it produced (cite
  the ADR from BL-994);
- the folder map: `Guard/` (the CI guard script), `Instructions/` (each auditor's method,
  and `Report-Format.md`), `PlantedDefects/` (the catalogue), `Findings/`, `Scorecards/`,
  `Tools/` (audit tooling; not product code, not held to the coverage gates),
  `RunAudit.cmd`/`RunAudit.ps1`, and the agents in `.claude/agents/audit-*.md`;
- that folders not yet built are marked "planned (BL-###)" with their task IDs
  (BL-1001 to BL-1020), so the README is true on the day it lands;
- the independence rules in one paragraph each: interactive-only tasks, the hook, the CI
  guard, the `audit` branch and Stewart's approval of every merge.

## Acceptance criteria

- [x] `Audit/Audit.shproj` and `Audit/Audit.projitems` exist, their GUIDs match each other and differ from every other `.shproj` in the repository, and every CodeSharing import is guarded by `Condition="Exists(...)"`.
- [x] `Curl.slnx` lists `Audit/Audit.shproj` once, beside the other shared projects, and its header comment names Audit among the shared projects.
- [x] `dotnet build Curl.slnx -warnaserror` succeeds.
- [x] `Audit/README.md` contains the folder map above, and every planned folder names its task ID.

## Notes

- Built on the `audit` branch (worktree `Z:/repos/Curl.audit`, updated to master 8daea3fe), commit b2bb89da, pull request https://github.com/StewartScottRogers/Curl/pull/27, not on `work/dark-factory`: `Audit/` changes only through the audit branch. The work is complete there; it reaches master when Stewart merges #27, and work/dark-factory takes it by merging master. Convention for the audit tasks from here (Decided by Claude): a task is Done when its work is complete and verified on the audit branch, with the pull request that carries it named here.
- GUID pair {422A221E-CC14-4F14-A6E7-A3A64623E59D}, unlike Documentation, .claude/Claude and Tasks. `Audit.projitems` globs every file (`**/*`, excluding the two project files), since the office holds scripts beside Markdown.
- `dotnet build Curl.slnx -warnaserror` on the audit branch: 0 warnings, 0 errors; `dotnet sln list` shows `Audit/Audit.shproj`.
- `Audit/README.md`: folder map with each planned folder's task (BL-1001 to BL-1020, BL-1028), and one paragraph each on interactive-only tasks, the hook, the CI guard, and the audit branch with Stewart's approval.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Audit is a shared project in Curl.slnx with a README mapping the office; on the audit branch in PR #27, awaiting Stewart's merge.
