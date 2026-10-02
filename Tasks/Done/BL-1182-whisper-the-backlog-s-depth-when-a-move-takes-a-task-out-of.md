---
id: BL-1182
title: Whisper the Backlog's depth when a move takes a task out of Backlog
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-board.ps1, .claude/skills/task-board/SKILL.md, .claude/hooks/whisper-milestone.ps1, CLAUDE.md]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1182 — Whisper the Backlog's depth when a move takes a task out of Backlog

## Goal

Each time `task-board.ps1 move` takes a task out of `Backlog`, Stewart hears the Backlog's depth whispered ("Backlog, 63."), at most once per burst of moves.

## Context

- Stewart asked on 2026-10-02: "Every time the number of backlogged tasks is decreased I want you to use a whisper audio to tell me its depth." He confirmed: the number is the `BL-*.md` files in `Tasks/Backlog` only (not Backlog + Doing), and **not** one phrase per move when moves come back to back - one phrase with the latest depth.
- A decrease is a `move` whose source state is `Backlog` (to Doing, Blocked or Deferred). `new` raises the depth and stays silent.
- It must live in `task-board.ps1`, not only in the PostToolUse hook: lanes' claims are made by `RunDarkFactory.ps1` calling `task-board.ps1 move -To Doing` directly, which no Claude hook sees. Interactive moves go through the same script, so one place covers both.
- Reuse the voice and queue of `.claude/hooks/whisper-milestone.ps1`: `System.Speech`, Zira, the same very quiet volume, and the `Global\CurlWhisper` named mutex so it never talks over "Committed" or "Task done". Factor the speaking into one shared function or script both use, rather than copying it.
- Coalescing (decided by Claude under Stewart's delegation): on each decrease, write the depth and a timestamp to one shared file in the user's temp folder, then start a detached, hidden speaker that waits 60 seconds and speaks only if the file still holds its own timestamp. A burst of moves across 9 lanes then gives one phrase, with the latest depth, 60 seconds after the last move.
- `move` must not wait for speech, and never fails because of it: every speech error is swallowed. Off Windows (no `System.Speech`), and when `CI` is set, it stays silent.

## Acceptance criteria

- [x] `task-board.ps1 move -Id <a Backlog task> -To Doing` in a scratch copy of the board returns as fast as before (no speech wait), and about 60 s later "Backlog, <n>." is spoken, where n is the Backlog count after the move.
- [x] Three moves out of Backlog within 60 s give exactly one phrase, naming the count after the third. Checked by hand; Notes records the result.
- [x] `new`, and moves from Doing, Blocked or Deferred, speak nothing.
- [x] The speaking is shared by `whisper-milestone.ps1` and `task-board.ps1` (one implementation), and the hook's four existing phrases still work.
- [x] A failure to speak (no `System.Speech`, mutex timeout) leaves `move`'s output and exit code unchanged.
- [x] `whisper-milestone.ps1`'s header and `.claude/skills/task-board/SKILL.md` describe the new phrase; root `CLAUDE.md`'s whisper sentence names it.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- One implementation: `whisper-milestone.ps1` gained a `-Phrase` mode (with `-DelaySeconds`, `-StampFile`, `-Stamp`) and an `Invoke-Whisper` function that the hook's four phrases also use. `task-board.ps1`'s `Start-BacklogDepthWhisper` starts it detached and hidden. Doing it this way kept the change inside `touches`, with no new shared file.
- Stamp file: `%TEMP%\CurlBacklogDepthWhisper.txt`, holding `<guid>|<depth>`. A speaker talks only if the file still starts with its own guid. The phrase is "Curl. Backlog, <n>." (every whisper names Curl, BL-901).
- Checked by hand 2026-10-02 in a scratch copy of the board. Three Backlog -> Deferred moves (21 -> 18) each returned exit 0 at the usual ~3 s per call (a move with `CI` set, which spawns nothing, also took ~2.9 s). Three speakers were spawned and the stamp file held only the third one's stamp (depth 18). Run directly, a speaker with a stale stamp exited silently after its delay (1.7 s for a 1 s delay), one with the current stamp spoke (7.9 s), and the hook's "Task done" phrase still spoke. `new` and Deferred -> Backlog left the stamp file untouched and spawned nothing.
- Errors: all of `Start-BacklogDepthWhisper` and the `-Phrase` path sit inside try/catch, so `move`'s output and exit code are unchanged. Off Windows (`OSVersion.Platform` is not `Win32NT`) and with `CI` set, nothing is spawned.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A move out of Backlog whispers the Backlog's depth a minute later, once per burst, through the shared whisper-milestone.ps1 speaker.
