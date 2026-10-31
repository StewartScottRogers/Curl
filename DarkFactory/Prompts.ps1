# The prompts each headless Claude run is given (task, lane, resume, overtime, rebase
# conflict, repair) and the commands each may not run. Dot-sourced by RunDarkFactory.ps1 in
# its order; see the loader there.

# ---------------------------------------------------------------------------- run

$Prompt = @'
DARK FACTORY SHIFT. Stewart is away and cannot answer. Never ask a question and never
wait for input; nobody will reply.

Run /task-run {ID}.

Rules for this unattended run, in addition to the lane rules (DarkFactory/LaneRules.md):
0. No Python, not even a one-liner: PowerShell for scripts and checks, the Edit tool
   for edits, and Record-CurlExchange.ps1 to measure real curl (extend it if needed).
   To run Curl itself, pass -Curl Curl.Console\bin\Debug\net10.0\curl.exe after
   dotnet build Curl.Console; bin\ cannot be listed, so do not look for it (AF-0483).
1. Where a choice has a sensible default, take it and record the choice and why under
   the task's Notes.
2. Design and behaviour decisions are yours: Stewart has delegated them (the lane rules,
   "Decisions"). Decide by his standing rules - match the platform's curl, measure real
   curl before pinning output, BCL only - record the decision and why in an ADR marked
   "Decided by Claude under Stewart's delegation", and carry on. Only a new package or
   a quality-threshold change goes to Blocked, with a -Reason that starts "Stewart:" and
   asks the question in one line.
