---
id: BL-760
title: Publish the lanes' heartbeats as status.json on a force-pushed board branch
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-759, BL-756]
touches: [RunDarkFactory.ps1, CLAUDE.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-760 — Publish the lanes' heartbeats as status.json on a force-pushed board branch

## Goal

During a shift, the dark factory's coordinator merges every lane's heartbeat file into one `status.json` every `-HeartbeatMinutes` (default 3), and publishes it once more when the shift ends. A single-runner shift publishes its own status the same way. The file goes to the `board` branch as a single parentless commit, force-pushed. `CLAUDE.md` and Stewart's memory record that force push as a standing exception, beside the `gource` one.

## Context

- Stewart chose this design on 2026-09-28 by picking option 2. That approval covers force-pushing `board`, and this task writes it down as a standing exception so later sessions need not ask.
- The ADR from BL-762 fixes the rest: the `status.json` schema 1, the one-writer rule, the plumbing push, the intervals and the `-HeartbeatMinutes` parameter. BL-759 writes the lane files as `<repo>.logs\lanes-<stamp>\lane-<n>.heartbeat.json`, and BL-756 keeps these pushes from starting CI or Gource.
- **Where the code goes in `RunDarkFactory.ps1`:**
  - The coordinator's wait loop (near line 1509, `while ((Get-Date) -lt $giveUp)`, ticking every 5 s) is where lanes are published.
  - The shift-end code after that loop publishes the final `"state": "ended"`.
  - A single-runner shift (`$Lanes -eq 1`, `$Lane -eq 0`) has no coordinator loop. It publishes from the same points where it writes its lane-0 heartbeat, throttled to `-HeartbeatMinutes`.
- **How to push without touching the checkout:** the coordinator's checkout is Stewart's `Z:\repos\Curl`, so neither its working tree nor its index may change.
  1. `git hash-object -w --stdin` for the JSON;
  2. `git mktree` with the line `100644 blob <sha>`, a tab, then `status.json`;
  3. `git commit-tree <tree> -m "chore(board): lane status <UTC time>"` with no `-p`;
  4. `git push -q --force origin <commit>:refs/heads/board`.
- **Failure handling:** a failed push is traced once per failure streak and never stops the shift or raises the alarm. An unreadable lane file keeps that lane's last good object.
- **Test switch:** BL-759 adds `-TestHeartbeat`. Extend it: after walking lane 1, it writes three fake lane files, merges them, prints the `status.json`, builds the commit object with the plumbing above, and prints the commit id. It does **not** push, because lanes are denied `git push` (`$LaneForbidden`). The first real push is made by the next shift.
- **Do not start a shift to test.** One is running, and lanes run the coordinator's copy of the script, so this change takes effect at the next shift.
- **Memory file:** `C:\Users\Stewart Rogers\.claude\projects\Z--repos-Curl\memory\gource-branch-force-push-authorized.md` records the gource exception. It is outside the repository and not committed. `MEMORY.md` in the same folder indexes it.
- **PowerShell only**, 5.1 compatible. No Python.

## Acceptance criteria

- [x] `RunDarkFactory.ps1` has `[ValidateRange(0, 60)][int]$HeartbeatMinutes = 3`, documented in `param` like its neighbours, where 0 means no publishing. Every place where the script starts itself again (`-NewTab`, the next shift under `-Continuous`) passes it on.
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestHeartbeat` exits 0 and prints a `status.json` that satisfies all of these:
  - it parses with `ConvertFrom-Json`;
  - `schema` is 1, `state` is `running`, and there are three `lanes` sorted by `lane`;
  - `publishedAt`, `shift` and `branch` are set.
- [x] The same run prints a commit id. For that id:
  - `git cat-file -p <id>` shows no `parent` line;
  - `git ls-tree <id>` lists exactly one entry, `status.json`;
  - `git status --porcelain` is the same before and after the run.
- [x] The diff shows:
  - the coordinator publishing every `-HeartbeatMinutes` from its wait loop and once at shift end with `"state": "ended"` and each lane's `phase` set to `finished`;
  - the single runner publishing its lane 0;
  - `--force` used only with `refs/heads/board`;
  - failures traced and swallowed.
- [x] `CLAUDE.md`, "Git and GitHub", has a new paragraph directly after the `gource` exception. It says that the `board` branch holds only the dark factory's latest `status.json` and is force-pushed every few minutes by `RunDarkFactory.ps1`'s coordinator (Stewart, 2026-09-28), and that this force push, to that branch only, needs no confirmation. The "Dark factory" section gains one sentence saying lanes' heartbeats are published there for the live board page.
- [x] The memory file above, and its `MEMORY.md` index line, cover both the `gource` and `board` branches. Only those two branches are named, and every other force push still needs confirmation. The file keeps its name, or `MEMORY.md` is updated to match if it is renamed.
- [x] The script's header "LIVE BOARD" paragraph (from BL-759) describes the publishing, the `board` branch and `-HeartbeatMinutes`.
- [x] `[System.Management.Automation.Language.Parser]::ParseFile` reports no errors for `RunDarkFactory.ps1`.

## Notes

- Plan: board-branch functions sit after the heartbeat section of `RunDarkFactory.ps1`: `Get-BoardStatusJson` (merges `lane-*.heartbeat.json`, keeps a lane's last good object, sorts by lane, marks all `finished` for `ended`), `New-BoardCommit` (plumbing only), `Publish-BoardStatus` (force-push to `refs/heads/board`, failure traced once per streak and swallowed) and `Publish-BoardStatusIfDue`. The coordinator calls the throttled one on every 5 s tick and publishes `ended` after reading the lane summaries; the single runner publishes from `Write-Heartbeat` (lane 0 only) and `ended` after its final heartbeat.
- Choice: nothing is piped to git. Windows PowerShell's pipe to a native command prepended a byte order mark and `git mktree` rejected the line (measured). So the blob is `git hash-object -w <utf-8 file>` instead of `--stdin`, and `mktree` reads its line through `cmd /s /c ... < file`. Same objects, no encoding risk for non-ASCII titles.
- `-NewTab` forwards every bound parameter already, so `-HeartbeatMinutes` passes on without a change there; the `-Continuous` next shift now passes it explicitly. Lanes do not need it: they never publish.
- Verified: `-TestHeartbeat` exits 0, its `status.json` parses with schema 1, state running, lanes 1,2,3 (written 3,1,2); the commit has no `parent` line, `git ls-tree` lists only `status.json`, and `git status --porcelain` is unchanged. `ParseFile` reports 0 errors. Build clean, fast tests green.
- The memory file keeps its name; its description and `MEMORY.md` line now name both `gource` and `board`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The coordinator (and a single runner) publishes the lanes' heartbeats as status.json on the force-pushed board branch every -HeartbeatMinutes and at shift end
