---
id: BL-1190
title: Whisper the real board's Backlog depth only, after a pause, as Backlog depth N
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-board.ps1, .claude/hooks/whisper-milestone.ps1]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1190 — Whisper the real board's Backlog depth only, after a pause, as Backlog depth N

## Goal

The Backlog-depth whisper speaks only the real board's depth, as "Curl. Backlog depth, N.", after two silent seconds holding the queue, so it is never a scratch board's number and never runs on from another phrase.

## Context

- Stewart, 2026-10-02, after BL-1182 landed: "The whisper of the backlog depth is not spoken correctly ... the wrong number ... Make sure it is spoken separately from anything else by putting a short delay before it."
- Cause of the wrong number: `Start-BacklogDepthWhisper` in `task-board.ps1` spoke after a move on any board, so lane 9's scratch-board test moves for BL-1182 whispered the scratch copies' small Backlog counts.
- Lanes' claims count the lane worktree just after `Sync-Lane`, so their numbers are right to within the tasks filed since.

## Acceptance criteria

- [x] A move out of Backlog on a board whose repository's `origin` is not `StewartScottRogers/Curl` (a scratch copy under the temp folder) writes no stamp and starts no speaker.
- [x] The main checkout and the lane worktrees still qualify (their `origin` is `https://github.com/StewartScottRogers/Curl.git`).
- [x] The phrase is "Backlog depth, N." and `whisper-milestone.ps1 -PauseSeconds 2` keeps the queue silent for two seconds before speaking.
- [x] `CLAUDE.md`, `SKILL.md`, the script help and the hook header quote the new phrase.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-02: Scratch board under the temp folder: `move -To Deferred` printed its line in 610 ms and left `CurlBacklogDepthWhisper.txt` untouched. Spoken by hand with the real count (20 in the main checkout, 23 on origin, 3 filed since the last pull): two seconds of silence, then the phrase.
## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. The Backlog-depth whisper speaks only the real board, as Backlog depth N, after two silent seconds