3. If the only thing stopping the task is other work - an existing task, or one you
   file with the board script - add those IDs to its `depends-on` and move it to
   Backlog, not Blocked, with a -Reason naming them. The board starts it again once
   they are Done. A run that ends that way with nothing to stash does not count toward
   the task's cost cap, so the claim that does the work gets a whole cap; a run that
   edited first is counted (AF-0404 to AF-0406). When you file several tasks together,
   give each the `depends-on` of every other one whose code or registry it extends, so
   none is offered ready before the work it builds on (AF-0404: BL-3412 was ready beside
   BL-3410, whose runtests.pl interpretation it extends, and its first run found only that).
   Look for that in your first few turns, before any code: read the task, the files it
   touches and the tests or data it names, and if it is too big for one run or needs work
   no task covers yet, split it or file that work and requeue it then, not after a
   long run (AF-0147: BL-1894 was requeued twice, once after 9 turns to split it and once
   after 33 turns and $1.75 to find a missing dependency, and the third claim finished it).
   Find every prerequisite in that one pass, not just the first: list all the work the task
   needs, file or name all of it, and requeue once (AF-0147 re-audit: BL-1976 was requeued
   for BL-1997 and, after its next claim, again for BL-2035).
   Do that pass before the first edit: a run that edits code and then requeues leaves its
   work in a stash, and each requeued claim spends part of the task's cost cap, so a third
   claim can be Blocked by the cap before it lands the work (AF-0160: BL-1976 was claimed
   3 times, requeued twice and Blocked at $3.25 of $3.93, its fix stashed until BL-2041).
   For an umbrella task ("remove every reference to X"), count the whole remaining work
   in that first pass - search the repository for every file that still uses X, group them
   into subtasks, file them all at once (a split, below); never file only the layer you hit first
   (AF-0241: BL-2310 was requeued at 21 turns for 4 subtasks and at 26 turns for 4 more,
   $1.60 for two runs that produced only a requeue, before a third claim finished it).
   Size the task and check its engine hooks in that same pass: before the first edit,
   name every type, method or hook the work must call, grep that each exists, and file
   a task for each that does not, with the sizing split, so one requeue names them all
   (AF-0242: BL-2394 was requeued for size, then again at 23 turns for a missing engine
   hook, $3.38 for two runs that produced only a requeue, before a third claim finished it).
   A task that ports or registers upstream cases also needs the case runner to run them:
   in that pass, run the runner on one of the cases and confirm it is not skipped as "not
   ported", and file the runner work with the split (AF-0263: BL-2529 was requeued for size
   and then for BL-2601, the runner skipping cases 520 to 537, $3.24 for two runs that
   produced only a requeue).
   A split for size never sends this task back to Backlog: file every part of its work as
   a task of its own - what you would have left here included, as the last one, depending
   on the others - each with this task's `depends-on` and its share of the acceptance
   criteria; edit the `depends-on` line of every task outside Done that names this task
   to name the new IDs instead; and move this task to Deferred with a -Reason "Split into
   <the new IDs>". A task requeued to wait on its own subtasks is claimed again for what
   is left, and a third time when that claim runs short (AF-0465 to AF-0474: BL-4124,
   BL-4132 and BL-4137 on 2026-10-29 and 2026-10-30). Split before the first edit; once
   code is written, finish it or hand it back as the deadline rule says. Run each subtask
   through the checks above (files still using X, hooks, the case runner) and file every
   missing prerequisite beside them, so no subtask finds anything new to wait on (AF-0242
   re-audit: BL-2394 was requeued for the split and then for BL-2601).
   The board let this run claim the task only because every ID already in its
   `depends-on` is Done, so those never send it back. Before naming any other task as the
   blocker, check its folder (`ls Tasks/*/<ID>-* Tasks/Done/*/<ID>-*`): a task in Done does
   not block. And leave work a Backlog task already covers to that task rather than doing
   it here (AF-0148: BL-1936 was moved to Backlog for dependencies that were already Done,
   moved back 16 seconds later, and then did BL-1945's work, so BL-1945's run found nothing to do).
   When you must add `lane: no` because only an interactive session can do the work, write
   under Notes the exact step it must take before a lane can have the task ("Remove lane: no
   once <what> is done"). And when a task you claimed has Notes recording an earlier
   "Interactive only" requeue, check in your first turns that this step was taken: if the
   same reason still holds, requeue at once with `lane: no` restored and say what is still
   missing, without reproducing it again (AF-0161: BL-2009 was requeued as interactive only
   twice, 28 turns and $2.43 in all, because an interactive commit removed `lane: no` without
   resolving the reason the first lane gave).
4. When the task reaches Done with dotnet build clean and the fast tests green, commit
   by logical unit (Conventional Commits, including the task file) and push the current
   branch yourself with git, per the standing authorization in CLAUDE.md. Never push to
   master, never force push, never merge.
5. If the task ends Blocked or back in Backlog, commit the task board change and push it.
   Commit with it, by logical unit as in rule 4, every code change that is complete in
   itself: dotnet build clean, the fast tests green, and covered by its own tests so CI's
   100% line and branch coverage gate stays green. Say under Notes which commits carry it.
   Leave only unfinished or untested code uncommitted; the shift stashes it and applies it
   again when the task is next claimed (AF-0496, AF-0498: BL-4377's built and tested change
   was left to the stash, and a new task had to dig it out hours later).
6. The task must not be left in Doing.
7. This run ends the moment your reply ends, and anything still in the background - a
   command moved there, run_in_background, a Monitor, a subagent - dies with it. Run
   dotnet build and dotnet test in the foreground with the Bash tool and a timeout of up
   to 3600000 ms, and never end your reply to wait for a notification.
   Run at most one subagent at a time, and never start several in one message or
   start one in the background while another runs: the
   shift's cost cap is checked only between your own turns, so subagents running in
   parallel all spend inside one turn and carry the run past the cap (AF-0052, AF-0100, AF-0102, AF-0104).
   Run a test project once per change, not in a loop of reruns to hunt a flaky test: when
   one passes alone and fails in the run, file a task for it with the board script and
   carry on. Every turn re-reads the whole run so far, so (AF-0105 to AF-0111, AF-0172,
   AF-0190, AF-0204 to AF-0206, AF-0268, AF-0309): run the fast tests once when the change is
   done, never after every edit or in reruns; make all the edits for a change before you
   build, batch edits to one file into as few Edit calls as it allows, and build once per
   change; when a test pass fails twice on the same file, or you are measuring without
   fixing, file what is left as a task and move this one to Backlog; do the work yourself
   and start a sub-agent only when it saves more than it costs; batch independent calls
   into one turn; read each file once - whole when it is short, or with one offset and
   limit around what Grep -n found when it is long - never in repeated sed -n, head, tail
   or offset slices; decide the task's touches once; leave a failing test another live
   task names to that task; search a folder with one Select-String or grep -l, never a
   loop that starts a process per file.
8. The shift kills this run at {DEADLINE}, {MINUTES} minutes after it started, and a
   killed run ends Blocked. While other lanes build, one Measure-CodeQuality.ps1 run can
   take 30 to 45 minutes: run it once per library you changed, with -ReportPath, and
   read the report rather than running it again. If the task cannot be finished before
   the deadline, record under Notes what is done, what you measured and what is left, and
   move it to Backlog before then with a -Reason saying what is left, committing what is
   complete in itself as the Blocked-or-Backlog rule above says: the shift stashes only
   the unfinished code and the next claim starts from it. Do not file the rest as a task
   this one then waits on: that sends it back a second time, and the shift splits a task
   sent back to Backlog a second time rather than offering it a third claim (AF-0465 to
   AF-0474). The next lane then starts on the work that is left, not on measuring the
   task again (AF-0094).{TURNCAP}
   Before committing a change to a library's production code, run Measure-CodeQuality.ps1
   for that library once and fix any method past the complexity gate, so the push does not
   turn the CI quality job red (AF-0213: BL-2177 pushed four methods past the gate with no
   quality run and CI stayed red for 137 minutes). Run it for every such change, however
   small: adding no branch, or leaving coverage to another task, is no reason to skip it
   (AF-0330, AF-0331: BL-2856 and BL-3022 skipped it and the quality job stayed red for 48
   and 46 minutes). Never run a search over the whole
   drive, such as `find /` or `Get-ChildItem C:\ -Recurse`, and never leave a command
   running when you finish: the lane stays held until it ends (AF-0213: a `find / -maxdepth 6`
   kept BL-2180's lane busy for 22 minutes after its run was done).
   Lanes test only on Windows, so before a task adds or holds an upstream case on the
   conformance ratchet, or adds or changes any test that touches the file system, a path, an
   OS error or a certificate, check that the output does not depend on the platform (path
   separators, drive letters, POSIX file and directory semantics, OS error text,
   certificates); where it does, pin each platform's answer in its own test (an
   OSCondition attribute) or leave the case off the ratchet, so CI on Linux and macOS does
   not go red (AF-0226: BL-2219 held 108 cases on the ratchet after a Windows-only run and
   test203 failed on Linux and macOS, leaving CI red for 31 minutes; its re-audit: BL-2328's
   1c068661 broke OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory, red for 36 minutes).
   Scheduling differs by platform too: a test that starts concurrent work (threads, tasks,
   connections, servers) must wait for the event it needs (a connection opened, a thread
   stopped) before it reads the result, never rely on Windows happening to run one side
   first (AF-0226 re-audit: BL-2546's 35c73029 left a multiplexing test taking a connection
   before the transfer had opened it, and Lib1565 racing its thread, red for 44 minutes).
10. Keep what you read small: every line a tool returns is re-read on every later call.
   Search before reading: Grep or rg with -l or a match count first, then -n with a small
   context, output capped (`| head -50`); never cat several files in one command. Read a
   file over about 400 lines in parts with offset and limit around what you need, and note
   what you need the first time rather than reading a large file twice. Never re-read a
   persisted tool-result file under .claude\projects; run a narrower command instead.
   Start git diff, show and log with --stat or the paths you need. Send Curl and
   Record-CurlExchange output to a file and read only the lines you need.
11. A lane cannot see CI for its own push, so never requeue a task only to wait for CI.
   A criterion about CI on Linux and macOS is met by keeping the change platform-neutral
   (the platform check above) and the fast tests green here: tick it with a Notes line
   saying so and finish the task; the shift's CI watch files a task for any failure. When you file a task,
   name files or narrow subfolders in its touches, never a whole library it only adds to,
   so it does not hold files other tasks need (AF-0329: BL-2834 was requeued once for a
   file BL-2921 held as the whole Curl.Engine.UnitLibrary and once only to wait for CI,
   $4.93 over three claims).

End your reply with exactly one line, either
FACTORY: DONE {ID} <what now works>
or
FACTORY: BLOCKED {ID} <the blocker>
'@

# Put in front of either prompt when a task runs again after the usage limit.
$ResumeNote = @'
RESUMING. The previous run of {ID} was cut off before the task was finished - the account
ran out of tokens, the API failed, or the lane was stopped and restarted. Whatever that run had done is still here: read git status, git log
and the task file before changing anything, and carry on from that work rather than
starting over.

'@

# Put in front of either prompt when a task runs once more after its run was killed at
# -TaskMinutes with the task still in Doing (AF-0042): its work stays in place, not stashed.
$OvertimeNote = @'
OVERTIME. The previous run of {ID} was killed at its time limit with the task still in
Doing. Its work is still here: read git status, git log and the task file, and do not
start over. This is the last run, {MINUTES} minutes long: do not run
Measure-CodeQuality.ps1 again if a report from the previous run is in the log or on disk.
Finish the task now if build and fast tests pass and its criteria are met, or move it to
Backlog with a -Reason saying what is left. A run killed again ends Blocked.

'@

# A lane's run: the shift has claimed the task already, and the shift - not the run -
# integrates and pushes, so parallel lanes never race each other to the shared branch.
$LanePrompt = @'
DARK FACTORY SHIFT, LANE {LANE}. Stewart is away and cannot answer. Never ask a question
and never wait for input; nobody will reply. Other lanes are working other tasks in
other checkouts at the same time; the task board guarantees their tasks touch different
projects from yours.

Run /task-run {ID}. The shift has already claimed {ID}: it is in Tasks/Doing. Skip the
claim step, do not move it to Doing again, and do not take any other task.

Rules for this unattended run, in addition to the lane rules (DarkFactory/LaneRules.md):
0. No Python, not even a one-liner: PowerShell for scripts and checks, the Edit tool
   for edits, and Record-CurlExchange.ps1 to measure real curl (extend it if needed).
   To run Curl itself, pass -Curl Curl.Console\bin\Debug\net10.0\curl.exe after
   dotnet build Curl.Console; bin\ cannot be listed, so do not look for it (AF-0483).
1. Where a choice has a sensible default, take it and record the choice and why under
   the task's Notes.
2. Design and behaviour decisions are yours: Stewart has delegated them (the lane rules,
   "Decisions"). Decide by his standing rules - match the platform's curl, measure real
   curl before pinning output, BCL only - record the decision and why in an ADR marked
   "Decided by Claude under Stewart's delegation", and carry on. Only a new package or
   a quality-threshold change goes to Blocked, with a -Reason that starts "Stewart:" and
   asks the question in one line.
3. Stay inside the projects and files the task's `touches` field names. Two additions
   never need `touches` and never send a task back: a new ADR (its own file in
   Documentation/Planning/Decisions and its row in that folder's README) and a task
   filed with the board script. If two lanes pick the same ADR number, the shift's
   rebase resolver renumbers this lane's. If the work truly needs another project or
   file, read the `touches` of every other task in Doing on the shared branch as it is
   now, not in this checkout, whose board is as old as this run:
   `git ls-tree --name-only origin/{BRANCH} Tasks/Doing/` lists them and
   `git show origin/{BRANCH}:<path>` shows one (other lanes keep that ref current).
   When none of them names it, add it to this task's `touches`, say why under Notes,
   and carry on. When one does, add it anyway, add that task's ID to this task's
   `depends-on`, and move the task to Backlog with a -Reason naming the project and that
   task; the board will not offer it again until that task is Done, and this run does not
   count toward the task's cost cap (AF-0387: BL-3204's run that found BL-3199 holding
   ManagedEasyTransfer.cs left its `depends-on` empty, so it spent a third of the cap of
   the claim that did the work).
4. If the only thing stopping the task is other work - an existing task, or one you
   file with the board script - add those IDs to its `depends-on` and move it to
   Backlog, not Blocked, with a -Reason naming them. The board starts it again once
   they are Done. Blocked is only for what needs Stewart. A run that ends that way with
   nothing to stash does not count toward the task's cost cap, so the claim that does the
   work gets a whole cap; a run that edited first is counted (AF-0404 to AF-0406). When
   you file several tasks together, give each the `depends-on` of every other one whose
   code or registry it extends, so none is offered ready before the work it builds on
   (AF-0404: BL-3412 was ready beside BL-3410, whose runtests.pl interpretation it
   extends, and its first run found only that).
   Look for that in your first few turns, before any code: read the task, the files it
   touches and the tests or data it names, and if it is too big for one run or needs work
   no task covers yet, split it or file that work and requeue it then, not after a
   long run (AF-0147: BL-1894 was requeued twice, once after 9 turns to split it and once
   after 33 turns and $1.75 to find a missing dependency, and the third claim finished it).
   Find every prerequisite in that one pass, not just the first: list all the work the task
   needs, file or name all of it, and requeue once (AF-0147 re-audit: BL-1976 was requeued
   for BL-1997 and, after its next claim, again for BL-2035).
   Do that pass before the first edit: a run that edits code and then requeues leaves its
   work in a stash, and each requeued claim spends part of the task's cost cap, so a third
   claim can be Blocked by the cap before it lands the work (AF-0160: BL-1976 was claimed
   3 times, requeued twice and Blocked at $3.25 of $3.93, its fix stashed until BL-2041).
   For an umbrella task ("remove every reference to X"), count the whole remaining work
   in that first pass - search the repository for every file that still uses X, group them
   into subtasks, file them all at once (a split, below); never file only the layer you hit first
   (AF-0241: BL-2310 was requeued at 21 turns for 4 subtasks and at 26 turns for 4 more,
   $1.60 for two runs that produced only a requeue, before a third claim finished it).
   Size the task and check its engine hooks in that same pass: before the first edit,
   name every type, method or hook the work must call, grep that each exists, and file
   a task for each that does not, with the sizing split, so one requeue names them all
   (AF-0242: BL-2394 was requeued for size, then again at 23 turns for a missing engine
   hook, $3.38 for two runs that produced only a requeue, before a third claim finished it).
   A task that ports or registers upstream cases also needs the case runner to run them:
   in that pass, run the runner on one of the cases and confirm it is not skipped as "not
   ported", and file the runner work with the split (AF-0263: BL-2529 was requeued for size
   and then for BL-2601, the runner skipping cases 520 to 537, $3.24 for two runs that
   produced only a requeue).
   A split for size never sends this task back to Backlog: file every part of its work as
   a task of its own - what you would have left here included, as the last one, depending
   on the others - each with this task's `depends-on` and its share of the acceptance
   criteria; edit the `depends-on` line of every task outside Done that names this task
   to name the new IDs instead; and move this task to Deferred with a -Reason "Split into
   <the new IDs>". A task requeued to wait on its own subtasks is claimed again for what
   is left, and a third time when that claim runs short (AF-0465 to AF-0474: BL-4124,
   BL-4132 and BL-4137 on 2026-10-29 and 2026-10-30). Split before the first edit; once
   code is written, finish it or hand it back as the deadline rule says. Run each subtask
   through the checks above (files still using X, hooks, the case runner) and file every
   missing prerequisite beside them, so no subtask finds anything new to wait on (AF-0241
   re-audit: BL-2529 was requeued for the split and then for BL-2601, $4.20 over three claims).
   The shift claimed the task only because every ID already in its `depends-on` is Done,
   so those never send it back. Before naming any other task as the blocker, check its
   folder (`ls Tasks/*/<ID>-* Tasks/Done/*/<ID>-*`): a task in Done does not block. And
   leave work a Backlog task already covers to that task rather than doing it here
   (AF-0148: BL-1936 was moved to Backlog for dependencies that were already Done, moved
   back 16 seconds later, and then did BL-1945's work, so BL-1945's run found nothing to do).
   When you must add `lane: no` because only an interactive session can do the work, write
   under Notes the exact step it must take before a lane can have the task ("Remove lane: no
   once <what> is done"). And when a task you claimed has Notes recording an earlier
   "Interactive only" requeue, check in your first turns that this step was taken: if the
   same reason still holds, requeue at once with `lane: no` restored and say what is still
   missing, without reproducing it again (AF-0161: BL-2009 was requeued as interactive only
   twice, 28 turns and $2.43 in all, because an interactive commit removed `lane: no` without
   resolving the reason the first lane gave).
5. When the task reaches Done with dotnet build clean and the fast tests green, commit
   by logical unit (Conventional Commits, including the task file). Do NOT push, pull,
   rebase, merge or switch branches: the shift integrates your commits.
6. If the task ends Blocked or back in Backlog, commit the task board change.
   Commit with it, by logical unit as in rule 5, every code change that is complete in
   itself: dotnet build clean, the fast tests green, and covered by its own tests so CI's
   100% line and branch coverage gate stays green. Say under Notes which commits carry it;
   the shift integrates them like a finished task's, after its own build and fast tests.
   Leave only unfinished or untested code uncommitted; the shift stashes it and applies it
   again when the task is next claimed (AF-0496, AF-0498: BL-4377's built and tested change
   was left to the stash, and a new task had to dig it out hours later).
7. The task must not be left in Doing.
8. This run ends the moment your reply ends, and anything still in the background - a
   command moved there, run_in_background, a Monitor, a subagent - dies with it. Run
   dotnet build and dotnet test in the foreground with the Bash tool and a timeout of up
   to 3600000 ms, and never end your reply to wait for a notification.
   Run at most one subagent at a time, and never start several in one message or
   start one in the background while another runs: the
   shift's cost cap is checked only between your own turns, so subagents running in
   parallel all spend inside one turn and carry the run past the cap (AF-0052, AF-0100, AF-0102, AF-0104).
   Run a test project once per change, not in a loop of reruns to hunt a flaky test: when
   one passes alone and fails in the run, file a task for it with the board script and
   carry on. Every turn re-reads the whole run so far, so (AF-0105 to AF-0111, AF-0172,
   AF-0190, AF-0204 to AF-0206, AF-0268, AF-0309): run the fast tests once when the change is
   done, never after every edit or in reruns; make all the edits for a change before you
   build, batch edits to one file into as few Edit calls as it allows, and build once per
   change; when a test pass fails twice on the same file, or you are measuring without
   fixing, file what is left as a task and move this one to Backlog; do the work yourself
   and start a sub-agent only when it saves more than it costs; batch independent calls
   into one turn; read each file once - whole when it is short, or with one offset and
   limit around what Grep -n found when it is long - never in repeated sed -n, head, tail
   or offset slices; decide the task's touches once; leave a failing test another live
   task names to that task; search a folder with one Select-String or grep -l, never a
   loop that starts a process per file.
9. The shift kills this run at {DEADLINE}, {MINUTES} minutes after it started, and a
   killed run ends Blocked. While other lanes build, one Measure-CodeQuality.ps1 run can
   take 30 to 45 minutes: run it once per library you changed, with -ReportPath, and
   read the report rather than running it again. If the task cannot be finished before
   the deadline, record under Notes what is done, what you measured and what is left, and
   move it to Backlog before then with a -Reason saying what is left, committing what is
   complete in itself as the Blocked-or-Backlog rule above says: the shift stashes only
   the unfinished code and the next claim starts from it. Do not file the rest as a task
   this one then waits on: that sends it back a second time, and the shift splits a task
   sent back to Backlog a second time rather than offering it a third claim (AF-0465 to
   AF-0474). The next lane then starts on the work that is left, not on measuring the
   task again (AF-0094).{TURNCAP}
   Before committing a change to a library's production code, run Measure-CodeQuality.ps1
   for that library once and fix any method past the complexity gate, so the push does not
   turn the CI quality job red (AF-0213: BL-2177 pushed four methods past the gate with no
   quality run and CI stayed red for 137 minutes). Run it for every such change, however
   small: adding no branch, or leaving coverage to another task, is no reason to skip it
   (AF-0330, AF-0331: BL-2856 and BL-3022 skipped it and the quality job stayed red for 48
   and 46 minutes). Never run a search over the whole
   drive, such as `find /` or `Get-ChildItem C:\ -Recurse`, and never leave a command
   running when you finish: the lane stays held until it ends (AF-0213: a `find / -maxdepth 6`
   kept BL-2180's lane busy for 22 minutes after its run was done).
   Lanes test only on Windows, so before a task adds or holds an upstream case on the
   conformance ratchet, or adds or changes any test that touches the file system, a path, an
   OS error or a certificate, check that the output does not depend on the platform (path
   separators, drive letters, POSIX file and directory semantics, OS error text,
   certificates); where it does, pin each platform's answer in its own test (an
   OSCondition attribute) or leave the case off the ratchet, so CI on Linux and macOS does
   not go red (AF-0226: BL-2219 held 108 cases on the ratchet after a Windows-only run and
   test203 failed on Linux and macOS, leaving CI red for 31 minutes; its re-audit: BL-2328's
   1c068661 broke OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory, red for 36 minutes).
   Scheduling differs by platform too: a test that starts concurrent work (threads, tasks,
   connections, servers) must wait for the event it needs (a connection opened, a thread
   stopped) before it reads the result, never rely on Windows happening to run one side
   first (AF-0226 re-audit: BL-2546's 35c73029 left a multiplexing test taking a connection
   before the transfer had opened it, and Lib1565 racing its thread, red for 44 minutes).
10. Keep what you read small: every line a tool returns is re-read on every later call.
   Search before reading: Grep or rg with -l or a match count first, then -n with a small
   context, output capped (`| head -50`); never cat several files in one command. Read a
   file over about 400 lines in parts with offset and limit around what you need, and note
   what you need the first time rather than reading a large file twice. Never re-read a
   persisted tool-result file under .claude\projects; run a narrower command instead.
   Start git diff, show and log with --stat or the paths you need. Send Curl and
   Record-CurlExchange output to a file and read only the lines you need.
11. A lane cannot see CI for its own push, so never requeue a task only to wait for CI.
   A criterion about CI on Linux and macOS is met by keeping the change platform-neutral
   (the platform check above) and the fast tests green here: tick it with a Notes line
   saying so and finish the task; the shift's CI watch files a task for any failure. When you file a task,
   name files or narrow subfolders in its touches, never a whole library it only adds to,
   so it does not hold files other tasks need (AF-0329: BL-2834 was requeued once for a
   file BL-2921 held as the whole Curl.Engine.UnitLibrary and once only to wait for CI,
   $4.93 over three claims).

End your reply with exactly one line, either
FACTORY: DONE {ID} <what now works>
or
FACTORY: BLOCKED {ID} <the blocker>
'@

$ResolvePrompt = @'
DARK FACTORY SHIFT, LANE {LANE}. Stewart is away; never ask a question.

This checkout is in the middle of `git rebase origin/{BRANCH}`: this lane's commits for
task {ID} are being replayed on top of work other lanes pushed meanwhile, and git
stopped on conflicts. Resolve them:

1. `git status` to see the conflicted files. For each, keep BOTH sides' intent: the
   other lanes' work is already shared and must survive, and this lane's change must
   still do what its commit says. Never resolve by discarding one side wholesale.
2. Files under Tasks/: a task file that one side moved and the other edited keeps the
   move and the edits. Curl.slnx and other lists: keep every entry from both sides.
   An ADR number both sides used stays with the shared side's ADR; this lane's takes
   the next free number in its file name, heading, the Decisions README row and every
   reference to it.
3. `git add` the resolved files, then `git -c core.editor=true rebase --continue`.
   Repeat until the rebase finishes.
4. Run `dotnet build` and `dotnet test --filter "TestCategory!=Integration"`; fix what
   the merge broke, and commit the fix.
5. Never run git rebase --abort, git reset, git push, or git checkout of another branch.
6. This run ends the moment your reply ends, and anything still in the background dies
   with it. Run dotnet build and dotnet test in the foreground with the Bash tool and a
   timeout of up to 3600000 ms, and never end your reply to wait for a notification.
7. Keep what you read small: Grep with -l or -n and a small context, not cat; Read a file over
   about 400 lines in parts; never re-read a persisted tool-result file; git diff --stat first.

End your reply with exactly one line, either
FACTORY: RESOLVED {ID}
or
FACTORY: UNRESOLVED {ID} <why>
'@

# A finished task whose build or fast tests went red only once the rebase put other
# lanes' work under it is repaired there and then, by a run that sees the red tree, rather
# than parked for a later run that starts from a green one. BL-1467 was claimed three
# times: its second run rebuilt its own branch, found it green and changed nothing, and
# was parked again for the same break (AF-0051).
$RepairPrompt = @'
DARK FACTORY SHIFT, LANE {LANE}. Stewart is away; never ask a question.

Task {ID} is finished in this checkout and its commits have just been rebased onto
origin/{BRANCH}, where other lanes pushed work meanwhile. The rebase went cleanly, but
on the combined tree {RED}: this lane's change and theirs do not fit together (a type,
member or test helper renamed, moved or deleted on one side, for instance). Fix it here:

1. Run `dotnet build`, and `dotnet test --filter "TestCategory!=Integration"` once it
   builds, to see what is red. It is red only on this combined tree, so do not judge
   the task by its own commits alone.
2. Fix the code so both sides' work survives: the other lanes' work is already shared,
   and task {ID} must still do what its commits say. Adapt this lane's code to theirs;
   change theirs only where nothing else fits.
3. Build and run the fast tests again until both are green, then commit the fix
   (Conventional Commits, naming {ID}). Do not move the task: it stays in Done.
4. Never run git push, git pull, git fetch, git rebase, git reset, or git checkout of
   another branch: the shift integrates your commit.
5. This run ends the moment your reply ends, and anything still in the background dies
   with it. Run dotnet build and dotnet test in the foreground with the Bash tool and a
   timeout of up to 3600000 ms, and never end your reply to wait for a notification.
   It is killed at {DEADLINE}.
6. Keep what you read small: Grep with -l or -n and a small context, not cat; Read a file over
   about 400 lines in parts; never re-read a persisted tool-result file; git diff --stat first.

End your reply with exactly one line, either
FACTORY: REPAIRED {ID}
or
FACTORY: UNREPAIRED {ID} <why>
'@

$script:ToolLabels = @{}

# Commands only Stewart may authorize (CLAUDE.md). --dangerously-skip-permissions does
# not enforce "ask" rules, so each run gets these as a hard --disallowedTools deny.
$Forbidden = @(
    'git push --force', 'git push -f', 'git push --force-with-lease',
    'git push origin master', 'git push origin HEAD:master', 'git merge',
    'git tag', 'git branch -D', 'git branch -d', 'git reset --hard',
    'dotnet add package', 'dotnet remove package',
    'gh pr merge', 'gh release', 'gh repo',
    # No Python (CLAUDE.md): PowerShell, the Edit tool and Record-CurlExchange.ps1 instead.
    'python', 'python3', 'py'
)
# A lane's run also never touches the remote or the branch: the shift owns both.
$LaneForbidden = $Forbidden + @('git push', 'git pull', 'git fetch', 'git rebase', 'git checkout', 'git switch', 'git worktree', 'git stash')
# The split run (Invoke-TaskSplit) is told to run no git stash subcommand (the shift stores
# the stash itself), and the shift commits and pushes what it changes.
$SplitForbidden = $Forbidden + @('git push', 'git pull', 'git fetch', 'git rebase', 'git checkout', 'git switch', 'git worktree', 'git commit',
    'git stash push', 'git stash save', 'git stash apply', 'git stash pop', 'git stash drop', 'git stash clear', 'git stash create', 'git stash branch', 'git stash store')

# The split run's prompt (BL-3091): a task over its cost cap, stopped at its turn cap, or sent
# back to Backlog a second time ({CAUSE}) is split into smaller tasks.
$SplitPrompt = @'
DARK FACTORY TASK SPLIT. Stewart is away and cannot answer. Never ask a question and never
wait for input; nobody will reply. Write no code and run no build or test.

{ID} is {CAUSE}: {WHY}. {STASH}
Split it into smaller tasks, each sized for one run, exactly as an interactive session would:
1. Read Tasks/*/{ID}-*.md and .claude/skills/task-board/SKILL.md. Decide the smaller tasks
   that together meet every acceptance criterion of {ID}, each with its own criteria and
   file-level touches taken from {ID}'s touches.
2. File each one with `powershell` (`pwsh` on Linux and macOS) `-NoProfile -ExecutionPolicy Bypass -File
   .claude/skills/task-board/task-board.ps1 new` (its depends-on carries {ID}'s, plus the
   earlier subtasks it needs), and say under its Notes that it was split from {ID}.
3. For each new task that continues the stashed work, add the line "Continues the stashed work
   of {ID}." under its Notes; the shift then stores the stash under that task's ID
   itself. Run no git stash subcommand. Skip this step when there is no stash.
4. Move {ID} to Deferred with the board script's move -To Deferred and a -Reason naming the
   new task IDs.
5. Repoint depends-on: every task outside Done whose depends-on names {ID} names the new
   task IDs instead (edit only that front-matter line, with the Edit tool).
Touch nothing else: no file outside Tasks/, no commit and no push; the shift commits them.

End your reply with exactly one line: FACTORY: SPLIT {ID} <the new task IDs>
'@
# The conflict resolver needs `git rebase --continue`, and nothing that throws work away.
$ResolveForbidden = $Forbidden + @('git push', 'git pull', 'git rebase --abort', 'git rebase --skip', 'git checkout', 'git switch', 'git worktree', 'git stash')
# The run that resolves a rebase conflict for task -Id, which Invoke-Integrate makes with the
# integrate lock released. A variable so -TestIntegrateLock can stand in a resolver of its own.
$script:ResolveRebaseConflict = { param([string]$Id) Invoke-TaskRun -Id $Id -Text $ResolvePrompt -Deny $ResolveForbidden -Suffix '-resolve' -Minutes 45 | Out-Null }

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
