<#
.SYNOPSIS
    Runs an unattended "dark factory" shift over the Curl task board.

.DESCRIPTION
    Takes the next ready task, hands it to a headless Claude Code run (/task-run <ID>)
    that must never ask a question, and repeats until nothing is ready, the shift's time
    is up, or -MaxTasks is reached. Work that needs Stewart ends in Blocked with the
    question in its Reason, and the shift moves on to the next ready task.

    The terminal shows a terse, timestamped trace. The same trace goes to
    ..\<repo>.logs\DarkFactory-<stamp>.log (beside the checkout, like the lanes, so it is
    never in the working tree), and each run's raw stream to <repo>.logs\<ID>-<stamp>.jsonl.
    A shift moves any logs\ left inside the checkout by an older version out there.

    When the shift ends with anything waiting on Stewart - a Blocked task, a Backlog task
    assigned to him, a run that stalled - it fills the screen with a flashing ASCII banner
    and raises an alarm that escalates until a key is pressed:

      0-2 min    chime and "Stewart, the Curl dark factory needs your input" every 30 s
      2-5 min    chime and the waiting tasks read aloud every 15 s
      5-15 min   siren and slower speech every 10 s; volume raised to -AlarmMaxVolume, unmuted
      15 min+    siren and speech every 5 s

    Speech is Windows' built-in System.Speech. The key press restores volume and mute.

    While %LOCALAPPDATA%\Curl\audio-off exists (Stewart's audio switch, BL-1317), the
    alarm, the out-of-tokens notices and the audit notice behave as under -QuietAlarm:
    banner and notices on screen, no chime, siren or speech, and the volume and mute are
    never touched. The file is checked at each sound, so creating or deleting it reaches a
    shift that is already running, and -QuietAlarm is then not needed (BL-1456).
    -TestAudioOff proves it without playing a sound.

    KEEPING THE BOARD MOVING

    Runs decide design and behaviour questions themselves (Stewart delegated them; see
    CLAUDE.md "Decisions") and block only for a new package or a threshold change. A
    task that waits on other tasks goes back to Backlog with them in `depends-on`, and a
    run that needs a project outside `touches` widens it, checking the overlap against
    the shared branch's Doing rather than its own stale copy; a new ADR or a newly filed
    task never needs `touches` and never sends a task back (BL-1069). Before each claim the shift
    also requeues any Blocked task whose reason names only tasks that are now Done. A task
    a lane sends back to Backlog (requeued or parked) is not claimed again by any lane of
    the same shift (lanes-<stamp>\requeued.txt); the next shift tries it afresh (AF-0072).

    COST CAP

    Each headless run is capped with claude's --max-budget-usd at 2.7 times the median cost
    of the newest 40 task runs in the log folder, never below $2 and never above
    -TaskBudgetUsd (default $6; AF-0004, AF-0033, ADR-0288, ADR-0407). 2.7 rather than 3
    because claude checks the cap between turns, so a run can end one turn's cost above
    it; with fewer than 10 logged runs the cap is -TaskBudgetUsd itself. A turn that waits
    on subagents spends all of theirs before the next check, so the prompt allows one
    subagent at a time (AF-0052: five in parallel took BL-1458 $1.77 past its cap). A task run that
    reaches the cap stops; like a timed-out run, its partial work is stashed and the task
    goes to Blocked for Stewart, since it is too big for one run and wants splitting.
    -TaskBudgetUsd 0 removes the cap. -TestTaskBudget proves the arithmetic.

    THE MODEL PER TASK

    Each run uses the Claude model its task calls for (BL-1705), chosen in this order:
      1. -Model other than auto (its default; a CLAUDE_MODEL environment value counts as
         given) forces that model for every task, as before. The shift start, the
         -Continuous hand-over and -Restart forward -Model unchanged, so auto stays auto.
      2. Otherwise the task's own front-matter model: haiku | sonnet | opus wins. Any other
         value is ignored with a "model" trace warning and the rule below applies.
      3. Otherwise opus when the task's touches name a hand-built security or crypto
         library (Curl.Cryptography, Curl.Tls, Curl.Quic, Curl.Kerberos, Curl.Ntlm,
         Curl.Protocol.Ssh, or their .UnitTests or .IntegrationTests twin), its pipeline is
         feature or protocol, its Log shows an earlier claim that came back ("Doing ->
         Backlog" or "Doing -> Blocked": a requeue, a park, a hand-back or a failed run),
         or it is a High "Fix CI failure ..." or "Fix flaky CI test ..." task; sonnet for
         everything else. The rule never picks haiku.
    The resolver's, overtime and resumed runs use their task's model; the --max-turns 1
    probes use sonnet under auto. The claim and end trace lines say "[model <m>: <why>]",
    the heartbeat and status.json lane objects carry model and modelWhy, each run log's
    first line is {"type":"factory","model":...,"why":...}, and the lane SUMMARY lines and
    the shift's closing trace print runs, tasks and US dollars per model.
    -TestModelChoice proves the rule.

    OUT OF TOKENS

    When the account's usage limit refuses a run, that is not a stall. The task stays
    claimed, the shift waits for the new session and then runs the same task again,
    telling it to carry on from the partial work. Stewart is told three times, each
    with a coloured notice, a chime and one spoken sentence (screen only under
    -QuietAlarm or the audio-off file), never the escalating alarm:

      at once              out of tokens, when the new session starts and how long until then
      -LimitWarnSeconds    before the reset: the new session is about to start
      on resuming          the new session has started, and which task it resumed

    Lanes survive being stopped. Each lane records its process and the task it holds in
    <repo>.logs\lanes-<stamp>\; the coordinator restarts a lane whose process has died (five tries
    each) and the lane resumes its task from the work in its worktree, first integrating
    any finished commits it had not pushed. A new shift adopts a stopped shift's lanes the
    same way instead of refusing to start over their tasks in Doing. A task in Doing that
    no lane holds (BL-1071) is adopted by the one lane worktree with uncommitted work, or,
    when none has any and a parked factory/<ID>-lane-* branch holds its work, returned to
    Backlog; anything else refuses to start, and every refusal raises the alarm, so the
    factory never stops without saying why. A coordinator whose clean checkout is only
    behind origin/<branch> fast-forwards it and starts (BL-1141); one that is ahead of it
    or has diverged from it refuses. A run that dies on
    the API without naming the limit waits until a one-word probe is answered, then runs
    again (three times at most).

    If the limit is lifted early, the shift carries on at once: every -LimitProbeMinutes
    the coordinator (or lone runner) asks Claude for one word, and an answer wakes every
    waiting runner. After resetting the limit by hand, `RunDarkFactory.cmd -Wake` does
    the same without waiting for the next probe.

    Better still, a shift ends before the tokens run out. Every run logs how much of the
    5-hour and the weekly usage window is used; once the 5-hour window reaches
    -StopAtUsage (85%) or the weekly one -StopAtWeeklyUsage (97%) no lane claims another
    task, the tasks already running finish, integrate and push, and the shift ends clean. The next shift (-Continuous) starts at once and waits for
    the 5-hour window to reset before starting its lanes; a used-up weekly window is
    waited out the same way, with a notice rather than the alarm, however long it is.

    To restart a running shift - to pick up a change to this script, say - run
    `RunDarkFactory.cmd -Restart`. It stops the coordinator first, so nothing restarts the
    lanes, then each lane as soon as that lane is neither claiming nor integrating, and
    starts a new shift with the old one's arguments, which adopts the stopped lanes and
    their tasks (BL-895). Only this checkout's shift is touched.

    A shift keeps this checkout on its branch: if something switched it (Visual Studio did,
    once), the coordinator switches it back before its shift-end pull, and -Continuous
    hands the next shift -ShiftBranch so it does the same before it starts (BL-809).

    At the end of every shift the coordinator merges the branch into master through a
    pull request, by Stewart's standing permission - only when the CI workflow passed on
    Windows, Linux and macOS for the exact commit being merged.

    CI WATCH

    Lanes test only on Windows, so a lane shift's coordinator watches CI for them (BL-987).
    Every -HeartbeatMinutes (3 when publishing is off) it reads the newest finished CI runs
    on the shift's branch that passed or failed - cancelled ones prove nothing, and with
    several lanes pushing most are cancelled - and reads each failed run's failures once
    from `gh run view <id> --log-failed`: every "Failed <TestName>" line, or every compiler
    "error" line when the build broke. Over the last six such runs, newest first, a failure
    is a regression when it fails in the newest run and broke the build, failed on two
    platforms, failed in the run before too, or found no run to confirm it within 30
    minutes; it is flaky when it failed once with a passing run on each side, or failed
    again after passing. For each one it files a High task with task-board.ps1 new -
    "Fix CI failure <test> on Linux and macOS", "Fix flaky CI test <test> that failed once
    on Linux" or "Fix CI build error CS1002 in X.cs on macOS" - naming the platforms, the
    run and its link, the first failing commit and the error message, with `touches` set to
    the test project and its library. It files nothing while a Backlog, Doing or Blocked
    task names the test, or when a finished task names it and the failing run predates that
    task's last commit. The task is committed in a detached worktree,
    <repo>.lanes\ci-watch, and pushed straight to the shift's branch, so lanes claim it on
    their next claim; each filing is traced "ci filed: ..." and whispered through
    .claude\hooks\whisper-milestone.ps1. Each run it examines is traced "ci run <id> ...":
    green, or each failure and whether it was filed, covered by a task, or is waiting for a
    run to confirm it - once per run, and again when that changes. The watch also runs while
    a coordinator waits for a fresh session before its shift starts, and once more at the
    end of a shift, after the merge has waited for CI on the last commit; that last look
    files a one-platform failure at once instead of waiting 30 minutes (BL-1031).

    The audit guard (BL-998) fails the `audit-guard` job with "Audit guard: <path> changed
    on work/dark-factory ..." when the factory changed an audit path or a guard. The watch
    files that from one failed run, as for a broken build (BL-999): a High, direct,
    interactive-only task (task-board.ps1 new -NoLane, the one way a factory process may
    file an audit-path task, BL-996) titled "Revert the dark factory's change to <path>",
    with `touches` set to the path and its Context naming the key "audit guard <path>", so a
    later run finds it. No lane can take it - the hook refuses a lane that path (BL-997) -
    so the shift's end report lists every open one as "AUDIT needs an interactive session"
    and the alarm sounds for it, as for work waiting on Stewart.
    -TestCiWatch proves the log reading, the verdicts, the run tracing and the task text on
    recorded lines. A single-runner shift (-Lanes 1) does not watch CI.

    The reset time comes from the run's rate_limit_event. Time spent waiting is added
    to the shift, so -Hours is always working time. With lanes, every lane waits on its
    own and the coordinator makes the announcements, once for all of them.

    PARALLEL LANES (-Lanes 2 or more, 16 at most)

    The shift runs that many lanes at once, each an independent task runner in its own
    console window and its own git worktree (..\<repo>.lanes\lane-<n>, on a local
    branch factory/lane-<n>), all feeding the branch this checkout is on:

      claim      A lane takes the next task the board offers - one whose `touches` do
                 not overlap any task in Doing - moves it to Doing, commits and pushes
                 that move. The push is the lock: if another lane got there first, the
                 push is refused, traced as "race", and the lane picks again. Only
                 a pushed claim is traced as "claim".
      offline    Before a claim or an integration attempt the lane checks that
                 origin answers, and waits up to an hour for it when it does not, so
                 a GitHub outage never parks finished work or counts as a lost race
                 (AF-0025, BL-1281).
      run        /task-run in the lane's worktree. The run commits but never pushes.
      integrate  The lane rebases its commits onto the shared branch, rebuilds, runs
                 the fast tests and pushes. Red fast tests are run once more, with the
                 failing test names traced as "flaky?"; only red twice counts (BL-898), and not even
                 then when the projects that failed pass when run alone (AF-0092). A conflict gets one headless run to resolve
                 it, and a finished task whose build or tests go red on the rebased
                 tree gets one headless repair run there, still holding the lock,
                 traced as "repair" (AF-0051). Work that still will not integrate is pushed to its own branch,
                 factory/<ID>-lane-<n>-<stamp>, and the task goes back to Backlog on the
                 shared branch, retried for several minutes; a park whose move is never
                 pushed is in the lane's summary and the end-of-shift report (BL-1071).

    Claims and integrations hold ..\<repo>.lanes\integrate.lock, so they happen one at
    a time; runs overlap freely. This window coordinates: it starts the lanes, waits
    for them, pulls the result and raises the alarm once for all of them. Each lane
    traces to <repo>.logs\DarkFactory-<stamp>-L<n>.log beside this checkout.

    The coordinator can change the lane set mid-shift (ADR-0130 item 6): it adds a lane
    by starting the lowest free lane number, up to 16, and retires one by writing
    lane-<n>.retire beside the lane's state. A lane asked to retire is never stopped
    mid-task: it finishes and integrates the task it holds, and stops with "retired"
    before its next claim. Restarts cover the active lanes, and the end-of-shift report
    covers every lane started, retired ones included. A retire goes to the highest-numbered
    lane that holds no task, when there is one.

    A fixed lane count (-Lanes N, N > 1) is capped by the board's capacity too (BL-1374,
    AF-0032): every 5 minutes, except while waiting for tokens, the coordinator reads
    task-board.ps1 capacity as -Lanes Auto does and retires idle lanes down to it, so no
    lane polls "every ready task overlaps one in Doing" for hours; when the capacity rises
    again it adds one lane per step, never above N. Each change traces
    "lanes a -> b (reason)", e.g. "lanes 9 -> 4 (4 ready tasks can run at once)". The shift
    also starts no more lanes than that capacity, nor fewer than the lanes it adopts
    (BL-1384, AF-0041), tracing e.g. "lanes: starting at 4 (-Lanes 9, capped at 4: 4 ready
    tasks can run at once)", so lanes do not open the shift waiting on overlapping touches.

    AUTO LANES (-Lanes Auto)

    The shift sizes itself (ADR-0130). It never needs to know the Claude plan: the usage
    windows are read as the share of the plan used, so the burn rate it meters - points per
    hour per busy lane - already scales with whatever plan Stewart is on.

      start     The machine cap comes from <repo>.lanes\machine-lanes.json; the machine
                probe (-ProbeMachine, below) runs first when that file is missing,
                incomplete or from other hardware. The cold start is the lane count
                <repo>.lanes\auto-lanes.json saved, raised to -MinStartLanes (default 16),
                capped by the ceilings, so by default a shift starts at its ceiling and
                dials down (BL-823). -MinStartLanes is a start, not a floor.
      step      Every 15 minutes, except while waiting for tokens, it samples usage and
                paces to the 5-hour window (and, with -WeeklyPace, the weekly one): the
                lanes that would spend each window up to -StopAtUsage or -StopAtWeeklyUsage
                just as it resets. The lower target binds. Without -WeeklyPace the weekly
                window only stops claims at -StopAtWeeklyUsage: pacing it finishes no more
                work, and loses what an idle factory leaves at the reset (BL-806).
      ceilings  task-board.ps1 capacity (read in a detached worktree, <repo>.lanes\auto-board,
                so this checkout is only pulled at shift end), the machine cap and -MaxLanes.
      change    Up at most one lane per step, so the meter samples each count. Down
                straight to max(1, floor(desired + 0.25)) in one step, the highest lane
                numbers first; a retiring lane finishes and integrates its task first. A
                pace target must be low two steps running before lanes retire ("low once"
                holds); a ceiling retires at once (BL-823). Each step traces
                "lanes a -> b (reason)", e.g. "lanes 3 -> 4 (5-hour pace allows 4.9)". It
                holds while the tokens are low or the shift's time is up.

    auto-lanes.json is saved on each change and at shift end, and -Continuous hands on
    -Lanes Auto. -AutoLanesReport prints one step's reading without starting a lane, and exits.

    LIVE BOARD

    Every runner keeps a heartbeat file, <repo>.logs\lanes-<stamp>\lane-<n>.heartbeat.json
    (the single runner is lane 0): one lane object of ADR-0129's status.json schema 1 -
    the task it holds, its title, the phase (starting, claim, run, integrate, wait, tokens,
    finished), the last tool step and when the task started. It is rewritten on every phase
    change, on each new tool step and at least every 60 seconds during a run or a wait,
    through a temporary file and a rename, so a reader never sees half of one. Lanes never
    push it. Every -HeartbeatMinutes (default 3; 0 turns publishing off) the coordinator
    merges the shift's heartbeat files into one status.json, sorted by lane, and
    force-pushes it to the board branch as a single parentless commit built with plumbing
    (hash-object, mktree, commit-tree), so its checkout's working tree and index never
    change. It publishes once more at shift end with "state": "ended" and every lane
    finished. A single-runner shift publishes its own lane 0 the same way, from where it
    writes its heartbeat. The board branch is the only one this script force-pushes
    (Stewart, 2026-09-28); a failed push is traced once and never stops the shift. The
    live board page reads it. -TestHeartbeat rehearses the files, the merge and the
    commit, without pushing.

    MACHINE PROBE (-ProbeMachine)

    Measures how many lanes this PC sustains, since parallel builds are a shift's CPU
    and memory peak. After one untimed warm-up build it runs k = 1, 2, ... concurrent
    `dotnet build --no-incremental` of this checkout, each into its own artifacts folder
    under <repo>.lanes\probe, and records each step's wall time and lowest free memory.
    A step passes when every build succeeds and free memory stays at least 20% of RAM;
    wall time is recorded but decides nothing (BL-812). The cap is the largest passing k (at
    least 1), found at the first failing step, 16, or -ProbeMaxLanes. The result goes to
    <repo>.lanes\machine-lanes.json (marked incomplete when -ProbeMaxLanes cut it short)
    and the probe folder is deleted. Run it only when no shift is building.

    AUDIT CADENCE (BL-1022)

    The audit office (ADR-0267) audits the factory between shifts, before each roadmap-
    milestone merge and after changes to this script. At the end of a shift, before the merge
    to master, the coordinator runs master's copy of Audit/Tools/Test-AuditDue.ps1 - never
    this branch's - with -Json; when master has no copy yet it skips this silently. When an
    audit is due it says "Audit due: <reasons>" in a notice (a chime and one sentence, not
    the alarm). A milestone:<N> reason also holds the merge, which reads "not merged: audit
    due before the Milestone <N> merge (run Audit\RunAudit.cmd)"; other reasons do not. At
    the start of a shift, while Audit\RunAudit.ps1 runs, the coordinator refuses with "An
    audit is running; shifts start between audits." - or with -Continuous waits and looks
    again every 5 minutes. -TestAuditCadence proves both.

    LANE MARKER (CURL_DARK_FACTORY_LANE)

    Every process a shift runs work in sets the environment variable CURL_DARK_FACTORY_LANE
    once, at shift start: the lane's number in a lane (-Lane N), 0 in the coordinator or a
    single-runner shift. Children inherit it - every claude -p run, every task-board.ps1
    call, every git and dotnet - so the audit guards can tell the factory from an
    interactive session: task-board.ps1 refuses audit-path and interactive-only work while
    it is set (BL-996), and the PreToolUse hook guard-audit-paths.ps1 blocks lanes from audit
    paths (BL-997). A -NewTab or -Restart launcher, which only starts another shift and
    exits, is not marked; nor is a -Test* self-test, so running one from an interactive
    session never marks that session's children. A lane cannot unmark itself: hooks are
    started by the Claude Code process, whose environment the lane's Bash tool cannot
    change. -TestLaneMarker proves the values and that a claude -p run inherits them.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -Hours 4 -MaxTasks 3
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -Lanes Auto -Continuous
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -Lanes 4
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAlarm
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAlarm -AlarmScale 0.1
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestOutOfTokens
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -AutoLanesReport
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAutoLanes
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -ProbeMachine
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestMachineProbe
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestHeartbeat
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestCiWatch
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestTaskIds
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestTaskBudget
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestModelChoice
#>
[CmdletBinding()]
param(
    # Wall-clock length of the shift. No new task is claimed after it runs out.
    [double]$Hours = 8,
    # Stop after this many tasks. 0 means no limit.
    [int]$MaxTasks = 0,
    # A single task run is killed after this long; a task still in Doing then gets one
    # overtime run of a quarter of this (at least 30 min) and is Blocked if killed again.
    [int]$TaskMinutes = 120,
    # The most a single headless run may cost, in US dollars (claude's
    # --max-budget-usd); the cap is 2.7 times the median recent run up to this, and a run
    # that reaches it has its task filed as Blocked. 0 means no cap (ADR-0288, ADR-0407).
    [double]$TaskBudgetUsd = 6,
    # Model for every run, or auto (the default) to choose one per task; see THE MODEL PER
    # TASK above. "opus" is the moving alias RunClaude.cmd also uses.
    [string]$Model = $(if ($env:CLAUDE_MODEL) { $env:CLAUDE_MODEL } else { 'auto' }),
    # Show the attention banner and exit, to check it can be seen across the room.
    [switch]$TestAlarm,
    # Multiplies the alarm's stage timings; 0.1 runs the whole ladder in about 90 seconds.
    [double]$AlarmScale = 1,
    # Installed Windows voice to speak with, e.g. "Microsoft Zira Desktop". Default voice if empty.
    [string]$AlarmVoice = '',
    # Master volume, in percent, that stage 3 raises the speakers to (and unmutes).
    [ValidateRange(0, 100)][int]$AlarmMaxVolume = 100,
    # Banner only: no chime, siren, speech or volume change. For nights.
    [switch]$QuietAlarm,
    # Rehearse the out-of-tokens notices with a pretend reset 90 seconds away, and exit.
    [switch]$TestOutOfTokens,
    # Check the -Lanes Auto burn-rate, pace and lane-step logic on recorded readings, and exit.
    [switch]$TestAutoLanes,
    # Measure how many concurrent solution builds this PC sustains, write the cap to
    # <repo>.lanes\machine-lanes.json, and exit. Never while a shift is running.
    [switch]$ProbeMachine,
    # The most concurrent builds -ProbeMachine tries; below 16 its file may come out incomplete.
    [ValidateRange(1, 16)][int]$ProbeMaxLanes = 16,
    # Check the -ProbeMachine pass and cap rule on recorded steps, and exit.
    [switch]$TestMachineProbe,
    # Walk lane 1's heartbeat file through its phases in a temporary log root, print each,
    # then merge three made-up lanes into status.json, print it and the board commit built
    # from it (never pushed), and exit.
    [switch]$TestHeartbeat,
    # Prove Restore-ShiftBranch on a throwaway repository in a temporary folder, and exit.
    [switch]$TestShiftBranch,
    # Stop this checkout's running shift and start a new one with the same arguments: the
    # coordinator first, then each lane as soon as it is neither claiming nor integrating.
    [switch]$Restart,
    # Prove which lanes -Restart may stop from their heartbeat phases, and exit.
    [switch]$TestRestart,
    # Prove how failing test names are read from dotnet test output, and exit.
    [switch]$TestFlakyTests,
    # Prove how the CI watch reads failures from a failed run's log and which it files, and exit.
    [switch]$TestCiWatch,
    # Prove that task IDs of three digits or more (BL-992, BL-1003) are read from next output,
    # -Reason text, status lines and file names, and that a wait logs next's reason line
    # rather than a WARNING printed before it, and exit.
    [switch]$TestTaskIds,
    # Prove the cost cap follows 2.7 times the median recent run cost (AF-0033), and exit.
    [switch]$TestTaskBudget,
    # Prove the choice of model per task (BL-1705), and exit.
    [switch]$TestModelChoice,
    # Prove the audit cadence: the shift-end audit check and the start refusal (BL-1022), and exit.
    [switch]$TestAuditCadence,
    # Prove the audio-off file silences the alarm, chimes and spoken notices, and that they
    # sound as before without it, playing nothing (BL-1456), and exit.
    [switch]$TestAudioOff,
    # Prove the CURL_DARK_FACTORY_LANE marker's value for a lane, a coordinator and a
    # launcher, and that a claude -p run inherits it, and exit.
    [switch]$TestLaneMarker,
    # Prove, on a throwaway repository, that a park pushes its move to Backlog or reports
    # that it could not, how shift start settles a task in Doing held by no lane, and that a
    # refused start raises the alarm (BL-1071), and exit.
    [switch]$TestPark,
    # Prove, on a throwaway repository, that a claim applies its task's own shift stash and
    # no other, and that a stash that no longer applies leaves the worktree clean and the
    # run its hash (AF-0091), and exit.
    [switch]$TestTaskStash,
    # How often, in minutes, the lanes' heartbeats are published as status.json on the
    # force-pushed board branch, plus once at shift end. 0 = never publish.
    [ValidateRange(0, 60)][int]$HeartbeatMinutes = 3,
    # How long before the usage limit resets to say the new session is about to start.
    [ValidateRange(0, 3600)][int]$LimitWarnSeconds = 60,
    # While waiting for tokens, how often to check whether the limit was lifted early. 0 = never.
    [ValidateRange(0, 600)][int]$LimitProbeMinutes = 10,
    # Wake a shift that is waiting for tokens (after resetting the limit), and exit.
    [switch]$Wake,
    # With lanes: when a shift ends and the board still has ready work, start the next one.
    [switch]$Continuous,
    # Stop claiming new tasks once this share of the 5-hour usage window is used, so the
    # tasks already running finish, integrate and push before the tokens run out. The next
    # shift waits for a fresh 5-hour window. 1 means never stop early.
    [ValidateRange(0.1, 1)][double]$StopAtUsage = 0.85,
    # The same for the weekly usage window, which is days from resetting, so the shift
    # runs it closer to empty (Stewart, 2026-09-28); a used-up weekly window raises the alarm.
    [ValidateRange(0.1, 1)][double]$StopAtWeeklyUsage = 0.97,
    # How many tasks run at once, each in its own worktree and window. 1 is the classic
    # single-runner shift in this checkout. Auto sizes the shift itself (ADR-0130): every
    # 15 minutes it adds at most one lane or retires straight down to the pace, paced to the
    # usage windows and capped by the board, the machine and -MaxLanes.
    [ValidatePattern('^(?i:auto|[1-9]|1[0-6])$')][string]$Lanes = '1',
    # -Lanes Auto's lane maximum (Stewart, 2026-10-01: "restart the dark factory with a
    # default of 9 lanes"; it was 6 from 2026-09-28).
    [ValidateRange(1, 16)][int]$MaxLanes = 9,
    # The fewest lanes a -Lanes Auto shift starts with. 16 starts at the ceiling and dials
    # down (Stewart, 2026-09-28: "Just start aggressively and dial down", BL-823). A start,
    # not a floor: the ceilings still cap it, and Auto still retires below it when pace demands.
    [ValidateRange(1, 16)][int]$MinStartLanes = 16,
    # -Lanes Auto also paces to the weekly window, spreading its budget evenly until the
    # reset, instead of burning at the 5-hour pace and stopping at -StopAtWeeklyUsage.
    [switch]$WeeklyPace,
    # Print what -Lanes Auto would do now - machine cap, board capacity, burn rates, pace
    # targets, start count and the step's log line - without starting a lane, and exit.
    [switch]$AutoLanesReport,
    # Start the shift somewhere of its own and return at once: a new herdr tab when this
    # is running inside herdr, otherwise a new console window. How Claude starts a shift.
    [switch]$NewTab,

    # The branch the previous shift ran on, handed over by -Continuous: a shift switches
    # this checkout back to it first, in case something (Visual Studio, once) moved it.
    [string]$ShiftBranch = '',

    # The rest are set by the coordinator when it starts a lane; not for direct use.
    [int]$Lane = 0,
    [string]$Branch = '',
    [string]$LogRoot = '',
    [string]$ShiftStamp = '',
    # The lane's worktree. Lanes run the coordinator's copy of this script, so a lane adopted
    # with its worktree left as it was still runs the current code.
    [string]$LaneDir = ''
)

# Continue, not Stop: native stderr from git or dotnet must never kill an unattended shift.
$ErrorActionPreference = 'Continue'

# -Lanes is Auto or a number; $LaneCount is the number, which Auto sets at shift start and
# changes step by step.
$AutoLanes = $Lanes -eq 'auto'
$LaneCount = if ($AutoLanes) { 1 } else { [int]$Lanes }

$Root = if ($LaneDir) { $LaneDir.Trim('"') } else { $PSScriptRoot }
# The board script and every Claude run use this checkout, never one inherited from a
# Claude Code session that happened to start the shift.
$env:CLAUDE_PROJECT_DIR = $Root
$Board = Join-Path $Root '.claude\skills\task-board\task-board.ps1'
# Logs live beside the checkout, like the lanes: Z:\repos\Curl -> Z:\repos\Curl.logs.
# Lanes are always handed the shift's -LogRoot.
$LogDir = if ($LogRoot) { $LogRoot } else { "$Root.logs" }
$Stamp = if ($ShiftStamp) { $ShiftStamp } else { Get-Date -Format 'yyyyMMdd-HHmmss' }
$LaneTag = if ($Lane) { "-L$Lane" } else { '' }
# The --max-turns 1 probes need no reasoning, so under -Model auto they use sonnet (BL-1705).
$ProbeModel = if ($Model -eq 'auto') { 'sonnet' } else { $Model }
$TraceFile = Join-Path $LogDir "DarkFactory-$Stamp$LaneTag.log"
# Lanes live beside the checkout: Z:\repos\Curl -> Z:\repos\Curl.lanes\lane-1. A lane
# is itself one of those folders, so its lanes directory is its parent.
$LanesDir = if ($Lane) { Split-Path $Root -Parent } else { "$Root.lanes" }
$LockFile = Join-Path $LanesDir 'integrate.lock'
# Every runner of a shift records usage-limit hits here; the coordinator reads it.
$LimitFile = Join-Path $LogDir "limit-$Stamp.txt"

# ---------------------------------------------------------------------------- trace

function Write-Trace {
    param([string]$Task, [string]$Verb, [string]$Detail = '', [string]$Color = 'Gray')
    if ($Lane) { $Task = "L$Lane $Task" }
    $line = '{0} {1,-6} {2,-7} {3}' -f (Get-Date -Format 'HH:mm:ss'), $Task, $Verb, $Detail
    $line = $line.TrimEnd()
    if ($line.Length -gt 118) { $line = $line.Substring(0, 117) + '~' }
    Write-Host $line -ForegroundColor $Color
    if (Test-Path $LogDir) { Add-Content -Path $TraceFile -Value $line -Encoding UTF8 }
}

function Get-Short {
    param([string]$Text, [int]$Max = 70)
    $one = ($Text -replace '\s+', ' ').Trim()
    if ($one.Length -gt $Max) { return $one.Substring(0, $Max - 1) + '~' }
    return $one
}

# ---------------------------------------------------------------------------- heartbeat

# Each runner's lane-<n>.heartbeat.json (ADR-0129 items 8 and 9), for the coordinator to
# publish. The coordinator of a multi-lane shift runs no task and writes none, and nor
# does the out-of-tokens rehearsal.
$WritesHeartbeat = ($Lane -or (-not $AutoLanes -and $LaneCount -le 1)) -and -not $TestOutOfTokens
$script:Beat = @{ Task = $null; Title = $null; Model = $null; ModelWhy = $null; Phase = 'starting'; Step = ''; TaskStartedAt = $null; WrittenAt = [datetime]::MinValue; FailureTraced = $false }

function Get-UtcStamp {
    param([datetime]$When = (Get-Date))
    return $When.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}

function Set-HeartbeatTask {
    # The task the heartbeat names; '' when the runner holds none. A lane resuming a task it
    # held keeps the time it claimed it, which is when its lane-<n>.task file was written.
    param([string]$Id, [string]$Title = $null)
    if (-not $Id) { $script:Beat.Task = $null; $script:Beat.Title = $null; $script:Beat.Model = $null; $script:Beat.ModelWhy = $null; $script:Beat.TaskStartedAt = $null; return }
    if ($script:Beat.Task -eq $Id) { return }
    $started = Get-Date
    if ($Lane) {
        $taskFile = Get-LaneStatePath $Lane 'task'
        if ((Test-Path $taskFile) -and (Get-LaneState $Lane 'task') -eq $Id) { $started = (Get-Item $taskFile).LastWriteTime }
    }
    $script:Beat.Task = $Id
    $script:Beat.Title = if ($PSBoundParameters.ContainsKey('Title')) { $Title } else { Get-TaskTitle $Id }
    $script:Beat.TaskStartedAt = Get-UtcStamp $started
}

function Write-Heartbeat {
    # Writes the heartbeat now. -Phase moves it to a new phase and sets its step (empty by
    # default); without -Phase it refreshes the current phase and step. A failed write is
    # traced once and never stops the runner.
    param([string]$Phase = '', [string]$Step = '')
    if (-not $WritesHeartbeat) { return }
    if ($Phase) { $script:Beat.Phase = $Phase; $script:Beat.Step = $Step }
    $script:Beat.WrittenAt = Get-Date
    try {
        $path = Get-LaneStatePath $Lane 'heartbeat.json'
        New-Item -ItemType Directory -Force -Path (Split-Path $path) -ErrorAction Stop | Out-Null
        $json = [pscustomobject][ordered]@{
            lane = $Lane
            task = $script:Beat.Task
            title = $script:Beat.Title
            model = $script:Beat.Model
            modelWhy = $script:Beat.ModelWhy
            phase = $script:Beat.Phase
            step = $script:Beat.Step
            taskStartedAt = $script:Beat.TaskStartedAt
            heartbeatAt = Get-UtcStamp $script:Beat.WrittenAt
        } | ConvertTo-Json -Compress
        # Written beside the target and renamed over it, so a reader never sees half a file.
        $temporary = "$path.tmp"
        [System.IO.File]::WriteAllText($temporary, $json)
        Move-Item -LiteralPath $temporary -Destination $path -Force -ErrorAction Stop
    } catch {
        if (-not $script:Beat.FailureTraced) {
            $script:Beat.FailureTraced = $true
            Write-Trace '-' 'beat' "heartbeat not written: $(Get-Short $_.Exception.Message 80)" 'DarkYellow'
        }
    }
    # The single runner publishes its own lane 0, throttled to -HeartbeatMinutes; its last
    # publish, with "state": "ended", is made at shift end.
    if (-not $Lane -and $script:Beat.Phase -ne 'finished') { Publish-BoardStatusIfDue -Branch $branch }
}

function Write-HeartbeatIfDue {
    # Refreshes the heartbeat once 60 seconds have passed since the last write.
    if (((Get-Date) - $script:Beat.WrittenAt).TotalSeconds -ge 60) { Write-Heartbeat }
}

function Set-HeartbeatStep {
    # The step is the tool label's verb and detail, cut to 80 characters; a new one is
    # written at once.
    param([string[]]$Label)
    $step = ((@($Label) -join ' ') -replace '\s+', ' ').Trim()
    if ($step.Length -gt 80) { $step = $step.Substring(0, 80) }
    if ($step -eq $script:Beat.Step) { return }
    $script:Beat.Step = $step
    Write-Heartbeat
}

# ---------------------------------------------------------------------------- board branch

# The board branch's status.json (ADR-0129 items 7 to 9): the shift's heartbeat files merged
# into one file and force-pushed as a single parentless commit. It has one writer - the
# coordinator of a lane shift, or the single runner for its own lane 0. Lanes never push.
$script:BoardStatus = @{ Lanes = @{}; PublishedAt = [datetime]::MinValue; PushFailing = $false }
# The latest -Lanes Auto step, published as status.json's autoLanes (BL-770): lanes, target,
# binding, reason and changedAt. Only the Auto start and step set it, so a fixed-lane shift
# publishes null.
$script:AutoLanesStatus = $null

function Set-AutoLanesStatus {
    # Records the Auto lane count, its target and what binds it. -Reason, given only when
    # the count starts or changes, replaces the last change's reason and time.
    param([int]$Lanes, $Target, [string]$Binding, [string]$Reason)
    $old = $script:AutoLanesStatus
    $changed = $Reason -or -not $old
    $script:AutoLanesStatus = [pscustomobject][ordered]@{
        lanes = $Lanes
        target = $(if ($null -ne $Target) { [math]::Round([double]$Target, 1) } else { $null })
        binding = $Binding
        reason = $(if ($changed) { $Reason } else { $old.reason })
        changedAt = $(if ($changed) { Get-UtcStamp } else { $old.changedAt })
    }
}

function Get-BoardStatusJson {
    # Merges every lane-<n>.heartbeat.json of this shift into status.json schema 1, lanes
    # sorted by number. A lane whose file cannot be read keeps its last good object.
    # -State ended marks every lane finished.
    param([string]$Branch, [string]$State = 'running')
    $dir = Join-Path $LogDir "lanes-$Stamp"
    foreach ($file in @(Get-ChildItem $dir -Filter 'lane-*.heartbeat.json' -ErrorAction SilentlyContinue)) {
        try {
            $laneObject = [System.IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -ErrorAction Stop
            if ($null -eq $laneObject.lane) { continue }
            $script:BoardStatus.Lanes[[int]$laneObject.lane] = $laneObject
        } catch { }
    }
    $laneObjects = @($script:BoardStatus.Lanes.Keys | Sort-Object | ForEach-Object { $script:BoardStatus.Lanes[$_] })
    if ($State -eq 'ended') { foreach ($laneObject in $laneObjects) { $laneObject.phase = 'finished' } }
    return [pscustomobject][ordered]@{
        schema = 1
        shift = $Stamp
        branch = $Branch
        state = $State
        publishedAt = Get-UtcStamp
        autoLanes = $script:AutoLanesStatus
        lanes = $laneObjects
    } | ConvertTo-Json -Depth 4
}

function New-BoardCommit {
    # Builds the board branch's commit with plumbing - a blob, a tree holding only
    # status.json, and a commit with no parent - so the checkout's working tree and index
    # never change. Returns the commit id; throws when git fails.
    param([string]$Json)
    # Nothing is piped to git: Windows PowerShell pipes to a native command in its output
    # encoding, which can prepend a byte order mark and mangle a title's non-ASCII
    # characters. The blob is hashed from a UTF-8 file and mktree reads its line through a
    # cmd redirect.
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $file = Join-Path ([IO.Path]::GetTempPath()) "DarkFactoryStatus-$Stamp-$PID.json"
    $treeFile = "$file.tree"
    try {
        [System.IO.File]::WriteAllText($file, $Json, $utf8)
        $blob = "$(git -C $Root hash-object -w -- $file 2>$null)".Trim()
        if ($LASTEXITCODE -ne 0 -or $blob -notmatch '^[0-9a-f]{40,64}$') { throw 'git hash-object failed' }
        [System.IO.File]::WriteAllText($treeFile, "100644 blob $blob`tstatus.json`n", $utf8)
        $mktree = 'git -C "{0}" mktree < "{1}"' -f $Root, $treeFile
        $tree = "$(cmd /s /c $mktree 2>$null)".Trim()
        if ($LASTEXITCODE -ne 0 -or $tree -notmatch '^[0-9a-f]{40,64}$') { throw 'git mktree failed' }
    } finally { Remove-Item $file, $treeFile -ErrorAction SilentlyContinue }
    $commit = "$(git -C $Root commit-tree $tree -m "chore(board): lane status $(Get-UtcStamp)" 2>$null)".Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40,64}$') { throw 'git commit-tree failed' }
    return $commit
}

function Publish-BoardStatus {
    # Publishes status.json now: builds the commit and force-pushes it to the board branch,
    # the only branch this script force-pushes. A failure is traced once per failure streak
    # and never stops the shift or raises the alarm. -HeartbeatMinutes 0 publishes nothing.
    param([string]$Branch, [string]$State = 'running')
    if ($HeartbeatMinutes -le 0) { return }
    $script:BoardStatus.PublishedAt = Get-Date
    try {
        $commit = New-BoardCommit (Get-BoardStatusJson -Branch $Branch -State $State)
        git -C $Root push -q --force origin "${commit}:refs/heads/board" 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "git push exited $LASTEXITCODE" }
        if ($script:BoardStatus.PushFailing) { Write-Trace '-' 'board' 'status.json published to board again' }
        $script:BoardStatus.PushFailing = $false
    } catch {
        if (-not $script:BoardStatus.PushFailing) {
            Write-Trace '-' 'board' "status.json not published: $(Get-Short $_.Exception.Message 80)" 'DarkYellow'
        }
        $script:BoardStatus.PushFailing = $true
    }
}

function Publish-BoardStatusIfDue {
    # Publishes once -HeartbeatMinutes have passed since the last publish.
    param([string]$Branch)
    if (((Get-Date) - $script:BoardStatus.PublishedAt).TotalMinutes -ge $HeartbeatMinutes) { Publish-BoardStatus -Branch $Branch }
}

# ---------------------------------------------------------------------------- herdr

function Get-HerdrBin {
    # The herdr binary when this process runs inside a herdr pane, otherwise $null.
    if ($env:HERDR_ENV -ne '1') { return $null }
    if ($env:HERDR_BIN_PATH -and (Test-Path $env:HERDR_BIN_PATH)) { return $env:HERDR_BIN_PATH }
    $cmd = Get-Command herdr -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Start-Detached {
    # Runs RunDarkFactory.ps1 from $Dir with $ScriptArgs somewhere the user can watch: a
    # new tab in the same herdr workspace when inside herdr, else a new console window.
    # Returns @{ Process = <Process> } or @{ Tab = '<tab id>' }.
    param([string]$Label, [string]$Dir, [string[]]$ScriptArgs)
    # Always this copy of the script; a lane is told its worktree with -LaneDir.
    $script = $PSCommandPath
    if ((Resolve-Path $Dir).Path -ne (Resolve-Path $PSScriptRoot).Path) { $ScriptArgs = @($ScriptArgs) + @('-LaneDir', "`"$Dir`"") }
    $herdr = Get-HerdrBin
    if ($herdr) {
        $create = @('tab', 'create', '--cwd', $Dir, '--label', $Label, '--no-focus')
        if ($env:HERDR_WORKSPACE_ID) { $create += @('--workspace', $env:HERDR_WORKSPACE_ID) }
        $created = (& $herdr @create) -join "`n" | ConvertFrom-Json
        $pane = $created.result.root_pane.pane_id
        if ($pane) {
            & $herdr pane wait-output $pane --match '>' --timeout 15000 2>&1 | Out-Null
            $line = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$script`" " + ($ScriptArgs -join ' ')
            # Windows PowerShell passes a native argument's inner quotes through unescaped,
            # and herdr's argument parser would then strip them, splitting any path with a
            # space. \" survives as a literal quote.
            & $herdr pane run $pane $line.Replace('"', '\"') 2>&1 | Out-Null
            return @{ Tab = $created.result.tab.tab_id }
        }
        Write-Trace '-' 'herdr' 'could not create a herdr tab; using a console window' 'DarkYellow'
    }
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$script`"") + $ScriptArgs
    return @{ Process = (Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -WorkingDirectory $Dir -PassThru) }
}

# Tab captions are all Stewart sees of a shift, so each says whose it is and whether it can
# go: "DF 09:07 L1 · BL-670 Create Curl.Cryptography", "DF 09:07 L2 · BLOCKED, read",
# "DF 09:07 shift · done, close". DF and the shift's start time come first, so a shift's
# tabs group together; a finished tab ends in "close" or "read"; anything else is working.
# A lane holding no task is "DF 09:07 L1 · empty": after each task it clears its screen to
# one line saying what it finished, and a lane that ends cleanly reads "empty, close".
$Dot = [char]0x00B7
$ShiftTime = if ($Stamp -match '^\d{8}-(\d\d)(\d\d)') { "$($Matches[1]):$($Matches[2])" } else { (Get-Date).ToString('HH:mm') }
$OwnTabPrefix = if ($Lane) { "DF $ShiftTime L$Lane" } else { "DF $ShiftTime shift" }

function Get-LaneTabLabel {
    # The caption the coordinator gives lane $N's tab: its prefix, then $Text if any.
    param([int]$N, [string]$Text)
    $label = "DF $ShiftTime L$N"
    if ($Text) { $label += " $Dot $Text" }
    return $label
}

function Set-HerdrTabLabel {
    # Renames a tab the factory opened; captions past 48 characters are cut with "~".
    param([string]$Tab, [string]$Label)
    $herdr = Get-HerdrBin
    if (-not $herdr -or -not $Tab) { return }
    if ($Label.Length -gt 48) { $Label = $Label.Substring(0, 47) + '~' }
    & $herdr tab rename $Tab $Label 2>&1 | Out-Null
}

function Test-FactoryTab {
    # True when $Tab is one the factory opened ("DF ..." or the older "Dark factory ..."),
    # so a shift Stewart started by hand in his own tab never renames or closes it.
    param([string]$Tab)
    $herdr = Get-HerdrBin
    if (-not $herdr -or -not $Tab) { return $false }
    $label = "$(((& $herdr tab get $Tab) -join "`n" | ConvertFrom-Json).result.tab.label)"
    return ($label -like 'DF *' -or $label -like 'Dark factory*')
}

function Set-OwnTabLabel {
    # Captions this process's own tab: "DF 09:07 L1 · $Text", or just the prefix.
    param([string]$Text)
    if (-not (Test-FactoryTab $env:HERDR_TAB_ID)) { return }
    Set-HerdrTabLabel $env:HERDR_TAB_ID $(if ($Text) { "$OwnTabPrefix $Dot $Text" } else { $OwnTabPrefix })
}

function Show-LaneEmpty {
    # A lane that holds no task says so: the screen is cleared of the last run's output,
    # $Line says what it last did, and the caption reads "empty" ($Caption overrides it).
    param([string]$Line, [string]$Caption = 'empty')
    if (-not $Lane) { return }
    try { Clear-Host } catch { }
    Write-Host $Line
    Set-OwnTabLabel $Caption
}

function Close-HerdrTab {
    # Closes a herdr tab the factory opened, once nothing in it is worth reading: a tab
    # left behind only says "idle" and looks like a lane that is still working. Its
    # trace and summary are in <repo>.logs\ either way. Closing this process's own tab
    # ends it. A tab herdr will not close is captioned "done, close" instead.
    param([string]$Tab, [string]$Why, [string]$DoneLabel)
    $herdr = Get-HerdrBin
    if (-not $herdr -or -not $Tab) { return }
    Write-Trace '-' 'herdr' "closing tab $Tab ($Why)"
    & $herdr tab close $Tab 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0 -and $DoneLabel) { Set-HerdrTabLabel $Tab "$DoneLabel $Dot done, close" }
}

function Get-LaneMarker {
    # The CURL_DARK_FACTORY_LANE value for this process (see LANE MARKER in the header): the
    # lane number, 0 for a coordinator or single runner, '' for a launcher that only starts
    # another shift and exits.
    param([int]$ForLane, [switch]$Launcher)
    if ($Launcher) { return '' }
    return "$ForLane"
}

function New-ClaudeRunStartInfo {
    # The process start info Invoke-TaskRun runs claude -p with: cmd.exe in this checkout,
    # inheriting this process's environment (so CURL_DARK_FACTORY_LANE) plus the Bash
    # tool's time limits.
    param([string]$Arguments)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $env:ComSpec
    $psi.Arguments = $Arguments
    $psi.WorkingDirectory = $Root
    $psi.EnvironmentVariables['BASH_DEFAULT_TIMEOUT_MS'] = '1800000'
    $psi.EnvironmentVariables['BASH_MAX_TIMEOUT_MS'] = '3600000'
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    return $psi
}

if ($TestLaneMarker) {
    $results = @(
        @('lane 3 is marked 3', (Get-LaneMarker -ForLane 3), '3'),
        @('a coordinator or single-lane shift is marked 0', (Get-LaneMarker -ForLane 0), '0'),
        @('a -NewTab or -Restart launcher is not marked', (Get-LaneMarker -ForLane 0 -Launcher), '')
    )
    $saved = $env:CURL_DARK_FACTORY_LANE
    try {
        $env:CURL_DARK_FACTORY_LANE = Get-LaneMarker -ForLane 3
        $probe = [System.Diagnostics.Process]::Start((New-ClaudeRunStartInfo '/d /c echo %CURL_DARK_FACTORY_LANE%'))
        $probe.StandardInput.Close()
        $seen = $probe.StandardOutput.ReadToEnd().Trim()
        $probe.WaitForExit()
        $results += , @('a claude -p run started like Invoke-TaskRun sees the marker', $seen, '3')
    } finally { $env:CURL_DARK_FACTORY_LANE = $saved }
    $failed = 0
    foreach ($r in $results) {
        $ok = $r[1] -ceq $r[2]
        if (-not $ok) { $failed++ }
        Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) $($r[0]): '$($r[1])'" -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    }
    exit $(if ($failed) { 1 } else { 0 })
}

function Close-OwnHerdrTab {
    # Closes the tab this shift runs in, but only one the factory opened.
    param([string]$Why)
    if (Test-FactoryTab $env:HERDR_TAB_ID) { Close-HerdrTab $env:HERDR_TAB_ID $Why $OwnTabPrefix }
}

if ($NewTab) {
    # Hand this exact shift, minus -NewTab, to a tab or window of its own, and return.
    $forward = @()
    foreach ($p in $PSBoundParameters.GetEnumerator()) {
        if ($p.Key -eq 'NewTab') { continue }
        if ($p.Value -is [System.Management.Automation.SwitchParameter]) { if ($p.Value) { $forward += "-$($p.Key)" } }
        else { $forward += @("-$($p.Key)", "`"$($p.Value)`"") }
    }
    $where = Start-Detached -Label "DF shift starting" -Dir $Root -ScriptArgs $forward
    if ($where.Tab) { Write-Host "Dark factory started in herdr tab $($where.Tab)." }
    else { Write-Host "Dark factory started in a new console window (pid $($where.Process.Id))." }
    exit 0
}

# ---------------------------------------------------------------------------- alarm

$Glyphs = @{
    'S' = @(' ####', '#    ', ' ### ', '    #', '#### ')
    'T' = @('#####', '  #  ', '  #  ', '  #  ', '  #  ')
    'E' = @('#####', '#    ', '#### ', '#    ', '#####')
    'W' = @('#   #', '#   #', '# # #', '## ##', '#   #')
    'A' = @(' ### ', '#   #', '#####', '#   #', '#   #')
    'R' = @('#### ', '#   #', '#### ', '#  # ', '#   #')
    'I' = @('#####', '  #  ', '  #  ', '  #  ', '#####')
    'N' = @('#   #', '##  #', '# # #', '#  ##', '#   #')
    'P' = @('#### ', '#   #', '#### ', '#    ', '#    ')
    'U' = @('#   #', '#   #', '#   #', '#   #', ' ### ')
    'D' = @('#### ', '#   #', '#   #', '#   #', '#### ')
    '!' = @('  #  ', '  #  ', '  #  ', '     ', '  #  ')
    ' ' = @('   ', '   ', '   ', '   ', '   ')
}

function Get-BigText {
    param([string]$Text)
    foreach ($row in 0..4) {
        (($Text.ToCharArray() | ForEach-Object { $Glyphs[[string]$_][$row] }) -join ' ')
    }
}

function Show-Banner {
    param([string[]]$Reasons, [int]$Frame)
    $colors = @(@('White', 'DarkRed'), @('Black', 'Yellow'))
    $fg, $bg = $colors[$Frame % 2]
    $width = 78
    try { Clear-Host } catch { }
    $lines = @('') + (Get-BigText 'STEWART!') + @('') + (Get-BigText 'INPUT NEEDED') + @('')
    $lines += '  The Curl dark factory is waiting on you.  ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    $lines += ''
    $lines += ($Reasons | Select-Object -First 8 | ForEach-Object { '  ' + (Get-Short $_ 74) })
    $lines += ''
    $lines += '  Press any key to silence.  Trace: ' + $TraceFile
    $lines += ''
    foreach ($l in $lines) { Write-Host ('  ' + $l).PadRight($width) -ForegroundColor $fg -BackgroundColor $bg }
}

# Core Audio, for raising and restoring the master volume from stage 3. Windows ships it;
# nothing is installed.
$script:AudioReady = $false
function Initialize-Audio {
    if ($script:AudioReady) { return $true }
    try {
        Add-Type -ErrorAction Stop -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace DarkFactory {
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume {
    int NotImpl1(); int NotImpl2(); int NotImpl3(); int NotImpl4();
    int SetMasterVolumeLevelScalar(float level, Guid context);
    int NotImpl5();
    int GetMasterVolumeLevelScalar(out float level);
    int NotImpl6(); int NotImpl7(); int NotImpl8(); int NotImpl9();
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid context);
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice { int Activate(ref Guid id, int clsCtx, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint); }
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator { int NotImpl1(); int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device); }
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }
public static class Audio {
    static IAudioEndpointVolume Endpoint() {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDevice device;
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out device));
        Guid iid = typeof(IAudioEndpointVolume).GUID;
        object endpoint;
        Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out endpoint));
        return (IAudioEndpointVolume)endpoint;
    }
    public static float Volume {
        get { float v; Marshal.ThrowExceptionForHR(Endpoint().GetMasterVolumeLevelScalar(out v)); return v; }
        set { Marshal.ThrowExceptionForHR(Endpoint().SetMasterVolumeLevelScalar(value, Guid.Empty)); }
    }
    public static bool Mute {
        get { bool m; Marshal.ThrowExceptionForHR(Endpoint().GetMute(out m)); return m; }
        set { Marshal.ThrowExceptionForHR(Endpoint().SetMute(value, Guid.Empty)); }
    }
}
}
'@
        $script:AudioReady = $true
    } catch { $script:AudioReady = $false }
    return $script:AudioReady
}

$script:Voice = $null
function Get-Voice {
    if ($script:Voice) { return $script:Voice }
    try {
        Add-Type -AssemblyName System.Speech -ErrorAction Stop
        $v = New-Object System.Speech.Synthesis.SpeechSynthesizer
        $v.SetOutputToDefaultAudioDevice()
        if ($AlarmVoice) { try { $v.SelectVoice($AlarmVoice) } catch { } }
        $script:Voice = $v
    } catch { $script:Voice = $null }
    return $script:Voice
}

function Get-Spoken {
    # "BL-004 DECIDE   Decide the licence - keep ..." -> "B L 0 0 4. Decide the licence"
    param([string]$Reason)
    $text = ($Reason -split '\s{2,}', 2)[-1]
    $text = ($text -split ' - ')[0]
    $text = $text -replace '^\d{4}-\d{2}-\d{2}:\s*\w+\s*->\s*\w+\.\s*', '' -replace '^Stewart:\s*', ''
    $id = ''
    if ($Reason -match '^BL-(\d)(\d)(\d)') { $id = "B L $($Matches[1]) $($Matches[2]) $($Matches[3]). " }
    return $id + $text
}

# What the voice calls this factory. The Surl dark factory runs on the same PC with the same
# voice, so everything said aloud names Curl (Stewart, 2026-09-29, BL-901).
$SpokenName = 'the Curl dark factory'

function Get-AlarmSpeech {
    param([string[]]$Reasons, [int]$Stage)
    if ($Stage -eq 0) { return "Stewart, $SpokenName needs your input." }
    $n = $Reasons.Count
    $what = if ($n -eq 1) { 'One item is' } else { "$n items are" }
    $first = ($Reasons | Select-Object -First 2 | ForEach-Object { Get-Spoken $_ }) -join '. Then, '
    if ($Stage -eq 1) { return "Stewart. $SpokenName. $what waiting on you. $first." }
    return "Stewart! Stewart! $SpokenName has stopped. $what waiting on you. $first. Press any key at the terminal."
}

function Invoke-Chime { try { [Console]::Beep(880, 300); [Console]::Beep(660, 300); [Console]::Beep(880, 450) } catch { } }

function Invoke-Siren {
    param([int]$Sweeps)
    try {
        foreach ($s in 1..$Sweeps) {
            foreach ($f in 600, 800, 1000, 1200, 1400) { [Console]::Beep($f, 70) }
            foreach ($f in 1400, 1200, 1000, 800, 600) { [Console]::Beep($f, 70) }
        }
    } catch { }
}

$script:SavedVolume = $null
$script:SavedMute = $false
function Set-AlarmVolume {
    # Remembers the listener's volume and mute once, then goes to -AlarmMaxVolume.
    if (-not (Initialize-Audio)) { return }
    try {
        if ($null -eq $script:SavedVolume) { $script:SavedVolume = [DarkFactory.Audio]::Volume; $script:SavedMute = [DarkFactory.Audio]::Mute }
        [DarkFactory.Audio]::Mute = $false
        [DarkFactory.Audio]::Volume = [float]($AlarmMaxVolume / 100.0)
    } catch { }
}

function Restore-AlarmVolume {
    if ($null -eq $script:SavedVolume) { return }
    try { [DarkFactory.Audio]::Volume = $script:SavedVolume; [DarkFactory.Audio]::Mute = $script:SavedMute } catch { }
    $script:SavedVolume = $null
}

# Stewart's audio switch (BL-1317, BL-1456): while this file exists every sound Curl makes is
# off - the whisper hook's and this script's alarm, chimes, speech and volume changes. Read
# at each sound, so creating or deleting it reaches a shift that is already running.
$script:AudioOffFile = if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'Curl\audio-off' } else { $null }
function Test-AudioOff { return [bool]($script:AudioOffFile -and (Test-Path -LiteralPath $script:AudioOffFile)) }

# True when the factory must stay silent: -QuietAlarm, or the audio-off file. The banner
# and notices still show on screen; nothing plays, speaks or touches the volume.
function Test-AlarmSilent { return ($QuietAlarm -or (Test-AudioOff)) }

function Invoke-AlarmSound {
    param([string[]]$Reasons, [int]$Stage)
    if (Test-AlarmSilent) { return }
    if ($Stage -ge 2) { Set-AlarmVolume }
    switch ($Stage) { 0 { Invoke-Chime } 1 { Invoke-Chime } 2 { Invoke-Siren 2 } default { Invoke-Siren 3 } }
    $voice = Get-Voice
    if ($voice) {
        $voice.SpeakAsyncCancelAll()
        $voice.Volume = 100
        $voice.Rate = if ($Stage -ge 2) { -2 } else { 0 }
        [void]$voice.SpeakAsync((Get-AlarmSpeech -Reasons $Reasons -Stage $Stage))
    }
}

function Test-KeyPressed {
    # $null when there is no console to read (output redirected): the caller gives up.
    try {
        if ([Console]::KeyAvailable) { [void][Console]::ReadKey($true); return $true }
        return $false
    } catch { return $null }
}

function Invoke-Alarm {
    param([string[]]$Reasons)
    try { $Host.UI.RawUI.WindowTitle = '!!! STEWART - INPUT NEEDED !!!' } catch { }
    # Stage starts (minutes) and sound intervals (seconds); -AlarmScale shrinks both for a test.
    $starts = @(0, 2, 5, 15) | ForEach-Object { $_ * 60 * $AlarmScale }
    $every = @(30, 15, 10, 5) | ForEach-Object { [math]::Max(3, $_ * $AlarmScale) }
    $began = Get-Date
    $nextSound = $began
    $stage = -1
    $frame = 0
    try {
        while ($true) {
            $elapsed = ((Get-Date) - $began).TotalSeconds
            $now = 0
            foreach ($i in 0..3) { if ($elapsed -ge $starts[$i]) { $now = $i } }
            if ($now -ne $stage) { $stage = $now; Write-Trace '-' 'ALARM' "stage $($stage + 1) of 4"; $nextSound = Get-Date }
            Show-Banner -Reasons $Reasons -Frame $frame
            $frame++
            if ((Get-Date) -ge $nextSound) {
                Invoke-AlarmSound -Reasons $Reasons -Stage $stage
                $nextSound = (Get-Date).AddSeconds($every[$stage])
            }
            $key = Test-KeyPressed
            if ($null -eq $key) { Start-Sleep -Seconds 6; return }
            if ($key) { Write-Trace '-' 'ALARM' 'acknowledged'; return }
            Start-Sleep -Milliseconds 1000
        }
    } finally {
        if ($script:Voice) { try { $script:Voice.SpeakAsyncCancelAll() } catch { } }
        Restore-AlarmVolume
    }
}

if ($TestAlarm) {
    Invoke-Alarm -Reasons @(
        'BL-004 DECIDE   Decide the licence - keep GPL-3.0 or relicense to MIT/Apache-2.0',
        'BL-005 DECIDE   Decide how curl''s upstream test cases are driven from .NET')
    exit 0
}

# ---------------------------------------------------------------------------- usage limit

function Format-Span {
    # 2 h 55 min, or 40 min, rounded up to the minute.
    param([TimeSpan]$Span)
    $mins = [int][math]::Max(0, [math]::Ceiling($Span.TotalMinutes))
    if ($mins -ge 60) { return "$([math]::Floor($mins / 60)) h $($mins % 60) min" }
    return "$mins min"
}

function Format-SpokenSpan {
    # 2 hours 55 minutes, or 1 minute, rounded up to the minute.
    param([TimeSpan]$Span)
    $mins = [int][math]::Max(1, [math]::Ceiling($Span.TotalMinutes))
    $h = [int][math]::Floor($mins / 60); $m = $mins % 60
    $parts = @()
    if ($h) { $parts += if ($h -eq 1) { '1 hour' } else { "$h hours" } }
    if ($m) { $parts += if ($m -eq 1) { '1 minute' } else { "$m minutes" } }
    return $parts -join ' '
}

function ConvertTo-Unix { param([datetime]$When) return ([DateTimeOffset]$When).ToUnixTimeSeconds() }
function ConvertFrom-Unix { param([long]$Seconds) return [DateTimeOffset]::FromUnixTimeSeconds($Seconds).LocalDateTime }

function Get-UsageReading {
    # The newest usage reading a run logged: the share of the 5-hour and the weekly window
    # used, and when each resets. A window whose reset has passed reads 0. -ThisShift looks
    # only at this shift's runs, so a lane never stops on a reading from before a reset
    # that was lifted early. $null when no run has logged a reading.
    param([switch]$ThisShift)
    $filter = if ($ThisShift) { "*-$Stamp*.jsonl" } else { '*.jsonl' }
    $files = Get-ChildItem $LogDir -Filter $filter -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 10
    foreach ($f in $files) {
        $line = Select-String -LiteralPath $f.FullName -Pattern '"five_hour":\{"utilization":' | Select-Object -Last 1
        if (-not $line) { continue }
        $text = $line.Line
        if ($text -notmatch '"five_hour":\{"utilization":([0-9.]+),"resetsAt":(\d+)') { continue }
        $reading = [pscustomobject]@{ FiveHour = [double]$Matches[1]; FiveHourResets = ConvertFrom-Unix ([long]$Matches[2]); Week = 0.0; WeekResets = [datetime]::MinValue }
        if ($text -match '"seven_day":\{"utilization":([0-9.]+),"resetsAt":(\d+)') {
            $reading.Week = [double]$Matches[1]; $reading.WeekResets = ConvertFrom-Unix ([long]$Matches[2])
        }
        if ($reading.FiveHourResets -le (Get-Date)) { $reading.FiveHour = 0.0 }
        if ($reading.WeekResets -le (Get-Date)) { $reading.Week = 0.0 }
        return $reading
    }
    return $null
}

function Get-UsageStop {
    # Why no new task should be claimed now - the weekly window is at least
    # -StopAtWeeklyUsage used or the 5-hour one -StopAtUsage - or '' when there is room or
    # this shift has no reading yet.
    $u = Get-UsageReading -ThisShift
    if (-not $u) { return '' }
    if ($u.Week -ge $StopAtWeeklyUsage) { return "weekly tokens $([math]::Round($u.Week * 100))% used, reset $($u.WeekResets.ToString('ddd HH:mm'))" }
    if ($u.FiveHour -ge $StopAtUsage) { return "session tokens $([math]::Round($u.FiveHour * 100))% used, reset $($u.FiveHourResets.ToString('HH:mm'))" }
    return ''
}

# ---- auto lanes
# The pure logic behind -Lanes Auto (ADR-0130 items 2 to 5 and 9): no file, git, board,
# Claude or clock inside, so -TestAutoLanes proves it on recorded readings.

function New-UsageSample {
    # One burn-rate sample: a usage reading taken at -At while -ActiveLanes lanes held a
    # task and were not waiting for tokens.
    param([datetime]$At, $Reading, [double]$ActiveLanes)
    return [pscustomobject]@{
        At = $At; FiveHour = [double]$Reading.FiveHour; FiveHourResets = [datetime]$Reading.FiveHourResets
        Week = [double]$Reading.Week; WeekResets = [datetime]$Reading.WeekResets; ActiveLanes = $ActiveLanes
    }
}

function Get-BurnRate {
    # Percentage points per hour per active lane that -Window rose by, over the span from
    # the oldest sample inside the window sharing the newest sample's reset time to the
    # newest sample. $null when the span is too short or the lanes were idle (mean < 0.5).
    # Usage comes in whole points, so a span must hold enough of them that one point of
    # rounding does not swing the rate: 30 to 60 minutes for the 5-hour window (BL-808).
    param([object[]]$Samples, [ValidateSet('FiveHour', 'Week')][string]$Window)
    $windowMinutes = if ($Window -eq 'FiveHour') { 60 } else { 180 }
    $minimumMinutes = if ($Window -eq 'FiveHour') { 30 } else { 60 }
    $resetsName = "$($Window)Resets"
    $sorted = @($Samples | Where-Object { $_ } | Sort-Object At)
    if ($sorted.Count -lt 2) { return $null }
    $newest = $sorted[-1]
    $from = $newest.At.AddMinutes(-$windowMinutes)
    $span = @($sorted | Where-Object { $_.At -ge $from -and $_.$resetsName -eq $newest.$resetsName })
    $hours = ($newest.At - $span[0].At).TotalHours
    if ($hours * 60 -lt $minimumMinutes) { return $null }
    $meanLanes = ($span | Measure-Object ActiveLanes -Average).Average
    if ($meanLanes -lt 0.5) { return $null }
    $rise = [math]::Max(0.0, ($newest.$Window - $span[0].$Window) * 100)
    return $rise / $hours / $meanLanes
}

function Get-WindowTarget {
    # Lanes that spend what is left of one window up to -Stop just as it resets:
    # infinity for no rate or a reset already due, 0 for a budget already spent.
    param([double]$Used, [datetime]$Resets, [datetime]$At, $Rate, [double]$Stop)
    $hours = ($Resets - $At).TotalHours
    if ($null -eq $Rate -or $Rate -le 0 -or $hours -le 0) { return [double]::PositiveInfinity }
    $budget = [math]::Max(0.0, ($Stop - $Used) * 100)
    return $budget / ($Rate * $hours)
}

function Get-PaceTarget {
    # The 5-hour and weekly pace targets, in lanes, for -Sample; the lower is the Target
    # and names the Binding pace. $null while there is no 5-hour rate yet.
    param($Sample, $FiveHourRate, $WeeklyRate, [bool]$WeeklyPace = $false, [double]$StopAtUsage, [double]$StopAtWeeklyUsage)
    if ($null -eq $FiveHourRate) { return $null }
    $fiveHour = Get-WindowTarget -Used $Sample.FiveHour -Resets $Sample.FiveHourResets -At $Sample.At -Rate $FiveHourRate -Stop $StopAtUsage
    $weekly = if ($WeeklyPace) {
        Get-WindowTarget -Used $Sample.Week -Resets $Sample.WeekResets -At $Sample.At -Rate $WeeklyRate -Stop $StopAtWeeklyUsage
    } else { [double]::PositiveInfinity }
    $binding = if ($weekly -lt $fiveHour) { 'weekly pace' } else { '5-hour pace' }
    return [pscustomobject]@{ FiveHour = $fiveHour; Weekly = $weekly; Target = [math]::Min($fiveHour, $weekly); Binding = $binding }
}

function Get-NextLaneCount {
    # One step from -Current lanes toward the pace, capped by the ceilings: up one when
    # the desired count is a whole lane above, and when it is more than a quarter lane
    # below, straight down to max(1, floor(desired + 0.25)) (BL-823). A measured pace must
    # be low two steps running before lanes retire (-PreviousLow, BL-808); a ceiling below
    # -Current retires at once.
    # Low says whether this step's pace was low. Reason is the log line, e.g.
    # "lanes 3 -> 4 (5-hour pace allows 4.9)".
    param([int]$Current, $Pace, [int]$Capacity, [int]$MachineCap, [int]$MaxLanes, [bool]$PreviousLow = $false)
    $ceiling = [math]::Min($Capacity, [math]::Min($MachineCap, $MaxLanes))
    $paceValue = if ($Pace) { [double]$Pace.Target } else { [double]$Current }
    $desired = [math]::Min($paceValue, [double]$ceiling)
    $lanes = $Current
    $low = $false
    $lowOnce = $false
    if ($desired -ge $Current + 1) { $lanes = $Current + 1 }
    elseif ($desired -lt $Current - 0.25) {
        $low = $paceValue -le $ceiling
        if (-not $low -or $PreviousLow) { $lanes = [int][math]::Max(1, [math]::Floor($desired + 0.25)) } else { $lowOnce = $true }
    }
    $culture = [Globalization.CultureInfo]::InvariantCulture
    if ($paceValue -le $ceiling) {
        $binding = if ($Pace) { $Pace.Binding } else { 'no burn rate' }
        $limit = if ($Pace) { "$($Pace.Binding) allows $($paceValue.ToString('0.0', $culture))" } else { 'no burn rate yet' }
    } elseif ($Capacity -eq $ceiling) {
        $binding = 'capacity'
        $limit = if ($Capacity -eq 1) { '1 ready task can run at once' } else { "$Capacity ready tasks can run at once" }
    } elseif ($MachineCap -eq $ceiling) { $binding = 'machine'; $limit = "machine sustains $MachineCap" }
    else { $binding = 'maximum'; $limit = "lane maximum $MaxLanes" }
    if ($lowOnce) { $limit += ', low once' }
    $step = if ($lanes -eq $Current) { "lanes $Current held" } else { "lanes $Current -> $lanes" }
    # Binding names the limit for status.json's autoLanes (BL-770).
    return [pscustomobject]@{ Lanes = $lanes; Changed = ($lanes -ne $Current); Desired = $desired; Low = $low; Binding = $binding; Reason = "$step ($limit)" }
}

function Get-AutoStartCount {
    # Where an Auto shift starts: the lane count auto-lanes.json saved (none on the first
    # Auto shift), raised to -MinStart, capped by the ceilings and at least 1. Why says which.
    param($Saved, [int]$Capacity, [int]$MachineCap, [int]$MinStart = $MinStartLanes, [int]$Max = $MaxLanes)
    $count = $MinStart
    $why = 'first auto shift'
    if ($Saved -and $Saved.lanes) { $count = [int]$Saved.lanes; $why = "last shift saved $count" }
    if ($count -lt $MinStart) { $count = $MinStart; $why += ", raised to $count by -MinStartLanes" }
    $ceiling = [math]::Min($Capacity, [math]::Min($MachineCap, $Max))
    if ($count -gt $ceiling) { $count = [math]::Max(1, $ceiling); $why += ", capped at $count" }
    return [pscustomobject]@{ Count = $count; Why = $why }
}

# Which lane scaling touches (ADR-0130 item 6), pure so -TestAutoLanes proves them.
function Get-LaneToAdd {
    # The lowest lane number from 1 to -Max that is not active, or $null when all are.
    param([int[]]$Active, [int]$Max = 16)
    foreach ($n in 1..$Max) { if ($Active -notcontains $n) { return $n } }
    return $null
}

function Get-LaneToRetire {
    # The highest-numbered active lane not already retiring, or $null when there is none.
    # One among -Idle (lanes holding no task) is preferred, so it stops within a minute
    # instead of after a task (BL-1374).
    param([int[]]$Active, [int[]]$Retiring, [int[]]$Idle = @())
    $candidates = @($Active | Where-Object { $Retiring -notcontains $_ } | Sort-Object -Descending)
    $idleCandidates = @($candidates | Where-Object { $Idle -contains $_ })
    if ($idleCandidates.Count) { return $idleCandidates[0] }
    if ($candidates.Count) { return $candidates[0] }
    return $null
}

function Get-CapacityLaneCount {
    # One capacity step of a fixed -Lanes N shift (BL-1374): straight down to the board's
    # -Capacity (at least 1) when more lanes run, but by no more than the -Idle lanes; up
    # one lane when the capacity allows it, never above -Requested. Reason is the log line,
    # e.g. "lanes 9 -> 4 (4 ready tasks can run at once)".
    param([int]$Current, [int]$Capacity, [int]$Requested, [int]$Idle)
    $ceiling = [math]::Max(1, [math]::Min($Capacity, $Requested))
    $lanes = $Current
    if ($Current -gt $ceiling) { $lanes = [math]::Max($ceiling, $Current - $Idle) }
    elseif ($Current -lt $ceiling) { $lanes = $Current + 1 }
    $limit = if ($Capacity -ge $Requested) { "-Lanes $Requested" }
        elseif ($Capacity -eq 1) { '1 ready task can run at once' }
        elseif ($Capacity -le 0) { 'no ready task can run' }
        else { "$Capacity ready tasks can run at once" }
    $step = if ($lanes -eq $Current) { "lanes $Current held" } else { "lanes $Current -> $lanes" }
    return [pscustomobject]@{ Lanes = $lanes; Changed = ($lanes -ne $Current); Reason = "$step ($limit)" }
}

function Get-FixedStartCount {
    # The lanes a fixed -Lanes N shift starts with (BL-1384, AF-0041): -Requested capped by
    # the board's -Capacity (at least 1), never below -Adopted, the highest lane number
    # adopted from the previous shift. Why is the log text, e.g. "-Lanes 9, capped at 4: 4
    # ready tasks can run at once".
    param([int]$Requested, [int]$Capacity, [int]$Adopted = 0)
    $count = [math]::Max(1, [math]::Min($Requested, $Capacity))
    $why = if ($count -ge $Requested) { "-Lanes $Requested" }
        elseif ($Capacity -le 0) { "-Lanes $Requested, capped at 1: no ready task can run" }
        elseif ($Capacity -eq 1) { "-Lanes $Requested, capped at 1: 1 ready task can run at once" }
        else { "-Lanes $Requested, capped at ${count}: $Capacity ready tasks can run at once" }
    if ($Adopted -gt $count) { $count = $Adopted; $why = "lane $Adopted adopted from the previous shift" }
    return [pscustomobject]@{ Count = $count; Why = $why }
}

function Test-LanesFinished {
    # True once every active lane is among the finished ones (has written its summary).
    param([int[]]$Active, [int[]]$Finished)
    return (@($Active | Where-Object { $Finished -notcontains $_ }).Count -eq 0)
}

if ($TestAutoLanes) {
    # Recorded readings on one arbitrary date D, each case checked against its expected log line.
    $D = [datetime]'2026-01-05'
    function New-TestSample {
        param([string]$At, [double]$FiveHour, [datetime]$FiveHourResets, [double]$Week, [datetime]$WeekResets, [double]$ActiveLanes)
        $reading = [pscustomobject]@{ FiveHour = $FiveHour; FiveHourResets = $FiveHourResets; Week = $Week; WeekResets = $WeekResets }
        return New-UsageSample -At ($D + [TimeSpan]$At) -Reading $reading -ActiveLanes $ActiveLanes
    }
    function Get-SampledLaneCount {
        param([object[]]$Samples, [int]$Current, [bool]$WeeklyPace, [int]$Capacity, [int]$MachineCap, [int]$MaxLanes, [bool]$PreviousLow = $false)
        $pace = Get-PaceTarget -Sample $Samples[-1] -FiveHourRate (Get-BurnRate -Samples $Samples -Window FiveHour) `
            -WeeklyRate (Get-BurnRate -Samples $Samples -Window Week) -WeeklyPace $WeeklyPace -StopAtUsage 0.85 -StopAtWeeklyUsage 0.97
        return (Get-NextLaneCount -Current $Current -Pace $pace -Capacity $Capacity -MachineCap $MachineCap -MaxLanes $MaxLanes -PreviousLow $PreviousLow).Reason
    }
    function New-TestPace { param([double]$Target) return [pscustomobject]@{ FiveHour = $Target; Weekly = [double]::PositiveInfinity; Target = $Target; Binding = '5-hour pace' } }

    $fiveHourBinds = @(
        New-TestSample '12:00' 0.20 ($D.AddHours(15.5)) 0.10 ($D.AddDays(3).AddHours(12.5)) 3
        New-TestSample '12:15' 0.23 ($D.AddHours(15.5)) 0.10 ($D.AddDays(3).AddHours(12.5)) 3
        New-TestSample '12:30' 0.26 ($D.AddHours(15.5)) 0.11 ($D.AddDays(3).AddHours(12.5)) 3)
    $weeklyBinds = @(
        New-TestSample '10:30' 0.10 ($D.AddHours(15.5)) 0.50 ($D.AddDays(2).AddHours(12.5)) 4
        New-TestSample '12:00' 0.20 ($D.AddHours(15.5)) 0.53 ($D.AddDays(2).AddHours(12.5)) 4
        New-TestSample '12:30' 0.22 ($D.AddHours(15.5)) 0.54 ($D.AddDays(2).AddHours(12.5)) 4)
    $resetInWindow = @(
        New-TestSample '15:29' 0.84 ($D.AddHours(15.5)) 0.30 ($D.AddDays(3)) 3
        New-TestSample '15:44' 0.02 ($D.AddHours(20.5)) 0.30 ($D.AddDays(3)) 3
        New-TestSample '15:59' 0.05 ($D.AddHours(20.5)) 0.30 ($D.AddDays(3)) 3
        New-TestSample '16:14' 0.08 ($D.AddHours(20.5)) 0.30 ($D.AddDays(3)) 3)
    $fiveHourTooShort = @($resetInWindow | Select-Object -First 3)
    $idle = @(
        New-TestSample '12:00' 0.20 ($D.AddHours(15.5)) 0.10 ($D.AddDays(3)) 0
        New-TestSample '12:30' 0.26 ($D.AddHours(15.5)) 0.11 ($D.AddDays(3)) 0)
    $infinite = New-TestPace ([double]::PositiveInfinity)

    $cases = @(
        ,@('five-hour-binds', 'lanes 3 -> 4 (5-hour pace allows 4.9)', (Get-SampledLaneCount $fiveHourBinds 3 $true 6 8 16))
        ,@('five-hour-binds weekly rate', 'null', "$(if ($null -eq (Get-BurnRate $fiveHourBinds Week)) { 'null' } else { 'a rate' })")
        ,@('weekly-binds low once', 'lanes 4 held (weekly pace allows 1.8, low once)', (Get-SampledLaneCount $weeklyBinds 4 $true 6 8 16))
        ,@('weekly-binds low twice', 'lanes 4 -> 2 (weekly pace allows 1.8)', (Get-SampledLaneCount $weeklyBinds 4 $true 6 8 16 $true))
        ,@('five-hour span under 30 min', 'null', "$(if ($null -eq (Get-BurnRate $fiveHourTooShort FiveHour)) { 'null' } else { 'a rate' })")
        ,@('weekly-pace-off', 'lanes 4 -> 5 (6 ready tasks can run at once)', (Get-SampledLaneCount $weeklyBinds 4 $false 6 8 16))
        ,@('weekly-pace-off by default', 'Infinity', "$((Get-PaceTarget -Sample $weeklyBinds[-1] -FiveHourRate 1.0 -WeeklyRate 5.0 -StopAtUsage 0.85 -StopAtWeeklyUsage 0.97).Weekly)")
        ,@('five-hour-reset-in-window', 'lanes 3 -> 4 (5-hour pace allows 4.5)', (Get-SampledLaneCount $resetInWindow 3 $true 8 8 16))
        ,@('hold-inside-band 3.8', 'lanes 4 held (5-hour pace allows 3.8)', (Get-NextLaneCount 4 (New-TestPace 3.8) 16 16 16).Reason)
        ,@('below-band low once', 'lanes 4 held (5-hour pace allows 3.7, low once)', (Get-NextLaneCount 4 (New-TestPace 3.7) 16 16 16).Reason)
        ,@('below-band low twice', 'lanes 4 -> 3 (5-hour pace allows 3.7)', (Get-NextLaneCount 4 (New-TestPace 3.7) 16 16 16 $true).Reason)
        ,@('below-band Low flag', 'True', "$((Get-NextLaneCount 4 (New-TestPace 3.7) 16 16 16).Low)")
        ,@('capacity-ceiling 3', 'lanes 5 -> 3 (3 ready tasks can run at once)', (Get-NextLaneCount 5 (New-TestPace 9.0) 3 16 16).Reason)
        ,@('pace drop 6 to 2.4 low once', 'lanes 6 held (5-hour pace allows 2.4, low once)', (Get-NextLaneCount 6 (New-TestPace 2.4) 16 16 16).Reason)
        ,@('pace drop 6 to 2.4 low twice', 'lanes 6 -> 2 (5-hour pace allows 2.4)', (Get-NextLaneCount 6 (New-TestPace 2.4) 16 16 16 $true).Reason)
        ,@('pace drop 6 to 2.8 rounds up', 'lanes 6 -> 3 (5-hour pace allows 2.8)', (Get-NextLaneCount 6 (New-TestPace 2.8) 16 16 16 $true).Reason)
        ,@('pace drop 5 to spent budget', 'lanes 5 -> 1 (5-hour pace allows 0.0)', (Get-NextLaneCount 5 (New-TestPace 0.0) 16 16 16 $true).Reason)
        ,@('ceiling drop 6 to capacity 2', 'lanes 6 -> 2 (2 ready tasks can run at once)', (Get-NextLaneCount 6 (New-TestPace 9.0) 2 16 16).Reason)
        ,@('ceiling drop 6 to machine 3', 'lanes 6 -> 3 (machine sustains 3)', (Get-NextLaneCount 6 $infinite 20 3 16).Reason)
        ,@('up one from 2 at ceiling 6', 'lanes 2 -> 3 (lane maximum 6)', (Get-NextLaneCount 2 $infinite 20 16 6).Reason)
        ,@('capacity-ceiling 1', 'lanes 2 -> 1 (1 ready task can run at once)', (Get-NextLaneCount 2 $null 1 16 16).Reason)
        ,@('machine-ceiling', 'lanes 7 held (machine sustains 7)', (Get-NextLaneCount 7 $infinite 20 7 16).Reason)
        ,@('max-ceiling', 'lanes 2 held (lane maximum 2)', (Get-NextLaneCount 2 $infinite 20 16 2).Reason)
        ,@('no-rate', 'lanes 2 held (no burn rate yet)', (Get-NextLaneCount 2 $null 6 8 16).Reason)
        ,@('binding pace', '5-hour pace', (Get-NextLaneCount 4 (New-TestPace 3.8) 16 16 16).Binding)
        ,@('binding capacity', 'capacity', (Get-NextLaneCount 5 (New-TestPace 9.0) 3 16 16).Binding)
        ,@('binding machine', 'machine', (Get-NextLaneCount 7 $infinite 20 7 16).Binding)
        ,@('binding maximum', 'maximum', (Get-NextLaneCount 2 $infinite 20 16 2).Binding)
        ,@('binding no rate', 'no burn rate', (Get-NextLaneCount 2 $null 6 8 16).Binding)
        ,@('idle-lanes', 'null', "$(if ($null -eq (Get-BurnRate $idle FiveHour)) { 'null' } else { 'a rate' })")
        ,@('lane-to-add 1,2,4', '3', "$(Get-LaneToAdd -Active 1, 2, 4 -Max 16)")
        ,@('lane-to-add 1..16', 'null', "$(if ($null -eq (Get-LaneToAdd -Active (1..16) -Max 16)) { 'null' } else { 'a lane' })")
        ,@('lane-to-retire 1,2,3 retiring 3', '2', "$(Get-LaneToRetire -Active 1, 2, 3 -Retiring 3)")
        ,@('lane-to-retire 1,2,3 retiring 1,2,3', 'null', "$(if ($null -eq (Get-LaneToRetire -Active 1, 2, 3 -Retiring 1, 2, 3)) { 'null' } else { 'a lane' })")
        ,@('lane-to-retire 1,2,3,4 idle 1,2', '2', "$(Get-LaneToRetire -Active 1, 2, 3, 4 -Retiring @() -Idle 1, 2)")
        ,@('lane-to-retire 1,2,3 retiring 2 idle 2', '3', "$(Get-LaneToRetire -Active 1, 2, 3 -Retiring 2 -Idle 2)")
        ,@('capacity 9 lanes capacity 4 idle 6', 'lanes 9 -> 4 (4 ready tasks can run at once)', (Get-CapacityLaneCount -Current 9 -Capacity 4 -Requested 9 -Idle 6).Reason)
        ,@('capacity 9 lanes capacity 4 idle 2', 'lanes 9 -> 7 (4 ready tasks can run at once)', (Get-CapacityLaneCount -Current 9 -Capacity 4 -Requested 9 -Idle 2).Reason)
        ,@('capacity 3 lanes capacity 0', 'lanes 3 -> 1 (no ready task can run)', (Get-CapacityLaneCount -Current 3 -Capacity 0 -Requested 9 -Idle 3).Reason)
        ,@('capacity 1 lane capacity 1', 'lanes 1 held (1 ready task can run at once)', (Get-CapacityLaneCount -Current 1 -Capacity 1 -Requested 9 -Idle 1).Reason)
        ,@('capacity 4 lanes capacity 6', 'lanes 4 -> 5 (6 ready tasks can run at once)', (Get-CapacityLaneCount -Current 4 -Capacity 6 -Requested 9 -Idle 0).Reason)
        ,@('capacity 9 lanes capacity 12', 'lanes 9 held (-Lanes 9)', (Get-CapacityLaneCount -Current 9 -Capacity 12 -Requested 9 -Idle 0).Reason)
        ,@('capacity 8 lanes capacity 12', 'lanes 8 -> 9 (-Lanes 9)', (Get-CapacityLaneCount -Current 8 -Capacity 12 -Requested 9 -Idle 0).Reason)
        ,@('fixed start 9 capacity 4', '4 (-Lanes 9, capped at 4: 4 ready tasks can run at once)', "$((Get-FixedStartCount -Requested 9 -Capacity 4) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('fixed start 9 capacity 12', '9 (-Lanes 9)', "$((Get-FixedStartCount -Requested 9 -Capacity 12) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('fixed start 9 capacity 1', '1 (-Lanes 9, capped at 1: 1 ready task can run at once)', "$((Get-FixedStartCount -Requested 9 -Capacity 1) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('fixed start 9 capacity 0', '1 (-Lanes 9, capped at 1: no ready task can run)', "$((Get-FixedStartCount -Requested 9 -Capacity 0) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('fixed start 9 capacity 2 adopted 6', '6 (lane 6 adopted from the previous shift)', "$((Get-FixedStartCount -Requested 9 -Capacity 2 -Adopted 6) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('lanes-finished 1,3 of 1,2', 'False', "$(Test-LanesFinished -Active 1, 3 -Finished 1, 2)")
        ,@('lanes-finished 1,3 of 1,3', 'True', "$(Test-LanesFinished -Active 1, 3 -Finished 1, 3)")
        ,@('start saved 2', '3 (last shift saved 2, raised to 3 by -MinStartLanes)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 2 }) 16 16 3 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start saved 5', '5 (last shift saved 5)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 5 }) 16 16 3 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start first shift', '3 (first auto shift)', "$((Get-AutoStartCount $null 16 16 3 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start min 4 ceiling 2', '2 (first auto shift, capped at 2)', "$((Get-AutoStartCount $null 2 16 4 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start default at ceiling', '9 (last shift saved 2, raised to 16 by -MinStartLanes, capped at 9)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 2 }) 16 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start default at capacity', '4 (first auto shift, capped at 4)', "$((Get-AutoStartCount $null 4 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start min 3 saved 2', '3 (last shift saved 2, raised to 3 by -MinStartLanes)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 2 }) 16 16 3) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start min 1 saved 1', '1 (last shift saved 1)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 1 }) 16 16 1 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })"))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# ---- machine probe
# How many concurrent solution builds this PC sustains (ADR-0130 item 7). The pass and cap
# rule is pure, so -TestMachineProbe proves it on recorded steps; -ProbeMachine measures.

# Lanes build for minutes of each task and integrate one at a time, so k simultaneous
# builds is a worst case, and how much slower they run swings with the one-build baseline
# (13.6 s one probe, 9.0 s the next). Only memory running out stalls lanes, so memory alone
# sets the cap; wall time and slowdown are recorded for reading (BL-812).
$MachineProbeRule = 'every build succeeds and free memory >= 20%'

function Test-MachineProbeStep {
    # Whether one probe step passes: every build succeeded and the lowest free memory
    # stayed at least 20% of RAM. -OneBuildSeconds is kept for the callers; it decides nothing.
    param($Step, [double]$OneBuildSeconds)
    return ([bool]$Step.Succeeded -and [double]$Step.MinFreeMemoryPercent -ge 20)
}

function Get-MachineLaneCap {
    # The largest passing lane count before the first failing step, at least 1. Complete is
    # false when the steps ran out (at -MaxLanes) before any failed and before 16.
    param([object[]]$Steps, [ValidateRange(1, 16)][int]$MaxLanes = 16)
    $steps = @($Steps | Where-Object { $_ } | Select-Object -First $MaxLanes)
    $cap = 0
    $failed = $false
    foreach ($step in $steps) {
        if (-not (Test-MachineProbeStep -Step $step -OneBuildSeconds $steps[0].Seconds)) { $failed = $true; break }
        $cap = [int]$step.Lanes
    }
    return [pscustomobject]@{ Cap = [math]::Max(1, $cap); Complete = ($failed -or $cap -ge 16) }
}

if ($TestMachineProbe) {
    function New-TestProbeSteps {
        # Recorded steps for k = 1, 2, ...: wall seconds, free memory percent (50 unless
        # given) and whether every build succeeded (unless k is in -FailedAt).
        param([double[]]$Seconds, [double[]]$FreePercent = @(), [int[]]$FailedAt = @())
        $k = 0
        return @($Seconds | ForEach-Object {
            $k++
            $free = if ($FreePercent.Count -ge $k) { $FreePercent[$k - 1] } else { 50 }
            [pscustomobject]@{ Lanes = $k; Seconds = $_; MinFreeMemoryPercent = $free; Succeeded = ($FailedAt -notcontains $k) }
        })
    }
    function Get-TestCapText {
        param([object[]]$Steps, [int]$MaxLanes = 16)
        $result = Get-MachineLaneCap -Steps $Steps -MaxLanes $MaxLanes
        return "cap $($result.Cap), $(if ($result.Complete) { 'complete' } else { 'not complete' })"
    }
    $cases = @(
        ,@('slow-steps-still-pass', 'cap 5, complete', (Get-TestCapText (New-TestProbeSteps 9.0, 33.2, 32.4, 41.4, 60, 70 -FreePercent 75.3, 61.2, 67.5, 63.8, 30, 19)))
        ,@('knee-by-memory', 'cap 2, complete', (Get-TestCapText (New-TestProbeSteps 60, 61, 63, 64 -FreePercent 40, 25, 19, 8)))
        ,@('failed-build', 'cap 2, complete', (Get-TestCapText (New-TestProbeSteps 60, 61, 62 -FailedAt 3)))
        ,@('all-pass-to-16', 'cap 16, complete', (Get-TestCapText (New-TestProbeSteps (@(60) * 16))))
        ,@('cut-short', 'cap 2, not complete', (Get-TestCapText (New-TestProbeSteps 60, 61) 2))
        ,@('first-step-fails', 'cap 1, complete', (Get-TestCapText (New-TestProbeSteps 60 -FailedAt 1))))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

function Invoke-ProbeBuilds {
    # Runs -Count concurrent builds of the checkout, each with its own artifacts folder
    # under -ProbeDir, sampling free memory every 2 s. Returns the step's measurements.
    param([string]$ProbeDir, [int]$Count, [switch]$Incremental)
    $os = Get-CimInstance Win32_OperatingSystem
    $totalKB = [double]$os.TotalVisibleMemorySize
    $minFreeKB = [double]$os.FreePhysicalMemory
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $builds = foreach ($i in 1..$Count) {
        $artifacts = Join-Path $ProbeDir "$i"
        New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
        $buildArgs = @('build', "`"$Root`"", '-nologo', '-v', 'q', '--artifacts-path', "`"$artifacts`"")
        if (-not $Incremental) { $buildArgs += '--no-incremental' }
        $process = Start-Process dotnet -ArgumentList $buildArgs -PassThru -NoNewWindow `
            -RedirectStandardOutput (Join-Path $ProbeDir "$i.out") -RedirectStandardError (Join-Path $ProbeDir "$i.err")
        # Windows PowerShell only reports ExitCode for a process whose handle was taken early.
        [void]$process.Handle
        $process
    }
    while (@($builds | Where-Object { -not $_.HasExited }).Count) {
        Start-Sleep -Seconds 2
        $minFreeKB = [math]::Min($minFreeKB, [double](Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory)
    }
    $clock.Stop()
    $builds | ForEach-Object { $_.WaitForExit() }
    return [pscustomobject]@{
        Lanes = $Count; Seconds = [math]::Round($clock.Elapsed.TotalSeconds, 1)
        MinFreeMemoryPercent = [math]::Round(100 * $minFreeKB / $totalKB, 1)
        Succeeded = (@($builds | Where-Object { $_.ExitCode -ne 0 }).Count -eq 0)
    }
}

$MachineFile = Join-Path $LanesDir 'machine-lanes.json'

function Invoke-MachineProbe {
    # Warm up once, then step k = 1, 2, ... concurrent builds until a step fails, 16, or
    # -ProbeMaxLanes, write the cap to <LanesDir>\machine-lanes.json and return it.
    $probeDir = Join-Path $LanesDir 'probe'
    $machineFile = $MachineFile
    New-Item -ItemType Directory -Force -Path $probeDir | Out-Null
    Write-Trace 'probe' 'warm-up' "one build of $Root, not measured"
    $warmUp = Invoke-ProbeBuilds -ProbeDir $probeDir -Count 1 -Incremental
    if (-not $warmUp.Succeeded) { Write-Trace 'probe' 'warn' 'the warm-up build failed' 'Yellow' }
    $steps = @()
    foreach ($k in 1..$ProbeMaxLanes) {
        $step = Invoke-ProbeBuilds -ProbeDir $probeDir -Count $k
        $steps += $step
        $passed = Test-MachineProbeStep -Step $step -OneBuildSeconds $steps[0].Seconds
        $culture = [Globalization.CultureInfo]::InvariantCulture
        Write-Trace 'probe' $(if ($passed) { 'pass' } else { 'fail' }) ("{0} builds {1}s, {2}x, free memory {3}%{4}" -f $k,
            $step.Seconds.ToString('0.0', $culture), ($step.Seconds / $steps[0].Seconds).ToString('0.00', $culture),
            $step.MinFreeMemoryPercent.ToString('0.0', $culture), $(if ($step.Succeeded) { '' } else { ', a build failed' })) `
            $(if ($passed) { 'Green' } else { 'Yellow' })
        if (-not $passed) { break }
    }
    $result = Get-MachineLaneCap -Steps $steps -MaxLanes $ProbeMaxLanes
    $memoryBytes = [double](Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory
    $record = [ordered]@{
        schema = 1; probedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        logicalProcessors = [Environment]::ProcessorCount; memoryGB = [math]::Round($memoryBytes / 1GB, 1)
        complete = $result.Complete; cap = $result.Cap; rule = $MachineProbeRule
        steps = @($steps | ForEach-Object {
            [ordered]@{ lanes = $_.Lanes; seconds = $_.Seconds; slowdown = [math]::Round($_.Seconds / $steps[0].Seconds, 2)
                minFreeMemoryPercent = $_.MinFreeMemoryPercent; succeeded = $_.Succeeded }
        })
    }
    $temp = "$machineFile.tmp"
    # Windows PowerShell escapes < and > in JSON; the rule reads better as written.
    $json = ($record | ConvertTo-Json -Depth 4) -replace '\\u003c', '<' -replace '\\u003e', '>'
    [IO.File]::WriteAllText($temp, $json, (New-Object Text.UTF8Encoding $false))
    Move-Item -Force -Path $temp -Destination $machineFile
    Remove-Item -Recurse -Force -Path $probeDir -ErrorAction SilentlyContinue
    Write-Trace 'probe' 'done' "machine sustains $($result.Cap)$(if (-not $result.Complete) { ' (incomplete)' }); $machineFile" 'Cyan'
    return $result
}

if ($ProbeMachine) { Invoke-MachineProbe | Out-Null; exit 0 }

function Get-OutOfTokensUntil {
    # When the last run was refused for the account's usage limit, the local time the
    # limit resets; otherwise $null. The run's rate_limit_event gives it exactly; the
    # result's text is the fallback: "You've hit your session limit · resets 12:50pm".
    if ($script:LimitResetAt) { return $script:LimitResetAt }
    $text = if ($script:RunResult) { "$($script:RunResult.result)" } else { '' }
    if ($text -notmatch '(?i)hit your .*limit|usage limit|limit reached') { return $null }
    if ($text -match '\|(\d{10})') { return (ConvertFrom-Unix ([long]$Matches[1])) }
    if ($text -match '(?i)resets\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)') {
        $hour = [int]$Matches[1] % 12
        if ($Matches[3] -ieq 'pm') { $hour += 12 }
        $min = if ($Matches[2]) { [int]$Matches[2] } else { 0 }
        $at = (Get-Date).Date.AddHours($hour).AddMinutes($min)
        if ($at -lt (Get-Date).AddMinutes(-10)) { $at = $at.AddDays(1) }
        return $at
    }
    # Refused, but no reset time given: look again in half an hour.
    return (Get-Date).AddMinutes(30)
}

function Add-LimitMark {
    # Lanes write at the same moment when the limit hits them all; retry a busy file.
    param([string]$Line)
    foreach ($try in 1..10) {
        try { Add-Content -Path $LimitFile -Value $Line -Encoding UTF8 -ErrorAction Stop; return }
        catch { Start-Sleep -Milliseconds 200 }
    }
}

function Show-LimitNotice {
    # A one-off notice, not the alarm: a coloured block, a chime and one spoken sentence.
    param([string]$Headline, [string]$Detail, [string]$Spoken, [string]$Color)
    $bar = '=' * 78
    foreach ($l in @($bar, "  $Headline", "  $Detail", $bar)) { Write-Host $l.PadRight(78) -ForegroundColor Black -BackgroundColor $Color }
    Write-Trace '-' 'TOKENS' "$Headline  $Detail" $Color
    if (Test-AlarmSilent) { return }
    Invoke-Chime
    $voice = Get-Voice
    if ($voice) {
        $voice.SpeakAsyncCancelAll()
        $voice.Volume = 100
        $voice.Rate = 0
        [void]$voice.SpeakAsync($Spoken)
    }
}

$script:Notice = @{ Reset = 0L; Warned = $false; Resumed = $false }

function Test-WaitingForSession { return ($script:Notice.Reset -and -not $script:Notice.Resumed) }

function Update-LimitNotice {
    # Tells Stewart about the usage limit once per stage however many lanes hit it: out of
    # tokens, the new session about to start, and the new session in use. Runners record
    # "reset <unix>" and "resumed <unix> <ID>" in the limit file; whoever Stewart watches -
    # the coordinator, or a lone runner - calls this to announce them.
    if (-not (Test-Path $LimitFile)) { return }
    $lines = @(Get-Content $LimitFile -ErrorAction SilentlyContinue)
    $resets = @($lines | Where-Object { $_ -match '^reset \d+$' } | ForEach-Object { [long]($_ -split ' ')[1] })
    if (-not $resets.Count) { return }
    $latest = [long]($resets | Measure-Object -Maximum).Maximum
    $reset = ConvertFrom-Unix $latest
    $at = $reset.ToString('HH:mm')
    $n = $script:Notice
    if ($n.Reset -ne $latest) {
        $n.Reset = $latest; $n.Warned = $false; $n.Resumed = $false
        $left = $reset - (Get-Date)
        Show-LimitNotice 'OUT OF TOKENS' "Out of tokens at $(Get-Date -Format 'HH:mm'). New session starts at $at, in $(Format-Span $left)." `
            "Stewart, $SpokenName is out of tokens. The new session starts at $($reset.ToString('h:mm tt')), in $(Format-SpokenSpan $left)." 'Yellow'
    }
    $resumed = @($lines | Where-Object { $_ -match "^resumed $latest \S+$" } | ForEach-Object { ($_ -split ' ')[2] })
    if (-not $n.Resumed -and $resumed.Count) {
        $n.Resumed = $true; $n.Warned = $true
        Show-LimitNotice 'NEW SESSION STARTED' "Started using the new session at $(Get-Date -Format 'HH:mm'); resuming $($resumed[0])." `
            "Stewart, the new session has started. $SpokenName is working again." 'Green'
        try { $Host.UI.RawUI.WindowTitle = if ($AutoLanes -or $LaneCount -gt 1) { "Dark factory - $LaneCount lanes" } else { 'Dark factory - running' } } catch { }
        return
    }
    if ($n.Resumed) { return }
    $left = $reset - (Get-Date)
    if (-not $n.Warned -and $left.TotalSeconds -le $LimitWarnSeconds) {
        $n.Warned = $true
        Show-LimitNotice 'NEW SESSION SOON' "The new session starts at $at, in $(Format-Span $left)." `
            "Stewart, the new session for $SpokenName will be ready in about $(Format-SpokenSpan $left)." 'Cyan'
    }
    try { $Host.UI.RawUI.WindowTitle = "Dark factory - out of tokens, new session at $at (in $(Format-Span $left))" } catch { }
}

function Test-WakeRequested {
    # True once "wake <unix>" for this reset is in the limit file: tokens came back early,
    # because Stewart reset the limit or the probe found the account answering again.
    # Only a wake written after the last time a runner hit this limit counts: a runner
    # that hits it again after a wake finds the tokens were not back after all, and must
    # wait, not spin through wait after wait on the stale wake.
    param([long]$Unix)
    if (-not (Test-Path $LimitFile)) { return $false }
    $marks = @(Get-Content $LimitFile -ErrorAction SilentlyContinue)
    return ([array]::LastIndexOf($marks, "wake $Unix") -gt [array]::LastIndexOf($marks, "reset $Unix"))
}

$script:NextProbe = [datetime]::MinValue
function Invoke-LimitProbe {
    # While the shift waits, asks Claude for one word every -LimitProbeMinutes. A refused
    # probe costs nothing; one that is answered means the limit was lifted early, and
    # every waiting runner is woken.
    if (-not (Test-WaitingForSession) -or $LimitProbeMinutes -le 0 -or (Get-Date) -lt $script:NextProbe) { return }
    $script:NextProbe = (Get-Date).AddMinutes($LimitProbeMinutes)
    $reset = $script:Notice.Reset
    if (Test-WakeRequested $reset) { return }
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $env:ComSpec
        $psi.Arguments = "/d /c claude -p --model $ProbeModel --output-format stream-json --verbose --max-turns 1 2>nul"
        $psi.WorkingDirectory = $Root
        $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $p = [System.Diagnostics.Process]::Start($psi)
        $p.StandardInput.Write('Reply with the single word OK.')
        $p.StandardInput.Close()
        $read = $p.StandardOutput.ReadToEndAsync()
        if (-not $p.WaitForExit(120000)) { & taskkill /T /F /PID $p.Id 2>&1 | Out-Null; return }
        $out = $read.Result
    } catch { return }
    $answered = $out -match '"type":"result"' -and $out -notmatch '"status":"rejected"' -and $out -notmatch '"is_error":true'
    if ($answered) {
        Add-LimitMark "wake $reset"
        Write-Trace '-' 'TOKENS' 'probe answered: tokens are back before the reset; waking the lanes' 'Green'
    }
}

function Wait-ForNewSession {
    # Holds this runner until just after the usage limit resets, and returns how long it
    # waited so the shift can add it back. A lone runner announces as it waits; a lane
    # leaves the announcing to the coordinator.
    # -UsageOnly: the tokens are not out, the shift is choosing to start on a fresh session
    # (-StopAtUsage). Then nothing is marked in the limit file and no probe or wake can
    # end the wait: a probe is always answered while tokens remain, so it would start the
    # shift at once, its lanes would stop on the same reading, and -Continuous would start
    # empty shift after empty shift until the reset.
    param([string]$Id, [datetime]$Until, [switch]$UsageOnly)
    $began = Get-Date
    $unix = ConvertTo-Unix $Until
    if (-not $UsageOnly) { Add-LimitMark "reset $unix" }
    # A weekly reset can be days away, so a reset on another day names the day.
    $when = $Until.ToString($(if ($Until.Date -eq (Get-Date).Date) { 'HH:mm' } else { 'ddd HH:mm' }))
    Write-Trace $Id 'tokens' "$(if ($UsageOnly) { 'saving tokens' } else { 'out of tokens' }); waiting for the new session at $when" 'Yellow'
    Set-OwnTabLabel "tokens back $when"
    Write-Heartbeat 'tokens' "new session at $(Get-UtcStamp $Until)"
    # A little past the reset, so the first request lands in the new session.
    $resume = $Until.AddSeconds(20)
    $nextTrace = (Get-Date).AddMinutes(30)
    while ((Get-Date) -lt $resume) {
        Write-HeartbeatIfDue
        # A coordinator waiting to start its shift still watches CI: a failure on the last
        # shift's commits otherwise went unfiled until the reset, hours later (BL-1031).
        if (-not $Lane -and $script:CiWatchBranch) { Invoke-CiWatch -Branch $script:CiWatchBranch }
        if ($UsageOnly) { Start-Sleep -Seconds 5; continue }
        if (Test-WakeRequested $unix) { Write-Trace $Id 'wake' 'tokens are back before the reset' 'Green'; break }
        if ($Lane) { try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane out of tokens until $($Until.ToString('HH:mm'))" } catch { } }
        else { Update-LimitNotice; Invoke-LimitProbe }
        if ((Get-Date) -ge $nextTrace) {
            Write-Trace $Id 'wait' "new session in $(Format-Span ($Until - (Get-Date)))" 'DarkGray'
            $nextTrace = (Get-Date).AddMinutes(30)
        }
        Start-Sleep -Seconds 1
    }
    if ($UsageOnly) { Write-Trace $Id 'resume' 'new session; starting the shift' 'Green'; Set-OwnTabLabel ''; return ((Get-Date) - $began) }
    Add-LimitMark "resumed $unix $Id"
    Write-Trace $Id 'resume' 'new session; running the task again' 'Green'
    Set-OwnTabLabel $(if ($Id -match '^BL-') { "$Id $(Get-TaskTitle $Id)" } else { '' })
    if ($Lane) { try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane" } catch { } }
    else { Update-LimitNotice }
    return ((Get-Date) - $began)
}

function Wait-ForFreshSession {
    # A shift starts on a fresh session: while the weekly window is at least
    # -StopAtWeeklyUsage used, or the 5-hour one -StopAtUsage, announce the reset and wait
    # for it - a notice, not the alarm, since running out is the plan working (BL-806).
    # Returns '' once there is room.
    $u = Get-UsageReading
    if (-not $u) { return '' }
    if ($u.Week -ge $StopAtWeeklyUsage) {
        Write-Trace '-' 'tokens' "weekly tokens $([math]::Round($u.Week * 100))% used; this shift starts when they reset, $($u.WeekResets.ToString('dddd d MMM HH:mm'))" 'Yellow'
        [void](Wait-ForNewSession -Id '-' -Until $u.WeekResets -UsageOnly)
        $u = Get-UsageReading
        if (-not $u) { return '' }
    }
    if ($u.FiveHour -lt $StopAtUsage) { return '' }
    Write-Trace '-' 'tokens' "session tokens $([math]::Round($u.FiveHour * 100))% used; this shift starts on the new session at $($u.FiveHourResets.ToString('HH:mm'))" 'Yellow'
    [void](Wait-ForNewSession -Id '-' -Until $u.FiveHourResets -UsageOnly)
    return ''
}

if ($Wake) {
    # Tells every runner of the newest shift that is waiting for tokens to carry on now.
    $file = Get-ChildItem $LogDir -Filter 'limit-*.txt' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    $resets = if ($file) { @(Get-Content $file.FullName | Where-Object { $_ -match '^reset \d+$' }) } else { @() }
    if (-not $resets.Count) { Write-Host 'No shift is waiting for tokens.'; exit 0 }
    $script:LimitFile = $file.FullName
    $LimitFile = $file.FullName
    Add-LimitMark "wake $(($resets[-1] -split ' ')[1])"
    Write-Host "Woke the shift waiting in $($file.Name)."
    exit 0
}

if ($TestOutOfTokens) {
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
    [void](Wait-ForNewSession -Id 'TEST' -Until (Get-Date).AddSeconds(90))
    if ($script:Voice) { Start-Sleep -Seconds 6 }
    exit 0
}

# ---------------------------------------------------------------------------- board

function Invoke-Board {
    param([string[]]$BoardArgs)
    $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $Board @BoardArgs 2>&1
    return ($out | ForEach-Object { "$_" })
}

function Get-TaskState {
    param([string]$Id)
    foreach ($state in 'Doing', 'Blocked', 'Done', 'Backlog', 'Deferred') {
        if (Get-ChildItem (Join-Path $Root "Tasks\$state") -Filter "$Id-*.md" -ErrorAction SilentlyContinue) { return $state }
    }
    return 'Unknown'
}

function Get-TaskTitle {
    param([string]$Id)
    $file = Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter "$Id-*.md" | Select-Object -First 1
    if (-not $file) { return '' }
    $m = Select-String -Path $file.FullName -Pattern '^title:\s*(.+)$' | Select-Object -First 1
    if ($m) { return $m.Matches[0].Groups[1].Value.Trim('"', "'", ' ') }
    return ''
}

# Task IDs have three digits or more: task-board.ps1 numbers with 'BL-{0:D3}', so BL-999 is
# followed by BL-1000 (BL-992). Every reading of an ID goes through these four.

function Get-TaskIdFromFileName {
    # The ID a task file name starts with: BL-1003-x.md gives BL-1003.
    param([string]$Name)
    if ($Name -match '^(BL-\d+)') { return $Matches[1] }
    return ''
}

function Get-NextTaskId {
    # The ID that task-board.ps1 next printed at the start of a line, or '' when it offered none.
    param([string]$Text)
    if ($Text -match '(?m)^(BL-\d+)\s') { return $Matches[1] }
    return ''
}

function Get-WaitReason {
    # Why task-board.ps1 next offered nothing: its 'No task ...' line, never a WARNING it
    # printed first (a duplicate ID) or that warning's wrapped continuation lines (BL-1054).
    param([string]$Text)
    $reason = @($Text -split "`r?`n" | Where-Object { $_ -match '^No task ' }) | Select-Object -First 1
    if ($reason) { return $reason.Trim() }
    return @($Text -split "`r?`n" | Where-Object { $_ -notmatch '^WARNING:' -and $_ -notmatch '^\s*Tasks[\\/]' }) -join ' '
}

function Get-TaskIdsNamed {
    # Every distinct ID a Log line or -Reason names, in order.
    param([string]$Text)
    return @([regex]::Matches($Text, '\bBL-\d+\b') | ForEach-Object { $_.Value } | Select-Object -Unique)
}

function Get-NeedsStewartId {
    # The ID on a task-board.ps1 status line marked [needs Stewart], or ''.
    param([string]$Line)
    if ($Line -match '^\s+(BL-\d+)\s.*\[needs Stewart\]') { return $Matches[1] }
    return ''
}

if ($TestTaskIds) {
    $cases = @(
        ,@('next offers a four-digit ID', 'BL-1003', (Get-NextTaskId "BL-1003  pipeline: direct  Tasks\Backlog\BL-1003-x.md"))
        ,@('next offers a three-digit ID', 'BL-992', (Get-NextTaskId 'BL-992  pipeline: direct  Tasks\Backlog\BL-992-x.md'))
        ,@('next offers nothing', '', (Get-NextTaskId 'No task is ready.'))
        ,@('a reason names two IDs', 'BL-1003,BL-999', ((Get-TaskIdsNamed 'Waiting on BL-1003 and BL-999') -join ','))
        ,@('a needs-Stewart status line', 'BL-1005', (Get-NeedsStewartId '  BL-1005 Normal Stewart Title  [needs Stewart]'))
        ,@('a file name with a four-digit ID', 'BL-1003', (Get-TaskIdFromFileName 'BL-1003-accept-four-digit-ids.md'))
        ,@('a wait reason behind a wrapped duplicate-ID warning', 'No task can start yet: every ready task overlaps one in Doing or waits behind one that does, e.g. BL-806 with BL-797.',
            (Get-WaitReason "WARNING: Duplicate task ID BL-806: `r`nTasks\Backlog\BL-806-write-curl-s-v-tls-lines.md, `r`nTasks\Done\2026-09-28_1849\BL-806-stop-lanes-auto.md`r`nNo task can start yet: every ready task overlaps one in Doing or waits behind one that does, e.g. BL-806 with BL-797."))
        ,@('a wait reason when nothing is ready, after a warning', 'No task is ready.', (Get-WaitReason "WARNING: Duplicate task ID BL-806: Tasks\Backlog\a.md, Tasks\Done\b.md`nNo task is ready."))
        ,@('a wait reason with no warning', 'No task is ready.', (Get-WaitReason 'No task is ready.')))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

function Get-MedianCost {
    # The median of a list of run costs in US dollars, or 0 for an empty list.
    param([double[]]$Costs)
    $sorted = @($Costs | Sort-Object)
    if ($sorted.Count -eq 0) { return 0.0 }
    $mid = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return [double]$sorted[$mid] }
    return ([double]$sorted[$mid - 1] + [double]$sorted[$mid]) / 2
}

function Get-RunBudgetUsd {
    # The cost cap for the next headless run (AF-0033, ADR-0407): 2.7 times the median of
    # the recent task runs' costs, so a run that ends one turn above it still stays under
    # three times the median, never above -TaskBudgetUsd and never below $2. Fewer than 10
    # recent costs leaves -TaskBudgetUsd as it is; -TaskBudgetUsd 0 means no cap.
    param([double]$Ceiling, [double[]]$RecentCosts)
    if ($Ceiling -le 0) { return 0.0 }
    $costs = @($RecentCosts | Where-Object { $_ -gt 0 })
    if ($costs.Count -lt 10) { return $Ceiling }
    $cap = [math]::Round(2.7 * (Get-MedianCost $costs), 2)
    return [math]::Min($Ceiling, [math]::Max(2.0, $cap))
}

function Get-RecentRunCosts {
    # total_cost_usd from the result events of the newest task runs' logs in a log folder
    # (BL-1377-20261003-145757-L1.jsonl and its -resumed run; resolver runs are not tasks).
    param([string]$Dir, [int]$Count = 40)
    if (-not (Test-Path $Dir)) { return @() }
    $logs = Get-ChildItem $Dir -Filter 'BL-*.jsonl' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^BL-\d+-\d{8}-\d{6}(-L\d+)?(-resumed)?\.jsonl$' } |
        Sort-Object LastWriteTime -Descending | Select-Object -First $Count
    foreach ($log in $logs) {
        $line = Get-Content $log.FullName -Tail 5 -ErrorAction SilentlyContinue | Where-Object { $_ -match '"total_cost_usd"' } | Select-Object -Last 1
        if (-not $line) { continue }
        $evt = try { $line | ConvertFrom-Json } catch { $null }
        if ($evt -and $evt.type -eq 'result') { [double]$evt.total_cost_usd }
    }
}

# ---- the model per task (BL-1705; THE MODEL PER TASK in the header)

$ModelNames = 'haiku', 'sonnet', 'opus'
# The hand-built security and crypto libraries; a task touching one, or its test twin, runs on opus.
$OpusLibraries = 'Curl.Cryptography', 'Curl.Tls', 'Curl.Quic', 'Curl.Kerberos', 'Curl.Ntlm', 'Curl.Protocol.Ssh'
$OpusLibraryPattern = '^(' + (($OpusLibraries | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')(\.(UnitLibrary|UnitTests|IntegrationTests))?([\\/].*)?$'
$script:ModelChoice = @{ Id = ''; Model = ''; Why = '' }

function Get-ModelChoice {
    # The model a task runs on and why: a forced -Model, else the task's own model:, else
    # opus for security, crypto, feature, protocol, retried and CI-fix work and sonnet for
    # the rest. The rule never picks haiku. Warning names an ignored model: value.
    param([string]$Forced, [string]$TaskModel = '', [string]$Pipeline = '', [string]$Priority = '',
        [string[]]$Touches = @(), [string]$Title = '', [switch]$Retried)
    if ($Forced -and $Forced -ne 'auto') { return [pscustomobject]@{ Model = $Forced; Why = '-Model forces it'; Warning = '' } }
    $warning = ''
    if ($TaskModel) {
        if ($TaskModel -in $ModelNames) { return [pscustomobject]@{ Model = $TaskModel.ToLowerInvariant(); Why = 'task model field'; Warning = '' } }
        $warning = "model: $TaskModel is not haiku, sonnet or opus; ignored"
    }
    $library = @($Touches | ForEach-Object { "$_".Trim() } | Where-Object { $_ -match $OpusLibraryPattern }) | Select-Object -First 1
    $why = if ($library) { "touches $library" }
        elseif ($Pipeline -in 'feature', 'protocol') { "$Pipeline pipeline" }
        elseif ($Retried) { 'retry after a run that came back' }
        elseif ($Priority -eq 'High' -and $Title -match '^Fix (CI failure|flaky CI test)') { 'CI failure fix' }
        else { '' }
    if ($why) { return [pscustomobject]@{ Model = 'opus'; Why = $why; Warning = $warning } }
    $why = if ($Pipeline) { "$Pipeline pipeline" } else { 'no pipeline' }
    return [pscustomobject]@{ Model = 'sonnet'; Why = $why; Warning = $warning }
}

function Test-TaskRetried {
    # Whether a task's Log shows an earlier claim that came back, as task-board.ps1 writes
    # it: "Doing -> Backlog" (a requeue, a park, a hand-back) or "Doing -> Blocked" (a
    # failed, stalled or killed run).
    param([string[]]$Lines)
    return [bool](@($Lines | Where-Object { $_ -match '^\s*-\s.*\bDoing -> (Backlog|Blocked)\b' }).Count)
}

function Get-TaskModelChoice {
    # Get-ModelChoice for a task file's lines: its front matter and its Log.
    param([string]$Forced, [string[]]$Lines)
    $fields = @{}
    $dashes = 0
    foreach ($line in $Lines) {
        if ($line -match '^---\s*$') { if (++$dashes -ge 2) { break }; continue }
        if ($dashes -eq 1 -and $line -match '^([A-Za-z-]+):\s*(.*)$') { $fields[$Matches[1]] = $Matches[2].Trim().Trim('"', "'") }
    }
    $touches = @("$($fields['touches'])".Trim('[', ']', ' ') -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    return Get-ModelChoice -Forced $Forced -TaskModel "$($fields['model'])" -Pipeline "$($fields['pipeline'])" -Priority "$($fields['priority'])" `
        -Touches $touches -Title "$($fields['title'])" -Retried:(Test-TaskRetried $Lines)
}

function Set-ModelChoice {
    # Chooses the model for the task about to run and puts it in the heartbeat.
    param([string]$Id)
    $file = Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter "$Id-*.md" | Select-Object -First 1
    $choice = Get-TaskModelChoice -Forced $Model -Lines $(if ($file) { @(Get-Content $file.FullName) } else { @() })
    if ($choice.Warning) { Write-Trace $Id 'model' $choice.Warning 'DarkYellow' }
    $script:ModelChoice = @{ Id = $Id; Model = $choice.Model; Why = $choice.Why }
    $script:Beat.Model = $choice.Model
    $script:Beat.ModelWhy = $choice.Why
}

function Format-ModelChoice {
    # The model and why, for the claim and end trace lines: [model sonnet: docs pipeline].
    if (-not $script:ModelChoice.Model) { return '' }
    return "  [model $($script:ModelChoice.Model): $($script:ModelChoice.Why)]"
}

function Get-ModelCostSummary {
    # Runs, tasks and US dollars per model over the run logs in -Dir whose names match
    # -NamePattern: each log's first line names its model, its result event its cost.
    param([string]$Dir, [string]$NamePattern)
    $byModel = @{}
    foreach ($log in @(Get-ChildItem $Dir -Filter 'BL-*.jsonl' -ErrorAction SilentlyContinue | Where-Object { $_.Name -match $NamePattern })) {
        $head = try { Get-Content $log.FullName -TotalCount 1 | ConvertFrom-Json } catch { $null }
        if (-not $head -or $head.type -ne 'factory' -or -not $head.model) { continue }
        $name = "$($head.model)"
        if (-not $byModel.ContainsKey($name)) { $byModel[$name] = @{ Runs = 0; Tasks = @{}; Usd = 0.0 } }
        $byModel[$name].Runs++
        $byModel[$name].Tasks[(Get-TaskIdFromFileName $log.Name)] = $true
        $line = Get-Content $log.FullName -Tail 5 -ErrorAction SilentlyContinue | Where-Object { $_ -match '"total_cost_usd"' } | Select-Object -Last 1
        $evt = try { $line | ConvertFrom-Json } catch { $null }
        if ($evt -and $evt.type -eq 'result') { $byModel[$name].Usd += [double]$evt.total_cost_usd }
    }
    if (-not $byModel.Count) { return 'models: none' }
    return 'models: ' + ((@($byModel.Keys | Sort-Object) | ForEach-Object {
        "$_ $($byModel[$_].Runs) runs $($byModel[$_].Tasks.Count) tasks `$" + $byModel[$_].Usd.ToString('0.00', [System.Globalization.CultureInfo]::InvariantCulture)
    }) -join '; ')
}

if ($TestModelChoice) {
    function Get-CaseModel {
        param([string]$Forced = 'auto', [string]$TaskModel = '', [string]$Pipeline = 'direct', [string]$Priority = 'Normal', [string[]]$Touches = @('Curl.Cli.UnitLibrary'), [string]$Title = 'Do a thing', [switch]$Retried)
        $c = Get-ModelChoice -Forced $Forced -TaskModel $TaskModel -Pipeline $Pipeline -Priority $Priority -Touches $Touches -Title $Title -Retried:$Retried
        return "$($c.Model)$(if ($c.Warning) { ' (warned)' })"
    }
    function Format-Choice { param($Choice) return "$($Choice.Model): $($Choice.Why)" }
    $cases = @(
        ,@('forced -Model beats the task model field', 'sonnet', (Get-CaseModel -Forced 'sonnet' -TaskModel 'opus' -Pipeline 'feature'))
        ,@('forced haiku is honoured', 'haiku', (Get-CaseModel -Forced 'haiku'))
        ,@('model: haiku wins over the rule', 'haiku', (Get-CaseModel -TaskModel 'haiku' -Pipeline 'feature'))
        ,@('model: sonnet wins over the rule', 'sonnet', (Get-CaseModel -TaskModel 'sonnet' -Touches 'Curl.Tls.UnitLibrary'))
        ,@('model: opus wins over the rule', 'opus', (Get-CaseModel -TaskModel 'Opus' -Pipeline 'docs'))
        ,@('an invalid model: falls back to the rule', 'sonnet (warned)', (Get-CaseModel -TaskModel 'gpt'))
        ,@('an invalid model: on feature work falls back to opus', 'opus (warned)', (Get-CaseModel -TaskModel 'fable' -Pipeline 'feature'))
        ,@('feature pipeline', 'opus', (Get-CaseModel -Pipeline 'feature'))
        ,@('protocol pipeline', 'opus', (Get-CaseModel -Pipeline 'protocol'))
        ,@('a retried task', 'opus', (Get-CaseModel -Retried))
        ,@('a High Fix CI failure task', 'opus', (Get-CaseModel -Priority 'High' -Title 'Fix CI failure Foo_Bar on Linux and macOS'))
        ,@('a High Fix flaky CI test task', 'opus', (Get-CaseModel -Priority 'High' -Title 'Fix flaky CI test Foo_Bar'))
        ,@('a Normal Fix CI failure task', 'sonnet', (Get-CaseModel -Title 'Fix CI failure Foo_Bar'))
        ,@('docs pipeline', 'sonnet', (Get-CaseModel -Pipeline 'docs'))
        ,@('a direct diagnostic-output task', 'sonnet', (Get-CaseModel -Title 'Print the lane diagnostics in the trace' -Touches 'RunDarkFactory.ps1'))
        ,@('a Normal ordinary direct task', 'sonnet', (Get-CaseModel))
        ,@('a lookalike library is not a crypto library', 'sonnet', (Get-CaseModel -Touches 'Curl.TlsSettings.UnitLibrary'))
        ,@('-Model defaults to auto without CLAUDE_MODEL', 'auto', $(if ($env:CLAUDE_MODEL) { 'auto' } else { $Model }))
        ,@('probes use sonnet under auto', 'sonnet', $(if ($Model -eq 'auto') { $ProbeModel } else { 'sonnet' })))
    foreach ($library in $OpusLibraries) {
        $cases += ,@("touches $library.UnitLibrary", 'opus', (Get-CaseModel -Touches 'Curl.Cli.UnitLibrary', "$library.UnitLibrary"))
        $cases += ,@("touches $library.UnitTests", 'opus', (Get-CaseModel -Touches "$library.UnitTests"))
    }
    # The task file reading: front matter, touches list and the Log's retry test.
    $front = @('---', 'id: BL-9', 'title: "Tidy names"', 'priority: Normal', 'pipeline: direct', 'touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]', '---', '## Log', '', '- 2026-10-07: Created.', '- 2026-10-07: Backlog -> Doing.')
    $requeued = $front + @('- 2026-10-07: Doing -> Backlog: depends on BL-8.', '- 2026-10-07: Backlog -> Doing.')
    $blockedOnce = $front + @('- 2026-10-07: Doing -> Blocked: Stewart: dark factory timed out.', '- 2026-10-07: Blocked -> Doing.')
    $cases += ,@('a task file with an ordinary first claim', 'sonnet: direct pipeline', (Format-Choice (Get-TaskModelChoice -Forced 'auto' -Lines $front)))
    $cases += ,@('a task file requeued once', 'opus: retry after a run that came back', (Format-Choice (Get-TaskModelChoice -Forced 'auto' -Lines $requeued)))
    $cases += ,@('a task file blocked by a run', 'opus', (Get-TaskModelChoice -Forced 'auto' -Lines $blockedOnce).Model)
    $cases += ,@('a task file touching Curl.Quic.UnitTests', 'opus', (Get-TaskModelChoice -Forced 'auto' -Lines ($front -replace 'Curl\.Cli\.UnitTests', 'Curl.Quic.UnitTests')).Model)
    $cases += ,@('a task file with model: haiku', 'haiku', (Get-TaskModelChoice -Forced 'auto' -Lines ($front[0..5] + @('model: haiku') + $front[6..10])).Model)
    $cases += ,@('a Log line in the body text is not a retry', 'False', "$(Test-TaskRetried 'Notes say Doing -> Backlog happens.')")
    # -Model is forwarded as given by the shift start and the -Continuous hand-over, and
    # -Restart reuses the coordinator's own command line, so auto stays auto.
    $own = Get-Content -Raw $PSCommandPath
    $cases += ,@('shift start and hand-over forward -Model as given', '2', "$(([regex]::Matches($own, "'-Model', \`$Model\b")).Count)")
    # Cost per model from a made-up shift's logs.
    $dir = Join-Path ([IO.Path]::GetTempPath()) "DarkFactoryModels-$PID"
    New-Item -ItemType Directory -Force $dir | Out-Null
    Set-Content (Join-Path $dir 'BL-1-20261007-100000-L1.jsonl') '{"type":"factory","model":"sonnet","why":"docs pipeline"}', '{"type":"result","total_cost_usd":1.25}'
    Set-Content (Join-Path $dir 'BL-1-20261007-100000-L1-resumed.jsonl') '{"type":"factory","model":"sonnet","why":"docs pipeline"}', '{"type":"result","total_cost_usd":0.5}'
    Set-Content (Join-Path $dir 'BL-2-20261007-100000-L2.jsonl') '{"type":"factory","model":"opus","why":"feature pipeline"}', '{"type":"result","total_cost_usd":4}'
    Set-Content (Join-Path $dir 'BL-3-20261006-100000-L1.jsonl') '{"type":"factory","model":"opus","why":"feature pipeline"}', '{"type":"result","total_cost_usd":9}'
    $cases += ,@('runs, tasks and dollars per model for a shift', 'models: opus 1 runs 1 tasks $4.00; sonnet 2 runs 1 tasks $1.75', (Get-ModelCostSummary $dir '^BL-\d+-20261007-100000(-L\d+)?[-.]'))
    $cases += ,@('the same for one lane', 'models: sonnet 2 runs 1 tasks $1.75', (Get-ModelCostSummary $dir '^BL-\d+-20261007-100000-L1[-.]'))
    $cases += ,@('the model line leaves costs readable', '0.5,1.25,4,9', ((@(Get-RecentRunCosts $dir) | Sort-Object) -join ','))
    Remove-Item -Recurse -Force $dir
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    # No case may give haiku unless haiku was asked for.
    $unasked = @($cases | Where-Object { "$($_[2])" -like 'haiku*' -and $_[0] -notmatch 'haiku' })
    if ($unasked.Count) { Write-Host "FAIL haiku chosen unasked: $($unasked[0][0])" -ForegroundColor Red; $failed++ }
    exit $(if ($failed) { 1 } else { 0 })
}

if ($TestTaskBudget) {
    $twelve = [double[]](1.0, 1.0, 1.0, 1.2, 1.2, 1.3, 1.3, 1.4, 1.5, 2.0, 4.0, 5.4)
    $cases = @(
        ,@('median of an odd list', '2', "$(Get-MedianCost 3.0, 1.0, 2.0)")
        ,@('median of an even list', '1.5', "$(Get-MedianCost 1.0, 2.0, 4.0, 1.0)")
        ,@('median of nothing', '0', "$(Get-MedianCost @())")
        ,@('cap at 2.7 times the median 1.3', '3.51', "$(Get-RunBudgetUsd 6 $twelve)")
        ,@('cap under three times the median with a turn over it', 'True', "$((Get-RunBudgetUsd 6 $twelve) + 0.3 -lt 3 * (Get-MedianCost $twelve))")
        ,@('cap held at -TaskBudgetUsd', '3', "$(Get-RunBudgetUsd 3 $twelve)")
        ,@('cap floor of 2 dollars', '2', "$(Get-RunBudgetUsd 6 ([double[]](0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1)))")
        ,@('fewer than 10 costs keeps -TaskBudgetUsd', '6', "$(Get-RunBudgetUsd 6 ([double[]](1.0, 1.0, 1.0)))")
        ,@('-TaskBudgetUsd 0 is no cap', '0', "$(Get-RunBudgetUsd 0 $twelve)")
        ,@('no log folder gives no costs', '0', "$(@(Get-RecentRunCosts (Join-Path ([IO.Path]::GetTempPath()) 'no-such-dark-factory-logs')).Count)"))
    $dir = Join-Path ([IO.Path]::GetTempPath()) "DarkFactoryBudget-$PID"
    New-Item -ItemType Directory -Force $dir | Out-Null
    Set-Content (Join-Path $dir 'BL-1-20261003-100000-L1.jsonl') '{"type":"assistant"}', '{"type":"result","total_cost_usd":1.25}'
    Set-Content (Join-Path $dir 'BL-2-20261003-100000-L2-resumed.jsonl') '{"total_cost_usd":2.5,"type":"result"}'
    Set-Content (Join-Path $dir 'BL-3-20261003-100000-L3-resolve.jsonl') '{"type":"result","total_cost_usd":9}'
    Set-Content (Join-Path $dir 'BL-4-20261003-100000-L4.jsonl') '{"type":"assistant"}'
    $cases += ,@('costs read from task and resumed runs only', '1.25,2.5', ((@(Get-RecentRunCosts $dir) | Sort-Object) -join ','))
    Remove-Item -Recurse -Force $dir
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

function Get-LastLogLine {
    param([string]$Id)
    $file = Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter "$Id-*.md" | Select-Object -First 1
    if (-not $file) { return '' }
    $log = Get-Content $file.FullName | Where-Object { $_ -match '^\s*-\s' } | Select-Object -Last 1
    return ("$log" -replace '^\s*-\s*', '')
}

function Get-WaitingOnStewart {
    $reasons = @()
    foreach ($f in Get-ChildItem (Join-Path $Root 'Tasks\Blocked') -Filter 'BL-*.md' -ErrorAction SilentlyContinue) {
        $id = Get-TaskIdFromFileName $f.Name
        $reasons += "$id BLOCKED  $(Get-LastLogLine $id)"
    }
    foreach ($line in Invoke-Board @('status')) {
        $id = Get-NeedsStewartId $line
        if ($id) { $reasons += "$id DECIDE   $(Get-TaskTitle $id)" }
    }
    $reasons += @(Get-OpenAuditTaskLines)
    return $reasons
}

function Get-OpenAuditTaskLines {
    # One line per open audit-guard task (BL-999): the ones the CI watch filed this shift, and
    # any still open in Backlog or Doing of this checkout. Each needs an interactive session.
    $open = [ordered]@{}
    if ($script:CiWatch -and $script:CiWatch.AuditTasks) { foreach ($id in $script:CiWatch.AuditTasks.Keys) { $open[$id] = $script:CiWatch.AuditTasks[$id] } }
    foreach ($state in 'Backlog', 'Doing') {
        foreach ($f in Get-ChildItem (Join-Path $Root "Tasks\$state") -Filter 'BL-*.md' -ErrorAction SilentlyContinue) {
            $title = "$(Select-String -LiteralPath $f.FullName -Pattern '^title:\s*(.*)$' | Select-Object -First 1 | ForEach-Object { $_.Matches[0].Groups[1].Value })"
            if ($title -like "Revert the dark factory's change to *") { $open[(Get-TaskIdFromFileName $f.Name)] = $title }
        }
    }
    return @($open.Keys | ForEach-Object { "$_ AUDIT    needs an interactive session: $($open[$_])" })
}

function Test-TaskDone {
    param([string]$Id)
    return [bool](Get-ChildItem (Join-Path $Root 'Tasks\Done') -Recurse -Filter "$Id-*.md" -ErrorAction SilentlyContinue)
}

function Invoke-Requeue {
    # Moves back to Backlog every Blocked task whose blocker was only other tasks that are
    # now all Done: a last Log line that names BL-### IDs and is not a question for
    # Stewart. Returns the IDs it moved.
    $moved = @()
    foreach ($f in Get-ChildItem (Join-Path $Root 'Tasks\Blocked') -Filter 'BL-*.md' -ErrorAction SilentlyContinue) {
        $id = Get-TaskIdFromFileName $f.Name
        $reason = Get-LastLogLine $id
        if ($reason -match 'Stewart') { continue }
        $waits = @(Get-TaskIdsNamed $reason | Where-Object { $_ -ne $id })
        if (-not $waits.Count -or @($waits | Where-Object { -not (Test-TaskDone $_) }).Count) { continue }
        Invoke-Board @('move', '-Id', $id, '-To', 'Backlog', '-Reason', "Unblocked: $($waits -join ', ') now Done") | Out-Null
        if ((Get-TaskState $id) -eq 'Backlog') { $moved += $id; Write-Trace $id 'requeue' "unblocked: $($waits -join ', ') Done" 'Cyan' }
    }
    return $moved
}

# ---------------------------------------------------------------------------- git

function Get-Dirty { return @(git -C $Root status --porcelain) | Where-Object { $_ } }

function Sync-CheckoutWithOrigin {
    # Brings a clean, freshly fetched checkout level with origin/<Branch> before a shift
    # starts. Only behind is fast-forwarded (BL-1141): a lane pushed after the last shift
    # synced. Ahead or diverged needs a person, so it returns the refusal text; $null
    # means the checkout now matches origin.
    param([string]$Repo, [string]$Branch)
    $head = "$(git -C $Repo rev-parse HEAD)".Trim()
    $remote = "$(git -C $Repo rev-parse "origin/$Branch")".Trim()
    if ($head -eq $remote) { return $null }
    git -C $Repo merge-base --is-ancestor $head $remote 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        git -C $Repo merge-base --is-ancestor $remote $head 2>&1 | Out-Null
        $how = if ($LASTEXITCODE -eq 0) { 'is ahead of' } else { 'has diverged from' }
        return "$Branch $how origin/$Branch; push or reconcile first"
    }
    $behind = "$(git -C $Repo rev-list --count "$head..$remote")".Trim()
    git -C $Repo merge -q --ff-only "origin/$Branch" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { return "$Branch is behind origin/$Branch and git merge --ff-only failed; pull first" }
    Write-Trace '-' 'sync' "fast-forwarded $Branch $behind commit(s) to origin/$Branch"
    return $null
}

function Get-FailedTestNames {
    # What a dotnet test run's -Output says failed, as one short line for a trace or a park
    # reason: "Curl.Quic.UnitTests: Loop_A, Loop_B", "a test host aborted", or "no test
    # named" when the run was red without naming one (BL-898).
    param([string[]]$Output)
    $tests = @($Output | ForEach-Object { if ($_ -match '^\s+Failed\s+(\S+)\s+\[') { $Matches[1] } } | Select-Object -Unique)
    $assemblies = @($Output | ForEach-Object { if ($_ -match '^Failed!\s.*?-\s+([\w.]+)\.dll') { $Matches[1] } } | Select-Object -Unique)
    $aborted = [bool]($Output | Where-Object { $_ -match 'test run was aborted|Test host process crashed|hang timeout' })
    $parts = @()
    if ($tests.Count) {
        $shown = ($tests | Select-Object -First 4) -join ', '
        if ($tests.Count -gt 4) { $shown += " and $($tests.Count - 4) more" }
        $parts += $(if ($assemblies.Count) { "$($assemblies -join ', '): $shown" } else { $shown })
    } elseif ($assemblies.Count) { $parts += "$($assemblies -join ', ') failed" }
    if ($aborted) { $parts += 'a test host aborted' }
    if (-not $parts.Count) { return 'no test named' }
    return $parts -join '; '
}

function Get-FailedTestProjects {
    # The test projects a dotnet test run's -Output names as failed, so a red run can be run
    # again with only them (AF-0092). Empty when a test host aborted or the run was red
    # without naming a project: then nothing smaller than the whole run can stand in for it.
    param([string[]]$Output)
    if ($Output | Where-Object { $_ -match 'test run was aborted|Test host process crashed|hang timeout' }) { return @() }
    return @($Output | ForEach-Object { if ($_ -match '^Failed!\s.*?-\s+([\w.]+)\.dll') { $Matches[1] } } | Select-Object -Unique)
}

if ($TestFlakyTests) {
    $cases = @(
        ,@('one named failure', 'Curl.Quic.UnitTests: Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout', (Get-FailedTestNames @(
            'Passed!  - Failed:     0, Passed:    44, Skipped:     0, Total:    44, Duration: 140 ms - Curl.Protocol.Dict.UnitTests.dll (net10.0)',
            '  Failed Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout [484 ms]',
            'Failed!  - Failed:     1, Passed:   398, Skipped:     0, Total:   399, Duration: 1 s - Curl.Quic.UnitTests.dll (net10.0)')))
        ,@('many named failures', 'Curl.Cli.UnitTests, Curl.Core.UnitTests: A, B, C, D and 1 more', (Get-FailedTestNames @(
            '  Failed A [1 ms]', '  Failed B [1 ms]', '  Failed C [1 ms]',
            'Failed!  - Failed:     3, Passed:     1, Skipped:     0, Total:     4, Duration: 1 s - Curl.Cli.UnitTests.dll (net10.0)',
            '  Failed D [1 ms]', '  Failed E [1 ms]',
            'Failed!  - Failed:     2, Passed:     1, Skipped:     0, Total:     3, Duration: 1 s - Curl.Core.UnitTests.dll (net10.0)')))
        ,@('aborted host', 'a test host aborted', (Get-FailedTestNames @('The active test run was aborted. Reason: Test host process crashed')))
        ,@('red without a name', 'no test named', (Get-FailedTestNames @('Passed!  - Failed:     0, Passed:     1 - Curl.Core.UnitTests.dll (net10.0)')))
        ,@('failed projects to rerun alone', 'Curl.Cli.UnitTests,Curl.Cookies.UnitTests', ((Get-FailedTestProjects @(
            '  Failed A [1 ms]',
            'Failed!  - Failed:     1, Passed:     1, Skipped:     0, Total:     2, Duration: 1 s - Curl.Cli.UnitTests.dll (net10.0)',
            'Passed!  - Failed:     0, Passed:     1 - Curl.Core.UnitTests.dll (net10.0)',
            'Failed!  - Failed:     1, Passed:     1, Skipped:     0, Total:     2, Duration: 1 s - Curl.Cookies.UnitTests.dll (net10.0)')) -join ','))
        ,@('no project to rerun after an aborted host', '', ((Get-FailedTestProjects @(
            'Failed!  - Failed:     1, Passed:     1 - Curl.Cli.UnitTests.dll (net10.0)',
            'The active test run was aborted. Reason: Test host process crashed')) -join ','))
        ,@('no project to rerun when none is named', '', ((Get-FailedTestProjects @('Passed!  - Failed:     0, Passed:     1 - Curl.Core.UnitTests.dll (net10.0)')) -join ',')))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

function Restore-ShiftBranch {
    # Switches -Repo back to -Branch when something else checked out another branch under
    # a running shift (BL-809). Returns '' when it is on -Branch, or why it was left alone.
    param([string]$Branch, [string]$Repo = $Root)
    $current = "$(git -C $Repo rev-parse --abbrev-ref HEAD 2>$null)".Trim()
    if ($current -eq $Branch) { return '' }
    if (@(git -C $Repo status --porcelain) | Where-Object { $_ }) {
        return "checkout is on $current, not $Branch, and has uncommitted changes; left alone"
    }
    git -C $Repo switch -q $Branch 2>&1 | Out-Null
    $now = "$(git -C $Repo rev-parse --abbrev-ref HEAD 2>$null)".Trim()
    if ($now -ne $Branch) { return "checkout is on $current and could not be switched to $Branch" }
    Write-Trace '-' 'branch' "checkout was on $current; switched back to $Branch" 'Yellow'
    return ''
}

if ($TestShiftBranch) {
    $repo = Join-Path ([IO.Path]::GetTempPath()) "df-shift-branch-$PID"
    New-Item -ItemType Directory -Force -Path $repo | Out-Null
    git -C $repo init -q -b master 2>&1 | Out-Null
    git -C $repo -c user.name=t -c user.email=t@t commit -q --allow-empty -m one 2>&1 | Out-Null
    git -C $repo branch work 2>&1 | Out-Null
    $failed = 0
    $cases = @(
        ,@('clean switch-back', "'' on work", { $r = Restore-ShiftBranch -Branch work -Repo $repo; "'$r' on $((git -C $repo rev-parse --abbrev-ref HEAD).Trim())" })
        ,@('already on it', "'' on work", { $r = Restore-ShiftBranch -Branch work -Repo $repo; "'$r' on $((git -C $repo rev-parse --abbrev-ref HEAD).Trim())" })
        ,@('dirty refusal', "left alone on master", {
            git -C $repo switch -q master 2>&1 | Out-Null
            Set-Content -Path (Join-Path $repo 'x.txt') -Value 'x'
            $r = Restore-ShiftBranch -Branch work -Repo $repo
            "$(if ($r -match 'left alone$') { 'left alone' } else { $r }) on $((git -C $repo rev-parse --abbrev-ref HEAD).Trim())" }))
    foreach ($case in $cases) {
        $got = & $case[2]
        if ($case[1] -ceq $got) { Write-Host "PASS $($case[0]): $got" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $got" -ForegroundColor Red; $failed++ }
    }
    Remove-Item -Recurse -Force -Path $repo -ErrorAction SilentlyContinue
    exit $(if ($failed) { 1 } else { 0 })
}

function Get-AuditDueFromMaster {
    # The audit office's view of whether an audit is due (BL-1019), from master's copy of
    # Audit/Tools/Test-AuditDue.ps1 - never this branch's, so the factory never runs its own copy
    # of an audit tool. Returns its -Json answer, or $null when master has no copy yet or it fails.
    param([string]$Branch, [string]$MasterRef = 'origin/master')
    $tool = Join-Path ([IO.Path]::GetTempPath()) "Test-AuditDue-$PID.ps1"
    $copy = git -C $Root show "${MasterRef}:Audit/Tools/Test-AuditDue.ps1" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $copy) { return $null }
    try {
        [IO.File]::WriteAllText($tool, ($copy -join "`r`n"))
        $json = & powershell -NoProfile -ExecutionPolicy Bypass -File $tool -Json -Repository $Root -Ref "origin/$Branch" -ScorecardsRef $MasterRef 2>$null
        if ($LASTEXITCODE -ne 0 -or -not $json) { return $null }
        return ($json -join '') | ConvertFrom-Json
    } catch { return $null }
    finally { Remove-Item -LiteralPath $tool -ErrorAction SilentlyContinue }
}

function Get-AuditCadence {
    # What the shift's end does with an audit-due answer: a report line, and whether to hold the
    # merge (only for a roadmap milestone, which is audited before it reaches master).
    param($Due)
    $result = [pscustomobject]@{ Line = ''; HoldMerge = $false; MergeMessage = '' }
    if (-not $Due -or -not $Due.due) { return $result }
    $result.Line = "Audit due: $(@($Due.reasons) -join ', ')"
    $milestone = @($Due.reasons | Where-Object { $_ -match '^milestone:(\d+)$' })[0]
    if ($milestone -and $milestone -match '^milestone:(\d+)$') {
        $result.HoldMerge = $true
        $result.MergeMessage = "not merged: audit due before the Milestone $($Matches[1]) merge (run Audit\RunAudit.cmd)"
    }
    return $result
}

function Get-ShiftStartDecision {
    # Whether a shift may start while an audit runs: start, refuse, or (with -Continuous) wait.
    param([object[]]$Processes, [bool]$IsContinuous)
    $audits = @($Processes | Where-Object { $_.CommandLine -match 'RunAudit\.ps1' -and $_.CommandLine -notmatch '\s-(NewTab|DryRun|SelfTest)\b' })
    if (-not $audits.Count) { return 'start' }
    if ($IsContinuous) { return 'wait' }
    return 'refuse'
}

function Show-AuditNotice {
    # A one-off notice, not the alarm: an audit is due, said once at the end of the shift.
    param([string]$Line)
    Write-Host ("  $Line  ".PadRight(78)) -ForegroundColor Black -BackgroundColor Cyan
    Write-Trace '-' 'AUDIT' $Line 'Cyan'
    if (Test-AlarmSilent) { return }
    Invoke-Chime
    $voice = Get-Voice
    if ($voice) { $voice.SpeakAsyncCancelAll(); [void]$voice.SpeakAsync("Curl dark factory: $($Line.ToLowerInvariant() -replace ':', '.' -replace 'milestone\.', 'milestone ')") }
}

if ($TestAuditCadence) {
    $failed = 0
    $check = { param([string]$Name, [bool]$Ok, [string]$Detail) if (-not $Ok) { $script:auditCadenceFailed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" -ForegroundColor $(if ($Ok) { 'Green' } else { 'Red' }) }
    $script:auditCadenceFailed = 0
    $none = Get-AuditDueFromMaster -Branch 'work/dark-factory' -MasterRef 'refs/heads/no-such-branch-for-the-test'
    $c = Get-AuditCadence $none
    & $check 'no copy on master skips silently' (($null -eq $none) -and -not $c.Line -and -not $c.HoldMerge) 'nothing reported, merge proceeds'
    $c = Get-AuditCadence ([pscustomobject]@{ due = $true; reasons = @('factory-script') })
    & $check 'factory-script is reported and the merge proceeds' ($c.Line -eq 'Audit due: factory-script' -and -not $c.HoldMerge) $c.Line
    $c = Get-AuditCadence ([pscustomobject]@{ due = $true; reasons = @('factory-script', 'milestone:3') })
    & $check 'milestone:3 is reported and the merge is skipped' ($c.Line -eq 'Audit due: factory-script, milestone:3' -and $c.HoldMerge -and $c.MergeMessage -eq 'not merged: audit due before the Milestone 3 merge (run Audit\RunAudit.cmd)') $c.MergeMessage
    $c = Get-AuditCadence ([pscustomobject]@{ due = $false; reasons = @() })
    & $check 'No audit due adds nothing' (-not $c.Line -and -not $c.HoldMerge) 'nothing'
    $audit = [pscustomobject]@{ CommandLine = 'powershell -NoProfile -File Z:\repos\Curl\Audit\RunAudit.ps1 -Auditors truthfulness' }
    $dry = [pscustomobject]@{ CommandLine = 'powershell -File Z:\repos\Curl\Audit\RunAudit.ps1 -DryRun' }
    & $check 'a running audit refuses a shift start' ((Get-ShiftStartDecision @($audit) $false) -eq 'refuse') 'refuse'
    & $check 'with -Continuous it waits instead' ((Get-ShiftStartDecision @($audit) $true) -eq 'wait') 'wait'
    & $check 'a dry run or no audit lets the shift start' ((Get-ShiftStartDecision @($dry) $false) -eq 'start') 'start'
    exit $(if ($script:auditCadenceFailed) { 1 } else { 0 })
}

if ($TestAudioOff) {
    # Proves the audio-off file silences the alarm and every notice (BL-1456) without a sound:
    # the chime, siren, volume and voice are replaced by recorders, and the file is a
    # throwaway one in a temporary folder, never Stewart's real switch.
    $script:audioOffFailed = 0
    $check = { param([string]$Name, [bool]$Ok, [string]$Detail) if (-not $Ok) { $script:audioOffFailed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" -ForegroundColor $(if ($Ok) { 'Green' } else { 'Red' }) }
    $script:Sounds = [System.Collections.Generic.List[string]]::new()
    function Invoke-Chime { $script:Sounds.Add('chime') }
    function Invoke-Siren { param([int]$Sweeps) $script:Sounds.Add("siren $Sweeps") }
    function Set-AlarmVolume { $script:Sounds.Add('volume') }
    function Get-Voice {
        $v = [pscustomobject]@{ Volume = 0; Rate = 0 }
        $v | Add-Member ScriptMethod SpeakAsyncCancelAll { }
        $v | Add-Member ScriptMethod SpeakAsync { param($Text) $script:Sounds.Add('speech') }
        return $v
    }
    $QuietAlarm = $false
    $dir = Join-Path ([IO.Path]::GetTempPath()) "curl-audio-off-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    New-Item -ItemType Directory -Path $dir | Out-Null
    $script:AudioOffFile = Join-Path $dir 'audio-off'
    $makeSounds = {
        $script:Sounds.Clear()
        Invoke-AlarmSound -Reasons @('BL-004 DECIDE   Decide the licence') -Stage 3
        Show-LimitNotice 'OUT OF TOKENS' 'Rehearsal.' 'Rehearsal.' 'Yellow'
        Show-AuditNotice 'Rehearsal of the due notice'
        return ($script:Sounds -join ', ')
    }
    $loud = 'volume, siren 3, speech, chime, speech, chime, speech'
    try {
        $heard = & $makeSounds
        & $check 'without the file the alarm and notices sound as before' ($heard -eq $loud) $heard
        Set-Content -LiteralPath $script:AudioOffFile -Value ''
        $heard = & $makeSounds
        & $check 'with the file nothing plays, speaks or changes the volume' (-not $heard) "'$heard'"
        Remove-Item -LiteralPath $script:AudioOffFile
        $heard = & $makeSounds
        & $check 'deleting the file while running brings the sound back' ($heard -eq $loud) $heard
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
    exit $(if ($script:audioOffFailed) { 1 } else { 0 })
}

function Invoke-MergeToMaster {
    # Stewart's standing permission (2026-09-27): at the end of a shift, merge the branch
    # into master through a pull request - only when the CI workflow passed, on every
    # platform, for the exact commit being merged. Returns a line for the trace.
    param([string]$Branch)
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { return 'not merged: gh is not installed' }
    git -C $Root fetch -q origin master $Branch 2>&1 | Out-Null
    if ([int](git -C $Root rev-list --count "origin/master..origin/$Branch") -eq 0) { return "nothing on $Branch to merge" }
    $head = (git -C $Root rev-parse "origin/$Branch").Trim()
    $short = $head.Substring(0, 7)
    # CI on the last push takes a few minutes; wait for the run on this exact commit.
    $deadline = (Get-Date).AddMinutes(30)
    $run = $null
    while ($true) {
        $run = @(gh run list --workflow CI --branch $Branch --commit $head --limit 1 --json status,conclusion 2>$null | ConvertFrom-Json)
        if ($run.Count -and $run[0].status -eq 'completed') { break }
        if ((Get-Date) -ge $deadline) { return "not merged: CI did not finish on $short within 30 min" }
        Start-Sleep -Seconds 30
    }
    if ($run[0].conclusion -ne 'success') { return "not merged: CI $($run[0].conclusion) on $short" }
    # --jq, not ConvertFrom-Json: Windows PowerShell turns "[]" into one empty element, so
    # a missing pull request looked like one with no number and the merge was a silent no-op.
    $number = "$(gh pr list --head $Branch --base master --state open --json number --limit 1 --jq '.[0].number // empty' 2>$null)".Trim()
    if (-not $number) {
        $bodyFile = Join-Path $LogDir "pr-body-$Stamp.md"
        $robot = [char]::ConvertFromUtf32(0x1F916)
        [IO.File]::WriteAllText($bodyFile, "Dark factory shift $Stamp. CI passed on Windows, Linux and macOS for $short.`n`n$robot Generated with [Claude Code](https://claude.com/claude-code)`n", (New-Object Text.UTF8Encoding($false)))
        $url = gh pr create --base master --head $Branch --title "Dark factory shift $Stamp" --body-file $bodyFile 2>$null
        if ("$url" -notmatch '/pull/(\d+)') { return 'not merged: could not open the pull request' }
        $number = $Matches[1]
    }
    gh pr merge $number --merge 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { return "not merged: gh pr merge refused pull request #$number" }
    return "merged $short into master (pull request #$number)"
}

# ---------------------------------------------------------------------------- CI watch

# Lanes test only on Windows, so a Linux- or macOS-only break used to sit unnoticed until
# the shift-end merge refused it (BL-987). The coordinator reads every finished CI run on
# the shift's branch and files a High task for each new failure. Runs are cached by id;
# Settled holds "<key>|<run id>" pairs already filed or found covered, so each is decided once.
$CiWatchDir = Join-Path $LanesDir 'ci-watch'
# Settled maps each pair to what became of it ("filed BL-###", "covered by a task"); Logged
# holds the last line traced for each run, so a run is traced again only when that changes.
$script:CiWatch = @{ CheckedAt = [datetime]::MinValue; Failures = @{}; Settled = @{}; Logged = @{}; AuditTasks = @{}; Off = $false }
# The shift's branch once the coordinator knows it, so a wait for tokens keeps watching CI.
$script:CiWatchBranch = ''
# How many finished runs the flaky and regression verdicts look back over, and how long a
# failure seen on one platform in only the newest run waits for the next run to confirm it.
$CiWindowRuns = 6
$CiConfirmMinutes = 30

function Get-CiPlatform {
    # "Build and test (ubuntu-latest)" -> Linux; the platform's name, or the job's when unknown.
    param([string]$Job)
    if ($Job -match 'ubuntu|linux') { return 'Linux' }
    if ($Job -match 'macos') { return 'macOS' }
    if ($Job -match 'windows') { return 'Windows' }
    return $Job
}

function Get-CiFailures {
    # What `gh run view <id> --log-failed` says failed: one object per failing test ("Failed
    # <TestName> [..]") or, when the build broke, per compiler error ("error CS1002 in X.cs"),
    # with the platforms it failed on, its test project (or the project that did not build)
    # and the first line of its error message.
    param([string[]]$Log)
    $found = [ordered]@{}
    $current = @{}
    $awaitingMessage = @{}
    foreach ($raw in $Log) {
        $parts = "$raw" -split "`t", 3
        if ($parts.Count -lt 3) { continue }
        $job = $parts[0]
        $text = $parts[2] -replace '^\d{4}-\d\d-\d\dT[\d:.]+Z ?', ''
        $platform = Get-CiPlatform $job
        $item = $null
        if ($text -match '^\s+Failed\s+(\S+)\s') {
            $item = [pscustomobject]@{ Key = $Matches[1]; Kind = 'test'; Platforms = @(); Project = ''; Message = '' }
        } elseif ($text -match '(?:^|\s)(?:[^\s(]*[/\\])?([^\s/\\(]+)\(\d+,\d+\):\s+error\s+(\w+):\s*(.*?)(?:\s+\[(?:[^\]]*[/\\])?([^\]/\\]+)\.\w+proj\])?\s*$') {
            $item = [pscustomobject]@{ Key = "error $($Matches[2]) in $($Matches[1])"; Kind = 'build'; Platforms = @(); Project = "$($Matches[4])"; Message = $Matches[3] }
        } elseif ($text -match '^Audit guard: (\S+) changed on ') {
            # The audit guard (BL-998) found the factory's own change to an audit path or a guard.
            $item = [pscustomobject]@{ Key = "audit guard $($Matches[1])"; Kind = 'audit'; Platforms = @(); Project = ''; Message = $text.Trim() }
        }
        if ($item) {
            if (-not $found.Contains($item.Key)) { $found[$item.Key] = $item }
            $entry = $found[$item.Key]
            if ($entry.Platforms -notcontains $platform) { $entry.Platforms += $platform }
            if ($item.Kind -eq 'test') { $current[$job] = $entry; $awaitingMessage[$job] = $false }
            continue
        }
        $entry = $current[$job]
        if (-not $entry) { continue }
        if ($text -match '^\s+Error Message:') { $awaitingMessage[$job] = $true; continue }
        if ($awaitingMessage[$job] -and $text.Trim()) {
            if (-not $entry.Message) { $entry.Message = $text.Trim() }
            $awaitingMessage[$job] = $false
            continue
        }
        # A stack-trace frame under a test project's folder, either suffix: *.UnitTests or *.IntegrationTests.
        if (-not $entry.Project -and $text -match '\sin\s.*?[/\\](Curl[\w.]*\.(?:UnitTests|IntegrationTests))[/\\]') { $entry.Project = $Matches[1] }
        if ($text -match '^Failed!\s.*-\s+([\w.]+)\.dll') {
            if (-not $entry.Project) { $entry.Project = $Matches[1] }
            $current[$job] = $null
        }
    }
    return @($found.Values)
}

function Get-CiVerdicts {
    # Which failures to file, from -Runs: the finished CI runs, newest first, each with Id,
    # Sha, FinishedAt and Failures (Get-CiFailures; empty for a green run). A failure is a
    # regression when it fails in the newest run and either broke the build, failed on two
    # platforms, failed in the run before too, or has waited -ConfirmMinutes for another run;
    # it is flaky when it failed once and a run on each side of that one passed it, or failed
    # in the newest run after passing in the one before. Anything else waits or is already fixed.
    param([object[]]$Runs, [datetime]$Now = (Get-Date), [int]$ConfirmMinutes = $CiConfirmMinutes)
    $Runs = @($Runs)
    $verdicts = @()
    $keys = @($Runs | ForEach-Object { @($_.Failures) } | Where-Object { $_ } | ForEach-Object { $_.Key } | Select-Object -Unique)
    foreach ($key in $keys) {
        $failing = @(0..($Runs.Count - 1) | Where-Object { @($Runs[$_].Failures | Where-Object { $_.Key -ceq $key }).Count })
        $at = $failing[0]
        $failure = @($Runs[$at].Failures | Where-Object { $_.Key -ceq $key })[0]
        $verdict = ''
        if ($at -eq 0) {
            if ($failure.Kind -in 'build', 'audit' -or @($failure.Platforms).Count -ge 2 -or $failing -contains 1) { $verdict = 'regression' }
            elseif ($failing.Count -gt 1) { $verdict = 'flaky' }
            elseif (($Now - $Runs[0].FinishedAt).TotalMinutes -ge $ConfirmMinutes) { $verdict = 'regression' }
        } elseif ($failing.Count -eq 1 -and $at -lt $Runs.Count - 1) { $verdict = 'flaky' }
        if (-not $verdict) { continue }
        # A regression's first failing commit is the oldest run of its unbroken red streak.
        $first = 0
        $platforms = @($failure.Platforms)
        if ($verdict -eq 'regression') {
            while ($failing -contains ($first + 1)) {
                $first++
                $platforms += @(@($Runs[$first].Failures | Where-Object { $_.Key -ceq $key })[0].Platforms)
            }
        } else { $first = $at }
        $verdicts += [pscustomobject]@{
            Key = $key; Kind = $failure.Kind; Verdict = $verdict; RunId = $Runs[$at].Id; FirstSha = $Runs[$first].Sha
            Platforms = @($platforms | Select-Object -Unique | Sort-Object); Project = $failure.Project; Message = $failure.Message
        }
    }
    return $verdicts
}

function Get-CiTaskTitle {
    # The task's title; each names the test or error exactly, which is how a later run finds it.
    param($Verdict)
    $on = ($Verdict.Platforms -join ' and ')
    if ($Verdict.Kind -eq 'audit') { return "Revert the dark factory's change to $(Get-CiAuditPath $Verdict.Key)" }
    if ($Verdict.Kind -eq 'build') { return "Fix CI build $($Verdict.Key) on $on" }
    if ($Verdict.Verdict -eq 'flaky') { return "Fix flaky CI test $($Verdict.Key) that failed once on $on" }
    return "Fix CI failure $($Verdict.Key) on $on"
}

function Get-CiAuditPath {
    # The path an audit-guard failure's key names: "audit guard Audit/x.md" -> Audit/x.md.
    param([string]$Key)
    return ($Key -replace '^audit guard ', '')
}

function Get-CiTaskNewArgs {
    # The task-board.ps1 new arguments that file -Verdict. An audit-guard failure is filed
    # -NoLane: a lane cannot touch the path (BL-997) and the board lets a factory process file
    # an audit-path task only that way (BL-996).
    param($Verdict, [string[]]$Touches)
    if ($Verdict.Kind -eq 'audit') {
        return @('new', '-Title', (Get-CiTaskTitle $Verdict), '-Priority', 'High', '-Pipeline', 'direct',
            '-Touches', (Get-CiAuditPath $Verdict.Key), '-NoLane')
    }
    $newArgs = @('new', '-Title', (Get-CiTaskTitle $Verdict), '-Priority', 'High', '-Pipeline', 'feature')
    if (@($Touches).Count) { $newArgs += @('-Touches', (@($Touches) -join ',')) }
    return $newArgs
}

function Get-CiTouches {
    # The test project and the library it tests, or for a build error the project that did not
    # build and its twin, as far as they exist in -Repo. Empty when the project is unknown.
    # Both test-project suffixes map to their library: X.UnitTests and X.IntegrationTests each
    # give X.UnitLibrary and X (the latter for Curl.Console).
    param([string]$Project, [string]$Repo)
    if (-not $Project) { return @() }
    $pair = @($Project)
    if ($Project -match '^(.+)\.(?:UnitTests|IntegrationTests)$') { $pair += @("$($Matches[1]).UnitLibrary", $Matches[1]) }
    elseif ($Project -match '^(.+)\.UnitLibrary$') { $pair += "$($Matches[1]).UnitTests" }
    else { $pair += "$Project.UnitTests" }
    return @($pair | Where-Object { Test-Path (Join-Path $Repo $_) -PathType Container })
}

function Set-CiTaskBody {
    # Fills the new task file's Goal, Context and Acceptance criteria.
    param([string]$Path, $Verdict, [string]$RunUrl)
    if ($Verdict.Kind -eq 'audit') { Set-CiAuditTaskBody -Path $Path -Verdict $Verdict -RunUrl $RunUrl; return }
    $on = $Verdict.Platforms -join ' and '
    $what = if ($Verdict.Kind -eq 'build') { "the build ($($Verdict.Key))" } else { "``$($Verdict.Key)``" }
    $goal = if ($Verdict.Verdict -eq 'flaky') {
        "$what passes on every run on Windows, Linux and macOS; it failed once on $on in the ``CI`` workflow and passed on the runs either side."
    } else { "$what passes on Windows, Linux and macOS, so the ``CI`` workflow on the shift's branch is green again." }
    $message = if ($Verdict.Message) { $Verdict.Message } else { '(no error message in the log)' }
    $first = if ($Verdict.FirstSha) { "First failing commit: $($Verdict.FirstSha.Substring(0, [math]::Min(8, $Verdict.FirstSha.Length)))." } else { 'First failing commit: not found.' }
    $context = @(
        "Filed by the dark factory's CI watch (BL-987). $what failed on $on in CI run $($Verdict.RunId) ($RunUrl). $first"
        ''
        "    $message"
        ''
        "Lanes test only on Windows, so reproduce with ``gh run view $($Verdict.RunId) --log-failed`` and fix it platform-neutrally (CLAUDE.md, ""Tests pass on Windows, Linux and macOS"")."
    ) -join "`n"
    $criteria = if ($Verdict.Verdict -eq 'flaky') {
        "- [ ] What made $what fail intermittently is named under Notes and removed.`n- [ ] $what passes locally, and in the ``CI`` workflow on Windows, Linux and macOS for the commit that lands the fix."
    } else { "- [ ] $what passes locally, and the ``CI`` workflow passes on Windows, Linux and macOS for the commit that lands the fix." }
    $text = [IO.File]::ReadAllText($Path)
    $text = $text.Replace('<!-- One sentence: the observable outcome once this task is done. -->', $goal)
    $text = $text -replace '<!-- Why this matters.*?-->', $context.Replace('$', '$$')
    $text = $text.Replace('- [ ] <!-- A statement someone else can check from the repository without asking a question. -->', $criteria)
    [IO.File]::WriteAllText($Path, $text, (New-Object Text.UTF8Encoding $false))
}

function Set-CiAuditTaskBody {
    # Fills an audit-guard task's Goal, Context and Acceptance criteria. The Context names the
    # watch's key, "audit guard <path>", which is how a later run finds the task.
    param([string]$Path, $Verdict, [string]$RunUrl)
    $target = Get-CiAuditPath $Verdict.Key
    $first = if ($Verdict.FirstSha) { "First failing commit: $($Verdict.FirstSha.Substring(0, [math]::Min(8, $Verdict.FirstSha.Length)))." } else { 'First failing commit: not found.' }
    $goal = "The ``audit-guard`` job passes on work/dark-factory again: ``$target`` there matches master, or its change reaches master through the audit branch."
    $context = @(
        "Filed by the dark factory's CI watch (BL-999) for $($Verdict.Key): the ``audit-guard`` job failed in CI run $($Verdict.RunId) ($RunUrl). $first"
        ''
        "    $($Verdict.Message)"
        ''
        "Audit paths and their guards change only through the audit branch (ADR-0267). Interactive only: a dark factory lane cannot touch ``$target`` (BL-997). Red CI blocks the shift-end merge until this is fixed. Decide whether the change was wanted: if not, restore master's copy on work/dark-factory (``git checkout origin/master -- $target``); if it was, land it on master through the audit branch's pull request, then merge master into work/dark-factory."
    ) -join "`n"
    $criteria = "- [ ] The ``audit-guard`` job passes on work/dark-factory for the commit that lands the fix."
    $text = [IO.File]::ReadAllText($Path)
    $text = $text.Replace('<!-- One sentence: the observable outcome once this task is done. -->', $goal)
    $text = $text -replace '<!-- Why this matters.*?-->', $context.Replace('$', '$$')
    $text = $text.Replace('- [ ] <!-- A statement someone else can check from the repository without asking a question. -->', $criteria)
    [IO.File]::WriteAllText($Path, $text, (New-Object Text.UTF8Encoding $false))
}

function Test-CiFailureCovered {
    # Whether a task already covers -Key failing in the run on -Sha: a live task (Backlog, Doing,
    # Blocked) names it, or a finished one does whose last commit that run did not yet contain,
    # so the failure predates its fix. Read in the -Repo worktree.
    param([string]$Key, [string]$Sha, [string]$Repo)
    $tasks = Join-Path $Repo 'Tasks'
    foreach ($file in @(Get-ChildItem $tasks -Recurse -Filter 'BL-*.md' -ErrorAction SilentlyContinue)) {
        # The whole name only: a task for Parse_Empty does not cover Parse_Empty_Throws.
        if (-not (Select-String -LiteralPath $file.FullName -Pattern "(?<![\w.])$([regex]::Escape($Key))(?![\w])" -CaseSensitive -Quiet)) { continue }
        $state = (Split-Path (Split-Path $file.FullName -Parent) -Leaf)
        if ($state -in 'Backlog', 'Doing', 'Blocked') { return $true }
        $relative = $file.FullName.Substring($Repo.TrimEnd('\', '/').Length + 1)
        $last = "$(git -C $Repo log -1 --format=%H -- $relative 2>$null)".Trim()
        if (-not $last) { continue }
        git -C $Repo merge-base --is-ancestor $last $Sha 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { return $true }
    }
    return $false
}

function Send-CiWhisper {
    # Has the whisper hook announce a filed task, as it would for a Claude session's
    # task-board.ps1 new, without waiting for the speech.
    param([string]$Line)
    $hook = Join-Path $Root '.claude\hooks\whisper-milestone.ps1'
    if (-not (Test-Path $hook)) { return }
    try {
        $stdinFile = Join-Path $LogDir "ci-whisper-$Stamp-$([guid]::NewGuid().ToString('N').Substring(0, 8)).json"
        $json = [pscustomobject]@{ cwd = $Root; tool_input = @{ command = 'task-board.ps1 new' }; tool_response = @{ stdout = $Line } } | ConvertTo-Json -Compress
        [IO.File]::WriteAllText($stdinFile, $json, (New-Object Text.UTF8Encoding $false))
        Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$hook`"") `
            -RedirectStandardInput $stdinFile -WindowStyle Hidden | Out-Null
    } catch { }
}

function Get-CiRuns {
    # The newest finished CI runs on -Branch that passed or failed (cancelled ones prove
    # nothing), newest first, each with its failures; a failed run whose log names no
    # failure, or cannot be read, is left out and traced (Write-CiRunLine). $null when gh
    # cannot list them. -ListRuns and -ReadLog stand in for gh in -TestCiWatch: the first
    # returns `gh run list`'s JSON, the second a run's `--log-failed` lines, $null if unreadable.
    param([string]$Branch, [scriptblock]$ListRuns, [scriptblock]$ReadLog)
    # No --jq: Windows PowerShell strips the double quotes a jq filter needs from a native
    # command's arguments. Its ConvertFrom-Json turns "[]" into one empty element, hence the
    # databaseId filter.
    if (-not $ListRuns) {
        $ListRuns = {
            param([string]$Branch)
            $json = (gh run list --workflow CI --branch $Branch --limit 30 --json databaseId,status,conclusion,headSha,updatedAt 2>$null) -join "`n"
            if ($LASTEXITCODE -ne 0) { return $null }
            return $json
        }
    }
    if (-not $ReadLog) {
        $ReadLog = {
            param([string]$Id)
            $log = @(gh run view $Id --log-failed 2>$null | ForEach-Object { "$_" })
            if ($LASTEXITCODE -ne 0) { return $null }
            return , $log
        }
    }
    $json = & $ListRuns $Branch
    if ($null -eq $json) { Write-CiRunLine -Key 'list' -Line "cannot list the CI runs on $Branch with gh"; return $null }
    try { $listed = @($json | ConvertFrom-Json | ForEach-Object { $_ } | Where-Object { $_.databaseId }) }
    catch { Write-CiRunLine -Key 'list' -Line "cannot read gh's list of CI runs on $Branch"; return $null }
    $runs = @()
    foreach ($listedRun in $listed) {
        if ($runs.Count -ge $CiWindowRuns) { break }
        if ($listedRun.status -ne 'completed' -or $listedRun.conclusion -notin 'success', 'failure') { continue }
        $id = "$($listedRun.databaseId)"; $conclusion = $listedRun.conclusion; $sha = "$($listedRun.headSha)"
        $on = $sha.Substring(0, [math]::Min(8, $sha.Length))
        $finished = if ($listedRun.updatedAt -is [datetime]) { $listedRun.updatedAt.ToLocalTime() }
            else { [datetime]::Parse("$($listedRun.updatedAt)", [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal).ToLocalTime() }
        if ($conclusion -eq 'failure' -and -not $script:CiWatch.Failures.ContainsKey($id)) {
            $log = & $ReadLog $id
            if ($null -eq $log) { Write-CiRunLine -RunId $id -Line "failure on ${on}: cannot read its log with gh run view --log-failed; trying again next heartbeat"; continue }
            $script:CiWatch.Failures[$id] = @(Get-CiFailures $log)
        }
        # Assigned apart from the if: an if statement's output is unrolled, so one failure came
        # back as a bare object, and Windows PowerShell gives a bare object no Count - a run
        # with a single failing test looked like one with none and was dropped (BL-1031).
        $failures = @()
        if ($conclusion -eq 'failure') { $failures = @($script:CiWatch.Failures[$id]) }
        if ($conclusion -eq 'failure' -and -not $failures.Count) {
            Write-CiRunLine -RunId $id -Line "failure on ${on}: its log names no failing test or build error; nothing to file"
            continue
        }
        $runs += [pscustomobject]@{ Id = $id; Sha = $sha; FinishedAt = $finished; Failures = $failures }
    }
    return , $runs
}

function Write-CiRunLine {
    # Traces what the watch made of one run: the first time it examines it, and again whenever
    # that changes (waiting, then filed), not on every heartbeat that finds the same.
    # -Key, without -RunId, names what is traced when it is not a run, such as gh's run list.
    param([string]$RunId, [string]$Line, [string]$Key = $RunId)
    if ($script:CiWatch.Logged[$Key] -ceq $Line) { return }
    $script:CiWatch.Logged[$Key] = $Line
    $text = if ($RunId) { "run $RunId $Line" } else { $Line }
    Write-Trace '-' 'ci' $text 'DarkGray'
}

function Get-CiRunOutcome {
    # One line on what the watch did with -Run, one of -Runs (newest first): nothing for a
    # green run; for a red one, each failure and whether it was filed (-Settled holds
    # "<key>|<run id>" -> "filed BL-###" or "covered by a task"), is filed with a newer run,
    # waits for a run to confirm it, or has passed since.
    param($Run, [object[]]$Runs, [object[]]$Verdicts, [hashtable]$Settled, [int]$ConfirmMinutes = $CiConfirmMinutes)
    $on = "$($Run.Sha)".Substring(0, [math]::Min(8, "$($Run.Sha)".Length))
    $failures = @($Run.Failures | Where-Object { $_ })
    if (-not $failures.Count) { return "success on ${on}: nothing to file" }
    $said = foreach ($failure in $failures) {
        $verdict = @($Verdicts | Where-Object { $_ -and $_.Key -ceq $failure.Key })[0]
        $what = if ($verdict -and $verdict.RunId -ne $Run.Id) { "filed with run $($verdict.RunId)" }
            elseif ($verdict -and $Settled.ContainsKey("$($failure.Key)|$($Run.Id)")) { "$($verdict.Verdict), $($Settled["$($failure.Key)|$($Run.Id)"])" }
            elseif ($verdict) { "$($verdict.Verdict), not filed yet" }
            elseif (@($Runs)[0].Id -eq $Run.Id) { "on $($failure.Platforms -join '+') only, waiting for the next run or $($Run.FinishedAt.AddMinutes($ConfirmMinutes).ToString('HH:mm')) to confirm it" }
            else { 'passed since, not filed' }
        "$($failure.Key) $what"
    }
    return "failure on ${on}: $(@($said) -join '; ')"
}

function Invoke-CiWatch {
    # Once a heartbeat (-HeartbeatMinutes, 3 when publishing is off): reads the finished CI
    # runs on -Branch and files one High task per new failing test or build error, in the
    # ci-watch worktree at origin/-Branch, pushed straight to -Branch, and traces what it made
    # of each run (Write-CiRunLine). Never stops the shift.
    # -Final: the shift's last look, after the merge has waited for CI on its last commit. It
    # runs whatever the heartbeat, and files a failure seen on one platform in only the newest
    # run at once rather than waiting for a run this shift will not be there to read (BL-1031).
    param([string]$Branch, [switch]$Final)
    if ($script:CiWatch.Off) { return }
    $every = if ($HeartbeatMinutes -gt 0) { $HeartbeatMinutes } else { 3 }
    if (-not $Final -and ((Get-Date) - $script:CiWatch.CheckedAt).TotalMinutes -lt $every) { return }
    $script:CiWatch.CheckedAt = Get-Date
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        $script:CiWatch.Off = $true
        Write-Trace '-' 'ci' 'CI watch off: gh is not installed' 'DarkYellow'
        return
    }
    $runs = Get-CiRuns -Branch $Branch
    if ($null -eq $runs) { return }
    $confirm = if ($Final) { 0 } else { $CiConfirmMinutes }
    $all = @(Get-CiVerdicts -Runs $runs -ConfirmMinutes $confirm)
    $verdicts = @($all | Where-Object { -not $script:CiWatch.Settled.ContainsKey("$($_.Key)|$($_.RunId)") })
    if ($verdicts.Count) { New-CiFailureTasks -Branch $Branch -Runs $runs -Verdicts $verdicts }
    foreach ($run in $runs) {
        Write-CiRunLine -RunId $run.Id -Line (Get-CiRunOutcome -Run $run -Runs $runs -Verdicts $all -Settled $script:CiWatch.Settled -ConfirmMinutes $confirm)
    }
}

function New-CiFailureTasks {
    # Files one High task per -Verdicts entry not already covered by a task, in the ci-watch
    # worktree at origin/-Branch, and pushes them straight to -Branch; up to three tries when
    # the push races a lane. Records each in $script:CiWatch.Settled.
    param([string]$Branch, [object[]]$Runs, [object[]]$Verdicts)
    $saved = $env:CLAUDE_PROJECT_DIR
    try {
        foreach ($attempt in 1..3) {
            git -C $Root fetch -q origin $Branch 2>&1 | Out-Null
            if (-not (Test-Path (Join-Path $CiWatchDir '.git'))) {
                New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
                git -C $Root worktree add -q --detach $CiWatchDir "origin/$Branch" 2>&1 | Out-Null
            }
            git -C $CiWatchDir checkout -q -f --detach "origin/$Branch" 2>&1 | Out-Null
            git -C $CiWatchDir clean -q -fd -- Tasks 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Trace '-' 'ci' "cannot check out origin/$Branch in $CiWatchDir" 'DarkYellow'; return }
            $env:CLAUDE_PROJECT_DIR = $CiWatchDir
            $board = Join-Path $CiWatchDir '.claude\skills\task-board\task-board.ps1'
            $filed = @()
            foreach ($verdict in $verdicts) {
                $sha = @($runs | Where-Object { $_.Id -eq $verdict.RunId })[0].Sha
                if (Test-CiFailureCovered -Key $verdict.Key -Sha $sha -Repo $CiWatchDir) { $script:CiWatch.Settled["$($verdict.Key)|$($verdict.RunId)"] = 'covered by a task'; continue }
                $touches = @(Get-CiTouches -Project $verdict.Project -Repo $CiWatchDir)
                $newArgs = Get-CiTaskNewArgs -Verdict $verdict -Touches $touches
                $out = (& powershell -NoProfile -ExecutionPolicy Bypass -File $board @newArgs 2>&1 | ForEach-Object { "$_" }) -join "`n"
                if ($out -notmatch '(?m)^(BL-\d+)\s+(Tasks\S+\.md)') { Write-Trace '-' 'ci' "cannot file $($verdict.Key): $(Get-Short $out 60)" 'DarkYellow'; continue }
                $id = $Matches[1]; $line = $Matches[0]
                $url = "https://github.com/$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>$null)/actions/runs/$($verdict.RunId)"
                Set-CiTaskBody -Path (Join-Path $CiWatchDir $Matches[2]) -Verdict $verdict -RunUrl $url
                $filed += [pscustomobject]@{ Id = $id; Line = $line; Verdict = $verdict }
                if ($verdict.Kind -eq 'audit') { $script:CiWatch.AuditTasks["$id"] = Get-CiTaskTitle $verdict }
            }
            if (-not $filed.Count) { return }
            git -C $CiWatchDir add -A -- Tasks 2>&1 | Out-Null
            git -C $CiWatchDir commit -q -m "chore(tasks): file $(($filed | ForEach-Object { $_.Id }) -join ', ') for CI failures" -m "Filed by the dark factory's CI watch (BL-987)." 2>&1 | Out-Null
            git -C $CiWatchDir push -q origin "HEAD:$Branch" 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { continue }
            foreach ($task in $filed) {
                $script:CiWatch.Settled["$($task.Verdict.Key)|$($task.Verdict.RunId)"] = "filed $($task.Id)"
                Write-Trace $task.Id 'ci' "filed: $($task.Verdict.Verdict) $($task.Verdict.Key) on $($task.Verdict.Platforms -join '+'), run $($task.Verdict.RunId)" 'Yellow'
                Send-CiWhisper $task.Line
            }
            return
        }
        Write-Trace '-' 'ci' "could not push the CI failure tasks to $Branch; trying again next heartbeat" 'DarkYellow'
    } catch {
        Write-Trace '-' 'ci' "CI watch failed: $(Get-Short $_.Exception.Message 80)" 'DarkYellow'
    } finally { $env:CLAUDE_PROJECT_DIR = $saved }
}

function Remove-CiWatch {
    # Deletes the ci-watch worktree at shift end.
    if (-not (Test-Path $CiWatchDir)) { return }
    git -C $Root worktree remove --force $CiWatchDir 2>&1 | Out-Null
    git -C $Root worktree prune 2>&1 | Out-Null
}

if ($TestCiWatch) {
    $check = {
        param([string]$Name, [string]$Expected, [string]$Got)
        if ($Expected -ceq $Got) { Write-Host "PASS ${Name}: $Got" -ForegroundColor Green }
        else { Write-Host "FAIL ${Name}: expected $Expected, got $Got" -ForegroundColor Red; $script:ciTestFailed++ }
    }
    $script:ciTestFailed = 0
    $t = "`t"
    $log = @(
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:04.3646946Z Passed!  - Failed:     0, Passed:   257 - Curl.Zstandard.UnitTests.dll (net10.0)"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.0746696Z   Failed FromComponents_ZeroCoefficient_Throws [54 ms]"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.0765938Z   Error Message:"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.0856851Z    Assertion failed. Expected exception of exact type CryptographicException but caught OpenSslCryptographicException."
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.1238521Z                      at Curl.Protocol.Ssh.Keys.RsaSshPrivateKey.FromPkcs1(ReadOnlySpan``1 der) in /home/runner/work/Curl/Curl/Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.1352234Z                      at Curl.Protocol.Ssh.Keys.RsaSshPrivateKeyTests.<>c.b__5_0() in /home/runner/work/Curl/Curl/Curl.Protocol.Ssh.UnitTests/Keys/RsaSshPrivateKeyTests.cs:line 90"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:07.6024310Z Failed!  - Failed:     1, Passed:   873, Skipped:     0, Total:   874, Duration: 16 s - Curl.Protocol.Ssh.UnitTests.dll (net10.0)"
        "Build and test (macos-latest)${t}Fast tests${t}2026-09-29T22:10:05.3235560Z   Failed FromComponents_ZeroCoefficient_Throws [73 ms]"
        "Build and test (macos-latest)${t}Fast tests${t}2026-09-29T22:10:05.3240640Z   Error Message:"
        "Build and test (macos-latest)${t}Fast tests${t}2026-09-29T22:10:05.3240641Z    Assertion failed. Expected exception of exact type CryptographicException but caught AppleCFErrorCryptographicException."
        "Build and test (macos-latest)${t}Fast tests${t}2026-09-29T22:10:07.6442400Z Failed!  - Failed:     1, Passed:   873 - Curl.Protocol.Ssh.UnitTests.dll (net10.0)")
    $parsed = @(Get-CiFailures $log)
    & $check 'test failure parsed' 'FromComponents_ZeroCoefficient_Throws|test|Linux,macOS|Curl.Protocol.Ssh.UnitTests|Assertion failed. Expected exception of exact type CryptographicException but caught OpenSslCryptographicException.' `
        (($parsed | ForEach-Object { "$($_.Key)|$($_.Kind)|$($_.Platforms -join ',')|$($_.Project)|$($_.Message)" }) -join ';')
    $buildLog = @(
        "Build and test (macos-latest)${t}Build${t}2026-09-29T22:00:00.0000000Z /Users/runner/work/Curl/Curl/Curl.Cli.UnitLibrary/Options/Parser.cs(12,5): error CS1002: ; expected [/Users/runner/work/Curl/Curl/Curl.Cli.UnitLibrary/Curl.Cli.UnitLibrary.csproj]"
        "Build and test (macos-latest)${t}Build${t}2026-09-29T22:00:01.0000000Z /Users/runner/work/Curl/Curl/Curl.Cli.UnitLibrary/Options/Parser.cs(12,5): error CS1002: ; expected [/Users/runner/work/Curl/Curl/Curl.Cli.UnitLibrary/Curl.Cli.UnitLibrary.csproj]")
    & $check 'build error parsed' 'error CS1002 in Parser.cs|build|macOS|Curl.Cli.UnitLibrary|; expected' `
        ((@(Get-CiFailures $buildLog) | ForEach-Object { "$($_.Key)|$($_.Kind)|$($_.Platforms -join ',')|$($_.Project)|$($_.Message)" }) -join ';')
    $now = [datetime]'2026-09-29T23:00:00'
    $f = { param([string]$Key, [string[]]$On, [string]$Kind = 'test') [pscustomobject]@{ Key = $Key; Kind = $Kind; Platforms = $On; Project = 'Curl.X.UnitTests'; Message = 'm' } }
    $run = { param([string]$Id, [int]$MinutesAgo, [object[]]$Failures) [pscustomobject]@{ Id = $Id; Sha = "sha$Id"; FinishedAt = $now.AddMinutes(-$MinutesAgo); Failures = @($Failures) } }
    $show = { param($Verdicts) (@($Verdicts | Where-Object { $_ }) | ForEach-Object { "$($_.Verdict) $($_.Key) run $($_.RunId) from $($_.FirstSha) on $($_.Platforms -join '+')" }) -join '; ' }
    & $check 'two platforms: regression at once' 'regression A run 9 from sha9 on Linux+macOS' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f A @('Linux', 'macOS'))), (& $run 8 20 @()))))
    & $check 'one platform, newest only: waits' '' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f A @('Linux'))), (& $run 8 20 @()))))
    & $check 'one platform, unconfirmed for 30 min: regression' 'regression A run 9 from sha9 on Linux' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 31 @(& $f A @('Linux'))), (& $run 8 40 @()))))
    & $check 'two runs in a row: regression from the first' 'regression A run 9 from sha8 on Linux+macOS' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f A @('Linux'))), (& $run 8 9 @(& $f A @('macOS'))), (& $run 7 20 @()))))
    & $check 'failed once between passes: flaky' 'flaky A run 8 from sha8 on Linux' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @()), (& $run 8 9 @(& $f A @('Linux'))), (& $run 7 20 @()))))
    & $check 'failed again after passing: flaky' 'flaky A run 9 from sha9 on Linux' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f A @('Linux'))), (& $run 8 9 @()), (& $run 7 20 @(& $f A @('Linux'))))))
    & $check 'fixed regression: nothing' '' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @()), (& $run 8 9 @(& $f A @('Linux'))), (& $run 7 20 @(& $f A @('Linux'))))))
    & $check 'build error: regression at once' 'regression error CS1002 in X.cs run 9 from sha9 on Linux' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f 'error CS1002 in X.cs' @('Linux') 'build')))))
    & $check 'flaky title' 'Fix flaky CI test A that failed once on Linux' (Get-CiTaskTitle ([pscustomobject]@{ Key = 'A'; Kind = 'test'; Verdict = 'flaky'; Platforms = @('Linux') }))
    & $check 'touches' 'Curl.Protocol.Ssh.UnitTests,Curl.Protocol.Ssh.UnitLibrary' ((Get-CiTouches -Project 'Curl.Protocol.Ssh.UnitTests' -Repo $Root) -join ',')
    & $check 'integration touches' 'Curl.Networking.IntegrationTests,Curl.Networking.UnitLibrary' ((Get-CiTouches -Project 'Curl.Networking.IntegrationTests' -Repo $Root) -join ',')
    & $check 'console integration touches' 'Curl.Console.IntegrationTests,Curl.Console' ((Get-CiTouches -Project 'Curl.Console.IntegrationTests' -Repo $Root) -join ',')
    $integrationLog = @(
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000000Z   Failed Connect_Loopback_Succeeds [12 ms]"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000001Z   Error Message:"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000002Z    Assert.AreEqual failed."
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000003Z      at Curl.Networking.TcpDialerTests.Connect_Loopback_Succeeds() in /home/runner/work/Curl/Curl/Curl.Networking.IntegrationTests/TcpDialerTests.cs:line 12"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000004Z Failed!  - Failed:     1, Passed:     3 - SomethingElse.dll (net10.0)")
    & $check 'integration test failure parsed' 'Connect_Loopback_Succeeds|Curl.Networking.IntegrationTests' `
        ((@(Get-CiFailures $integrationLog) | ForEach-Object { "$($_.Key)|$($_.Project)" }) -join ';')
    $body = Join-Path ([IO.Path]::GetTempPath()) "df-ci-body-$PID.md"
    Copy-Item (Join-Path $Root '.claude\skills\task-board\TASK-TEMPLATE.md') $body
    Set-CiTaskBody -Path $body -RunUrl 'https://github.com/o/r/actions/runs/9' -Verdict ([pscustomobject]@{
        Key = 'A_Throws'; Kind = 'test'; Verdict = 'regression'; RunId = '9'; FirstSha = '0729839c0000'; Platforms = @('Linux', 'macOS'); Message = 'costs $5' })
    $text = [IO.File]::ReadAllText($body)
    Remove-Item $body -ErrorAction SilentlyContinue
    $seen = @()
    $seen += $(if ($text -match '<!--') { 'placeholder left' } else { 'no placeholder' })
    if ($text -match 'CI run 9 \(https') { $seen += 'run 9' }
    if ($text -match 'First failing commit: 0729839c\.') { $seen += '0729839c' }
    if ($text.Contains('costs $5')) { $seen += '$5' }
    if ($text.Contains('- [ ] `A_Throws` passes locally')) { $seen += 'criteria' }
    & $check 'task body filled' 'no placeholder; run 9; 0729839c; $5; criteria' ($seen -join '; ')
    # Covered: a live task names the test; a Done one does for a run older than its last commit.
    $repo = Join-Path ([IO.Path]::GetTempPath()) "df-ci-covered-$PID"
    foreach ($state in 'Backlog', 'Done') { New-Item -ItemType Directory -Force -Path (Join-Path $repo "Tasks\$state") | Out-Null }
    $commit = { param([string]$Message) git -C $repo add -A 2>&1 | Out-Null; git -C $repo -c user.name=t -c user.email=t@t commit -q --allow-empty -m $Message 2>&1 | Out-Null; "$(git -C $repo rev-parse HEAD)".Trim() }
    git -C $repo init -q -b work 2>&1 | Out-Null
    Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-001-fix-ci-failure-a.md') -Value 'title: Fix CI failure A_Throws on Linux'
    $oldRun = & $commit 'file BL-001'
    $live = Test-CiFailureCovered -Key 'A_Throws' -Sha $oldRun -Repo $repo
    $prefix = Test-CiFailureCovered -Key 'A' -Sha $oldRun -Repo $repo
    Move-Item (Join-Path $repo 'Tasks\Backlog\BL-001-fix-ci-failure-a.md') (Join-Path $repo 'Tasks\Done\')
    & $commit 'BL-001 Done' | Out-Null
    $newRun = & $commit 'a later push'
    $stale = Test-CiFailureCovered -Key 'A_Throws' -Sha $oldRun -Repo $repo
    $again = Test-CiFailureCovered -Key 'A_Throws' -Sha $newRun -Repo $repo
    Remove-Item -Recurse -Force -Path $repo -ErrorAction SilentlyContinue
    & $check 'covered' 'live True; other name False; before the fix True; after the fix False' "live $live; other name $prefix; before the fix $stale; after the fix $again"
    # BL-1031, the live path: gh's run list and --log-failed lines, recorded from runs
    # 36674691490 (two TcpDialerTests failures on Linux) and 36690174792 (one on macOS).
    $traced = New-Object Collections.Generic.List[string]
    function Write-Trace { param($Id, $Kind, $Text, $Color) $traced.Add($Text) }
    $script:CiWatch = @{ CheckedAt = [datetime]::MinValue; Failures = @{}; Settled = @{}; Logged = @{}; AuditTasks = @{}; Off = $false }
    $ubuntu = "Build and test (ubuntu-latest)${t}Fast tests${t}"
    $macos = "Build and test (macos-latest)${t}UNKNOWN STEP${t}"
    $recorded = @{
        '36674691490' = @(
            "${ubuntu}2026-09-30T05:46:09.4012066Z   Skipped ConnectAsync_WithAnAbstractSocketOnWindows_FailsAsCurlsInvalidArguments"
            "${ubuntu}2026-09-30T05:46:09.4101921Z   Failed BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed [12 ms]"
            "${ubuntu}2026-09-30T05:46:09.4131550Z   Error Message:"
            "${ubuntu}2026-09-30T05:46:09.4161939Z    Assertion failed. Expected exception of exact type LocalBindException but no exception was thrown."
            "${ubuntu}2026-09-30T05:46:09.4211716Z   Stack Trace:"
            "${ubuntu}2026-09-30T05:46:09.4242317Z      at Curl.Networking.TcpDialerTests.BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed() in /home/runner/work/Curl/Curl/Curl.Networking.UnitTests/TcpDialerTests.cs:line 231"
            "${ubuntu}2026-09-30T05:46:09.4331989Z   Failed BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext [22 ms]"
            "${ubuntu}2026-09-30T05:46:09.4332652Z   Error Message:"
            "${ubuntu}2026-09-30T05:46:09.4333071Z    Assertion failed. Expected values to be equal."
            "${ubuntu}2026-09-30T05:46:09.4382540Z   Stack Trace:"
            "${ubuntu}2026-09-30T05:46:09.4412190Z      at Curl.Networking.TcpDialerTests.BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext() in /home/runner/work/Curl/Curl/Curl.Networking.UnitTests/TcpDialerTests.cs:line 219"
            "${ubuntu}2026-09-30T05:46:09.4992460Z Passed!  - Failed:     0, Passed:    98, Skipped:     0, Total:    98, Duration: 531 ms - Curl.Protocol.Mqtt.UnitTests.dll (net10.0)"
            "${ubuntu}2026-09-30T05:46:17.9032566Z Failed!  - Failed:     2, Passed:  1666, Skipped:    16, Total:  1684, Duration: 35 s - Curl.Networking.UnitTests.dll (net10.0)")
        '36690174792' = @(
            "${macos}2026-09-30T08:34:39.6519410Z   Failed BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext [9 ms]"
            "${macos}2026-09-30T08:34:39.6519420Z   Error Message:"
            "${macos}2026-09-30T08:34:39.6519430Z    Test method Curl.Networking.TcpDialerTests.BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext threw exception:"
            "${macos}2026-09-30T08:34:44.7941160Z Failed!  - Failed:     1, Passed:  1651, Skipped:    32, Total:  1684, Duration: 25 s - Curl.Networking.UnitTests.dll (net10.0)")
        '36671448420' = @("${ubuntu}2026-09-30T05:05:00.0000000Z ##[error]Process completed with exit code 1.")
    }
    $readLog = { param([string]$Id) if ($Id -eq '99') { return $null } return , @($recorded[$Id]) }
    $listed = { param([string]$Branch) $script:ciListJson }
    $script:ciListJson = '[{"conclusion":"failure","databaseId":36674691490,"headSha":"d0621052bba101113a0187570196267f13bc6b74","status":"completed","updatedAt":"2026-09-30T05:47:43Z"},{"conclusion":"success","databaseId":36668685198,"headSha":"7c9833f44d160cdb92e3bf3c9fda81b9cc9a0e5d","status":"completed","updatedAt":"2026-09-30T04:28:52Z"}]'
    $replay = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog
    $replayAt = $replay[0].FinishedAt
    & $check 'replay 36674691490: new, waits to confirm' '' (& $show (Get-CiVerdicts -Now $replayAt.AddMinutes(5) -Runs $replay))
    & $check 'replay 36674691490: a task per failing test once confirmed' `
        'regression BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed run 36674691490 from d0621052bba101113a0187570196267f13bc6b74 on Linux; regression BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext run 36674691490 from d0621052bba101113a0187570196267f13bc6b74 on Linux' `
        (& $show (Get-CiVerdicts -Now $replayAt.AddMinutes(31) -Runs $replay))
    & $check 'replay 36674691490: the final look files at once' '2' "$(@(Get-CiVerdicts -Now $replayAt.AddMinutes(5) -Runs $replay -ConfirmMinutes 0).Count)"
    # The case that failed live: a run with one failing test was dropped as if it had none.
    $script:ciListJson = '[{"conclusion":"failure","databaseId":36690174792,"headSha":"b86b15dfc80d1dea7a5733c9b73fd263a17533b3","status":"completed","updatedAt":"2026-09-30T08:35:42Z"},{"conclusion":"cancelled","databaseId":1,"headSha":"x","status":"completed","updatedAt":"2026-09-30T08:31:26Z"},{"conclusion":"failure","databaseId":36671448420,"headSha":"bfd523f47edea0e8d7826a657606353aa2b6b462","status":"completed","updatedAt":"2026-09-30T05:05:30Z"},{"conclusion":"failure","databaseId":99,"headSha":"9999999999","status":"completed","updatedAt":"2026-09-30T05:00:00Z"},{"conclusion":"success","databaseId":36689969747,"headSha":"c4083da8fbe96c97b45499a41bab397f52fadce4","status":"completed","updatedAt":"2026-09-30T08:33:40Z"}]'
    $single = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog
    & $check 'one failing test keeps its run' '36690174792 1 macOS; 36689969747 0 ' `
        (($single | ForEach-Object { "$($_.Id) $(@($_.Failures).Count) $(@($_.Failures | ForEach-Object { $_.Platforms }) -join '+')" }) -join '; ')
    & $check 'unreadable and nameless failures traced' 'run 36671448420 failure on bfd523f4: its log names no failing test or build error; nothing to file|run 99 failure on 99999999: cannot read its log with gh run view --log-failed; trying again next heartbeat' `
        ((@($traced) | Sort-Object) -join '|')
    $traced.Clear()
    $script:ciListJson = $null
    $first = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog
    $second = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog
    & $check 'gh cannot list: no runs, traced once' 'True True; cannot list the CI runs on work with gh' "$($null -eq $first) $($null -eq $second); $($traced -join '|')"
    # What a coordinator log line says for each run it examined, and that it says it once.
    $verdicts = @(Get-CiVerdicts -Now $replayAt.AddMinutes(31) -Runs $replay)
    $waiting = Get-CiRunOutcome -Run $replay[0] -Runs $replay -Verdicts @() -Settled @{} -ConfirmMinutes 30
    & $check 'outcome: waiting' "failure on d0621052: BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed on Linux only, waiting for the next run or $($replayAt.AddMinutes(30).ToString('HH:mm')) to confirm it; BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext on Linux only, waiting for the next run or $($replayAt.AddMinutes(30).ToString('HH:mm')) to confirm it" $waiting
    $settled = @{ 'BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed|36674691490' = 'filed BL-1040'; 'BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext|36674691490' = 'covered by a task' }
    & $check 'outcome: filed and covered' 'failure on d0621052: BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed regression, filed BL-1040; BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext regression, covered by a task' `
        (Get-CiRunOutcome -Run $replay[0] -Runs $replay -Verdicts $verdicts -Settled $settled)
    & $check 'outcome: green run' 'success on 7c9833f4: nothing to file' (Get-CiRunOutcome -Run $replay[1] -Runs $replay -Verdicts $verdicts -Settled $settled)
    & $check 'outcome: not filed yet, older run, passed since' 'failure on sha8: A regression, not filed yet; failure on sha8: A filed with run 9; failure on sha8: A passed since, not filed' `
        ((@(
            Get-CiRunOutcome -Run (& $run 8 9 @(& $f A @('Linux'))) -Runs @((& $run 8 9 @())) -Verdicts @([pscustomobject]@{ Key = 'A'; Verdict = 'regression'; RunId = '8' }) -Settled @{}
            Get-CiRunOutcome -Run (& $run 8 9 @(& $f A @('Linux'))) -Runs @((& $run 9 2 @())) -Verdicts @([pscustomobject]@{ Key = 'A'; Verdict = 'regression'; RunId = '9' }) -Settled @{}
            Get-CiRunOutcome -Run (& $run 8 9 @(& $f A @('Linux'))) -Runs @((& $run 9 2 @())) -Verdicts @() -Settled @{})) -join '; ')
    $traced.Clear()
    Write-CiRunLine -RunId '7' -Line 'success on sha7: nothing to file'
    Write-CiRunLine -RunId '7' -Line 'success on sha7: nothing to file'
    Write-CiRunLine -RunId '8' -Line 'failure on sha8: A waiting'
    Write-CiRunLine -RunId '8' -Line 'failure on sha8: A regression, filed BL-1'
    & $check 'a run is traced once per outcome' 'run 7 success on sha7: nothing to file|run 8 failure on sha8: A waiting|run 8 failure on sha8: A regression, filed BL-1' ($traced -join '|')
    # BL-999: the audit guard's failure line files one interactive-only task, once.
    $auditLog = @("audit-guard${t}Run guard${t}2026-09-29T10:00:00.0000000Z Audit guard: Audit/Findings/x.md changed on work/dark-factory since its merge base with master")
    $audit = @(Get-CiFailures $auditLog)
    & $check 'audit guard line parsed' 'audit guard Audit/Findings/x.md|audit' (($audit | ForEach-Object { "$($_.Key)|$($_.Kind)" }) -join ';')
    $auditVerdicts = @(Get-CiVerdicts -Now $now -Runs @((& $run 9 2 $audit)))
    & $check 'audit guard: one red run files it' 'regression audit guard Audit/Findings/x.md run 9 from sha9 on audit-guard' (& $show $auditVerdicts)
    & $check 'audit guard title' "Revert the dark factory's change to Audit/Findings/x.md" (Get-CiTaskTitle $auditVerdicts[0])
    $board = Join-Path ([IO.Path]::GetTempPath()) "df-ci-audit-$PID"
    foreach ($state in 'Backlog', 'Doing', 'Blocked', 'Done') { New-Item -ItemType Directory -Force -Path (Join-Path $board "Tasks\$state") | Out-Null }
    $savedDir = $env:CLAUDE_PROJECT_DIR; $savedLane = $env:CURL_DARK_FACTORY_LANE
    try {
        $env:CLAUDE_PROJECT_DIR = $board
        $env:CURL_DARK_FACTORY_LANE = '0'
        $boardScript = Join-Path $Root '.claude\skills\task-board\task-board.ps1'
        $out = (& powershell -NoProfile -ExecutionPolicy Bypass -File $boardScript @(Get-CiTaskNewArgs -Verdict $auditVerdicts[0] -Touches @()) 2>&1 | ForEach-Object { "$_" }) -join "`n"
        $filedPath = if ($out -match '(?m)^(BL-\d+)\s+(Tasks\S+\.md)') { Join-Path $board $Matches[2] } else { '' }
        & $check 'audit task filed with CURL_DARK_FACTORY_LANE=0' 'filed' $(if ($filedPath -and (Test-Path $filedPath)) { 'filed' } else { "not filed: $out" })
        if ($filedPath -and (Test-Path $filedPath)) {
            Set-CiTaskBody -Path $filedPath -Verdict $auditVerdicts[0] -RunUrl 'https://github.com/o/r/actions/runs/9'
            $text = [IO.File]::ReadAllText($filedPath)
            $fields = @(
                $(if ($text -match '(?m)^lane: no\s*$') { 'lane: no' })
                $(if ($text -match '(?m)^priority: High\s*$') { 'High' })
                $(if ($text -match '(?m)^touches: \[Audit/Findings/x\.md\]\s*$') { 'touches: [Audit/Findings/x.md]' })
                $(if ($text -notmatch '<!--') { 'no placeholder' })) -join '; '
            & $check 'audit task fields' 'lane: no; High; touches: [Audit/Findings/x.md]; no placeholder' $fields
            & $check 'audit guard: a second run files nothing' 'True' "$(Test-CiFailureCovered -Key $auditVerdicts[0].Key -Sha 'sha10' -Repo $board)"
        }
    } finally {
        $env:CLAUDE_PROJECT_DIR = $savedDir; $env:CURL_DARK_FACTORY_LANE = $savedLane
        Remove-Item -Recurse -Force -Path $board -ErrorAction SilentlyContinue
    }
    $script:CiWatch.AuditTasks = @{ 'BL-2001' = "Revert the dark factory's change to Audit/Findings/x.md" }
    & $check 'end report names an open audit task' "BL-2001 AUDIT    needs an interactive session: Revert the dark factory's change to Audit/Findings/x.md" `
        ((@(Get-OpenAuditTaskLines) | Where-Object { $_ -like 'BL-2001 *' }) -join '|')
    exit $(if ($script:ciTestFailed) { 1 } else { 0 })
}

function Save-StrayChanges {
    param([string]$Id)
    if (-not (Get-Dirty)) { return }
    git -C $Root stash push --include-untracked -m "darkfactory $Id $Stamp" | Out-Null
    Write-Trace $Id 'stash' "uncommitted work kept: git stash list" 'Yellow'
}

function Restore-TaskStash {
    # A task that went back to Backlog with work in progress left it in the shared stash
    # list as "darkfactory <id> <stamp>" (Save-StrayChanges). A lane's run may not run
    # git stash, not even git stash list (AF-0091: BL-1609's fourth claim was spent finding
    # the third's stash), so the shift applies the newest such stash itself when the task
    # is claimed again. Returns the note put in front of the run's prompt, '' for none.
    param([string]$Id, [string]$Repo = $Root)
    $pattern = "^(\S+) .*: darkfactory $([regex]::Escape($Id)) "
    $entry = @(git -C $Repo stash list --format='%H %gs' 2>$null) | Where-Object { $_ -match $pattern } | Select-Object -First 1
    if (-not $entry) { return '' }
    $sha = ($entry -split ' ', 2)[0]
    $applied = $false
    if (-not (@(git -C $Repo status --porcelain) | Where-Object { $_ })) {
        git -C $Repo stash apply -q $sha 2>&1 | Out-Null
        $applied = $LASTEXITCODE -eq 0
        if (-not $applied) {
            # A conflict half-applies it; the worktree was clean, so put it back that way.
            git -C $Repo reset -q --hard HEAD 2>&1 | Out-Null
            git -C $Repo clean -fdq 2>&1 | Out-Null
        }
    }
    if ($applied) {
        Write-Trace $Id 'stash' "applied the earlier run's work from stash $($sha.Substring(0, 8))" 'Yellow'
        return @"
STASHED WORK. An earlier run of $Id went back to Backlog with uncommitted work, which the
shift kept as stash $sha and has applied to this worktree. Read git status, git diff and
the task file before changing anything, and carry on from that work rather than starting over.

"@
    }
    Write-Trace $Id 'stash' "stash $($sha.Substring(0, 8)) from an earlier run did not apply cleanly; the run is told its hash" 'Yellow'
    return @"
STASHED WORK. An earlier run of $Id went back to Backlog with uncommitted work, which the
shift kept as stash $sha. It did not apply cleanly to this branch, so this worktree is
clean. ``git diff $sha^1 $sha`` shows its changes to tracked files and
``git show --stat $sha^3`` the files it added (``git show $sha^3:<path>`` prints one);
carry over what still fits rather than starting over.

"@
}

if ($TestTaskStash) {
    $repo = Join-Path ([IO.Path]::GetTempPath()) "df-task-stash-$PID"
    New-Item -ItemType Directory -Force -Path $repo | Out-Null
    git -C $repo init -q -b master 2>&1 | Out-Null
    git -C $repo config user.name t; git -C $repo config user.email t@t
    Set-Content -Path (Join-Path $repo 'a.txt') -Value 'one'
    git -C $repo add a.txt; git -C $repo commit -q -m one 2>&1 | Out-Null
    $failed = 0
    $check = { param($Name, $Expected, $Got)
        if ($Expected -ceq $Got) { Write-Host "PASS ${Name}: $Got" -ForegroundColor Green }
        else { Write-Host "FAIL ${Name}: expected $Expected, got $Got" -ForegroundColor Red; $script:failed++ } }
    $read = { param($f) $p = Join-Path $repo $f; if (Test-Path $p) { (Get-Content $p -Raw).Trim() } else { '-' } }
    try {
        & $check 'no stash, no note' '' (Restore-TaskStash -Id 'BL-1' -Repo $repo)
        # An earlier run's work: a tracked change and a new file, stashed as the shift does.
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'two'
        Set-Content -Path (Join-Path $repo 'b.txt') -Value 'new'
        git -C $repo stash push -q --include-untracked -m 'darkfactory BL-1 20260101-000000' 2>&1 | Out-Null
        & $check 'other task''s stash is left alone' '' (Restore-TaskStash -Id 'BL-10' -Repo $repo)
        & $check 'other task''s claim stays clean' 'one -' "$(& $read 'a.txt') $(& $read 'b.txt')"
        $note = Restore-TaskStash -Id 'BL-1' -Repo $repo
        & $check 'its own stash is applied' 'two new' "$(& $read 'a.txt') $(& $read 'b.txt')"
        & $check 'the note says it was applied' 'True' "$($note -match 'has applied to this worktree')"
        # Back to clean, then a branch that moved under the stash: it cannot apply.
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null; git -C $repo clean -fdq 2>&1 | Out-Null
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'three'
        git -C $repo commit -q -am three 2>&1 | Out-Null
        $note = Restore-TaskStash -Id 'BL-1' -Repo $repo
        & $check 'a conflict leaves the worktree clean' "three - True" "$(& $read 'a.txt') $(& $read 'b.txt') $(-not (git -C $repo status --porcelain))"
        $sha = "$(git -C $repo rev-parse 'stash@{0}')".Trim()
        & $check 'the note gives the hash to read it by' 'True' "$($note.Contains("git diff $sha^1 $sha"))"
    } finally {
        Remove-Item -Recurse -Force -Path $repo -ErrorAction SilentlyContinue
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# ---------------------------------------------------------------------------- run

$Prompt = @'
DARK FACTORY SHIFT. Stewart is away and cannot answer. Never ask a question and never
wait for input; nobody will reply.

Run /task-run {ID}.

Rules for this unattended run, in addition to CLAUDE.md:
0. No Python, not even a one-liner: PowerShell for scripts and checks, the Edit tool
   for edits, and Record-CurlExchange.ps1 to measure real curl (extend it if needed).
1. Where a choice has a sensible default, take it and record the choice and why under
   the task's Notes.
2. Design and behaviour decisions are yours: Stewart has delegated them (CLAUDE.md,
   "Decisions"). Decide by his standing rules - match the platform's curl, measure real
   curl before pinning output, BCL only - record the decision and why in an ADR marked
   "Decided by Claude under Stewart's delegation", and carry on. Only a new package or
   a quality-threshold change goes to Blocked, with a -Reason that starts "Stewart:" and
   asks the question in one line.
3. If the only thing stopping the task is other work - an existing task, or one you
   file with the board script - add those IDs to its `depends-on` and move it to
   Backlog, not Blocked, with a -Reason naming them. The board starts it again once
   they are Done.
4. When the task reaches Done with dotnet build clean and the fast tests green, commit
   by logical unit (Conventional Commits, including the task file) and push the current
   branch yourself with git, per the standing authorization in CLAUDE.md. Never push to
   master, never force push, never merge.
5. If the task ends Blocked or back in Backlog, commit only the task board change and
   push it. Leave any unfinished code uncommitted; the shift stashes it and applies it
   again when the task is next claimed.
6. The task must not be left in Doing.
7. This run ends the moment your reply ends, and anything still in the background - a
   command moved there, run_in_background, a Monitor, a subagent - dies with it. Run
   dotnet build and dotnet test in the foreground with the Bash tool and a timeout of up
   to 3600000 ms, and never end your reply to wait for a notification.
   Run at most one subagent at a time, and never start several in one message: the
   shift's cost cap is checked only between your own turns, so subagents running in
   parallel all spend inside one turn and carry the run past the cap (AF-0052).
8. The shift kills this run at {DEADLINE}, {MINUTES} minutes after it started, and a
   killed run ends Blocked. While other lanes build, one Measure-CodeQuality.ps1 run can
   take 30 to 45 minutes: run it once per library you changed, with -ReportPath, and
   read the report rather than running it again. If the task cannot be finished before
   the deadline, move it to Backlog before then with a -Reason saying what is left.

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

Rules for this unattended run, in addition to CLAUDE.md:
0. No Python, not even a one-liner: PowerShell for scripts and checks, the Edit tool
   for edits, and Record-CurlExchange.ps1 to measure real curl (extend it if needed).
1. Where a choice has a sensible default, take it and record the choice and why under
   the task's Notes.
2. Design and behaviour decisions are yours: Stewart has delegated them (CLAUDE.md,
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
   and carry on. When one does, add it anyway and move the task to Backlog with a
   -Reason naming the project and that task; the board will not offer it again until
   they no longer overlap.
4. If the only thing stopping the task is other work - an existing task, or one you
   file with the board script - add those IDs to its `depends-on` and move it to
   Backlog, not Blocked, with a -Reason naming them. The board starts it again once
   they are Done. Blocked is only for what needs Stewart.
5. When the task reaches Done with dotnet build clean and the fast tests green, commit
   by logical unit (Conventional Commits, including the task file). Do NOT push, pull,
   rebase, merge or switch branches: the shift integrates your commits.
6. If the task ends Blocked or back in Backlog, commit only the task board change.
   Leave any unfinished code uncommitted; the shift stashes it and applies it again
   when the task is next claimed.
7. The task must not be left in Doing.
8. This run ends the moment your reply ends, and anything still in the background - a
   command moved there, run_in_background, a Monitor, a subagent - dies with it. Run
   dotnet build and dotnet test in the foreground with the Bash tool and a timeout of up
   to 3600000 ms, and never end your reply to wait for a notification.
   Run at most one subagent at a time, and never start several in one message: the
   shift's cost cap is checked only between your own turns, so subagents running in
   parallel all spend inside one turn and carry the run past the cap (AF-0052).
9. The shift kills this run at {DEADLINE}, {MINUTES} minutes after it started, and a
   killed run ends Blocked. While other lanes build, one Measure-CodeQuality.ps1 run can
   take 30 to 45 minutes: run it once per library you changed, with -ReportPath, and
   read the report rather than running it again. If the task cannot be finished before
   the deadline, move it to Backlog before then with a -Reason saying what is left.

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
# The conflict resolver needs `git rebase --continue`, and nothing that throws work away.
$ResolveForbidden = $Forbidden + @('git push', 'git pull', 'git rebase --abort', 'git rebase --skip', 'git checkout', 'git switch', 'git worktree', 'git stash')

function Get-ToolLabel {
    param($Tool)
    $in = $Tool.input
    switch ($Tool.name) {
        'Agent' { return @('agent', "$($in.subagent_type): $($in.description)") }
        'Task' { return @('agent', "$($in.subagent_type): $($in.description)") }
        'Skill' { return @('skill', "$($in.skill) $($in.args)") }
        'Edit' { return @('edit', (Split-Path "$($in.file_path)" -Leaf)) }
        'Write' { return @('write', (Split-Path "$($in.file_path)" -Leaf)) }
        { $_ -in 'Bash', 'PowerShell' } {
            $c = "$($in.command)"
            if ($c -match 'task-board\.ps1\s+move\b.*-To\s+(\w+)') { return @('move', $Matches[1]) }
            if ($c -match 'dotnet\s+build') { return @('build', '') }
            if ($c -match 'dotnet\s+test') { return @('test', '') }
            if ($c -match 'dotnet\s+format') { return @('format', '') }
            if ($c -match 'Measure-CodeQuality') { return @('quality', '') }
            if ($c -match 'git\s+commit') { return @('commit', '') }
            if ($c -match 'git\s+push') { return @('push', '') }
        }
    }
    return $null
}

function Get-ResultText {
    param($Content)
    if ($Content -is [string]) { return $Content }
    return (@($Content) | ForEach-Object { if ($_.PSObject.Properties['text']) { $_.text } }) -join "`n"
}

function Get-Outcome {
    param([string]$Verb, [string]$Text, [bool]$IsError)
    if ($Verb -eq 'test') {
        $t = [regex]::Matches($Text, 'total:\s*(\d+),\s*failed:\s*(\d+)', 'IgnoreCase')
        if ($t.Count -eq 0) { $t = [regex]::Matches($Text, 'Failed:\s*(\d+),\s*Passed:\s*(\d+)') ; $swap = $true } else { $swap = $false }
        if ($t.Count -gt 0) {
            $total = 0; $failed = 0
            foreach ($m in $t) {
                if ($swap) { $failed += [int]$m.Groups[1].Value; $total += [int]$m.Groups[1].Value + [int]$m.Groups[2].Value }
                else { $total += [int]$m.Groups[1].Value; $failed += [int]$m.Groups[2].Value }
            }
            return "$($total - $failed)/$total pass"
        }
    }
    if ($Verb -eq 'commit' -and $Text -match '\[[^\]]*\s([0-9a-f]{7,})\]\s*(.*)') { return "$($Matches[1]) $($Matches[2])" }
    if ($Verb -eq 'build' -and $Text -match '(\d+)\s+Error\(s\)' -and [int]$Matches[1] -gt 0) { return "FAIL $($Matches[1]) error(s)" }
    if ($IsError) { return 'FAIL ' + (Get-Short (($Text -split "`n" | Where-Object { $_.Trim() } | Select-Object -Last 1)) 50) }
    return 'ok'
}

function Write-Event {
    param([string]$Id, $Evt)
    switch ($Evt.type) {
        'assistant' {
            foreach ($c in @($Evt.message.content)) {
                if ($c.type -ne 'tool_use') { continue }
                $label = Get-ToolLabel $c
                if (-not $label) { continue }
                $script:ToolLabels[$c.id] = $label
                Set-HeartbeatStep $label
                if ($label[0] -in 'agent', 'skill', 'edit', 'write') { Write-Trace $Id $label[0] (Get-Short $label[1]) }
            }
        }
        'user' {
            foreach ($c in @($Evt.message.content)) {
                if ($c.type -ne 'tool_result' -or -not $script:ToolLabels.ContainsKey($c.tool_use_id)) { continue }
                $verb, $detail = $script:ToolLabels[$c.tool_use_id]
                if ($verb -in 'agent', 'skill', 'edit', 'write') { continue }
                $isErr = [bool]($c.PSObject.Properties['is_error'] -and $c.is_error)
                $outcome = Get-Outcome $verb (Get-ResultText $c.content) $isErr
                $color = if ($outcome -like 'FAIL*') { 'Red' } else { 'Gray' }
                if ($verb -eq 'move') { $outcome = "$detail $outcome" }
                Write-Trace $Id $verb $outcome $color
            }
        }
        'rate_limit_event' {
            $info = $Evt.rate_limit_info
            if ($info.status -eq 'rejected' -and $info.resetsAt) { $script:LimitResetAt = ConvertFrom-Unix ([long]$info.resetsAt) }
        }
        'result' {
            $mins = [math]::Round($Evt.duration_ms / 60000, 1)
            $script:RunResult = $Evt
            Write-Trace $Id 'end' "$($Evt.num_turns) turns, $mins min$(Format-ModelChoice)"
        }
    }
}

function Invoke-TaskRun {
    # One headless Claude run in this checkout. By default it is the task run; a lane
    # passes its own prompt and deny list, and a log suffix for the resolver's run.
    # -Resume is the task run again after the usage limit cut the last one off.
    # -Note goes in front of the prompt, e.g. Restore-TaskStash's word on an earlier run's work.
    param([string]$Id, [string]$Text = '', [string[]]$Deny = $null, [string]$Suffix = '', [int]$Minutes = 0, [switch]$Resume, [switch]$Overtime, [string]$Note = '')
    if (-not $Text) { $Text = if ($Lane) { $LanePrompt } else { $Prompt } }
    $Text = $Note + $Text
    if ($Overtime) { $Text = $OvertimeNote + $Text; $Suffix += '-overtime' }
    elseif ($Resume) { $Text = $ResumeNote + $Text; $Suffix += '-resumed' }
    if ($null -eq $Deny) { $Deny = if ($Lane) { $LaneForbidden } else { $Forbidden } }
    if ($Minutes -le 0) { $Minutes = $TaskMinutes }
    # The run is told when it will be killed, so it can hand the task back before then (AF-0034).
    $deadline = (Get-Date).AddMinutes($Minutes)
    $Text = $Text.Replace('{ID}', $Id).Replace('{LANE}', "$Lane").Replace('{BRANCH}', $Branch)
    $Text = $Text.Replace('{DEADLINE}', $deadline.ToString('HH:mm')).Replace('{MINUTES}', "$Minutes")
    $raw = Join-Path $LogDir "$Id-$Stamp$LaneTag$Suffix.jsonl"
    $err = Join-Path $LogDir "$Id-$Stamp$LaneTag$Suffix.err.txt"
    $script:RunResult = $null
    $script:LimitResetAt = $null
    $script:ToolLabels = @{}

    $denied = ($Deny | ForEach-Object { "`"Bash($_`:*)`" `"PowerShell($_`:*)`"" }) -join ' '
    # New-ClaudeRunStartInfo gives Bash calls room for a build and the fast tests while six
    # lanes build at once: a tool call past its timeout is moved to the background, and a
    # headless run that then ends its reply to wait for it exits with the task still in
    # Doing (BL-855). It inherits CURL_DARK_FACTORY_LANE.
    # The cost cap (AF-0004, AF-0033): the run stops once it has cost 2.7 times the median
    # recent run, at most -TaskBudgetUsd.
    $script:RunBudgetUsd = Get-RunBudgetUsd $TaskBudgetUsd @(Get-RecentRunCosts $LogDir)
    $budget = if ($script:RunBudgetUsd -gt 0) { ' --max-budget-usd ' + $script:RunBudgetUsd.ToString([System.Globalization.CultureInfo]::InvariantCulture) } else { '' }
    # The task's model (BL-1705); the resolver's, overtime and resumed runs keep it. The
    # log's first line names it, so cost per model can be read back (Get-ModelCostSummary).
    if ($script:ModelChoice.Id -ne $Id) { Set-ModelChoice $Id }
    $runModel = $script:ModelChoice.Model
    if (-not (Test-Path $raw)) { Add-Content -Path $raw -Value ([pscustomobject][ordered]@{ type = 'factory'; model = $runModel; why = $script:ModelChoice.Why } | ConvertTo-Json -Compress) -Encoding UTF8 }
    $psi = New-ClaudeRunStartInfo "/d /c claude -p --model $runModel --dangerously-skip-permissions --output-format stream-json --verbose$budget --disallowedTools $denied 2>`"$err`""
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.Write($Text)
    $p.StandardInput.Close()

    $timedOut = $false
    $pending = $p.StandardOutput.ReadLineAsync()
    while ($true) {
        Write-HeartbeatIfDue
        if ($pending.Wait(1000)) {
            $line = $pending.Result
            if ($null -eq $line) { break }
            Add-Content -Path $raw -Value $line -Encoding UTF8
            try { $evt = $line | ConvertFrom-Json } catch { $evt = $null }
            if ($evt) { Write-Event $Id $evt }
            $pending = $p.StandardOutput.ReadLineAsync()
        } elseif ((Get-Date) -gt $deadline) {
            & taskkill /T /F /PID $p.Id 2>&1 | Out-Null
            $timedOut = $true
            # A killed run sends no result event, so its end is traced here: without it the
            # lane log's last line for the run is whatever step it was in (AF-0034).
            Write-Trace $Id 'end' "killed: timed out after $Minutes min$(Format-ModelChoice)" 'Red'
            break
        }
    }
    $p.WaitForExit()
    return @{ ExitCode = $p.ExitCode; TimedOut = $timedOut }
}

# ---------------------------------------------------------------------------- lanes

function Invoke-Git {
    # git in this checkout; returns $true when it exited 0. Output goes to the trace only
    # on failure, so a quiet lane stays quiet.
    param([string[]]$GitArgs)
    $out = & git -C $Root @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $last = ($out | ForEach-Object { "$_" } | Where-Object { $_.Trim() } | Select-Object -Last 1)
        Write-Trace '-' 'git' "FAIL git $($GitArgs[0]): $(Get-Short "$last" 60)" 'DarkYellow'
        return $false
    }
    return $true
}

function Enter-Lock {
    # Claims and integrations take turns across every lane. Holding the file open with no
    # sharing is the lock; the operating system releases it if the lane dies.
    New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
    while ($true) {
        try { return [IO.File]::Open($LockFile, 'OpenOrCreate', 'ReadWrite', 'None') }
        catch { Write-HeartbeatIfDue; Start-Sleep -Seconds 3 }
    }
}

function Test-RemoteReachable {
    # Whether origin answers at all, so a failed fetch or push can be told apart: refused by
    # a remote that answers (another lane pushed first) or never heard (GitHub out of reach).
    & git -C $Root ls-remote -q origin "refs/heads/$Branch" 2>&1 | Out-Null
    return ($LASTEXITCODE -eq 0)
}

function Wait-RemoteReachable {
    # Waits for origin to answer, up to -Minutes, polling every -PollSeconds. An outage at
    # GitHub is no reason to park finished work, or to trace a claim that was never refused
    # as a lost race: on 2026-09-30 a few minutes of "unable to access" parked BL-892, which
    # was finished, and another lane did it again (AF-0025, BL-1281). Returns $true once
    # origin answers, $false when it never did.
    param([string]$Id = '-', [int]$Minutes = 60, [int]$PollSeconds = 30)
    if (Test-RemoteReachable) { return $true }
    Write-Trace $Id 'offline' "origin out of reach; waiting up to $Minutes min for it" 'DarkYellow'
    $deadline = (Get-Date).AddMinutes($Minutes)
    while ((Get-Date) -lt $deadline) {
        Write-HeartbeatIfDue
        Start-Sleep -Seconds $PollSeconds
        if (Test-RemoteReachable) { Write-Trace $Id 'online' 'origin answers again'; return $true }
    }
    return $false
}

function Sync-Lane {
    # Puts this lane's checkout exactly on the shared branch as it is on the remote.
    Save-StrayChanges 'sync'
    if (-not (Invoke-Git @('fetch', '-q', 'origin', $Branch))) { return $false }
    if (-not (Invoke-Git @('checkout', '-q', '-B', "factory/lane-$Lane", "origin/$Branch"))) { return $false }
    return (Invoke-Git @('reset', '-q', '--hard', "origin/$Branch"))
}

function Test-Rebasing {
    foreach ($dir in 'rebase-merge', 'rebase-apply') {
        $path = (& git -C $Root rev-parse --git-path $dir).Trim()
        if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $Root $path }
        if (Test-Path $path) { return $true }
    }
    return $false
}

function Get-DoingCount { return @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue).Count }

function Invoke-Claim {
    # Returns @{ Id = 'BL-###' } on success, or @{ Wait = $true } when every ready task
    # overlaps work in progress (or nothing is ready but other lanes may unlock more), or
    # @{ None = $true } when the board has nothing left for this shift.
    param([string[]]$Skip)
    $lock = Enter-Lock
    try {
        foreach ($attempt in 1..5) {
            if (-not (Wait-RemoteReachable)) { return @{ Wait = $true; Why = 'origin out of reach' } }
            if (-not (Sync-Lane)) { Start-Sleep -Seconds 10; continue }
            $requeued = @(Invoke-Requeue)
            if ($requeued.Count) {
                Invoke-Git @('add', '-A', 'Tasks') | Out-Null
                Invoke-Git @('commit', '-q', '-m', "chore(tasks): requeue $($requeued -join ', ') - blockers Done") | Out-Null
                if (-not (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch"))) { continue }
            }
            $boardArgs = @('next')
            if ($Skip.Count) { $boardArgs += @('-Skip', ($Skip -join ',')) }
            $next = (Invoke-Board $boardArgs) -join "`n"
            $id = Get-NextTaskId $next
            if (-not $id) {
                if ($next -match 'can start yet' -or (Get-DoingCount) -gt 0) { return @{ Wait = $true; Why = (Get-Short (Get-WaitReason $next) 80) } }
                return @{ None = $true }
            }
            Invoke-Board @('move', '-Id', $id, '-To', 'Doing') | Out-Null
            if ((Get-TaskState $id) -ne 'Doing') { continue }
            Invoke-Git @('add', '-A', 'Tasks') | Out-Null
            Invoke-Git @('commit', '-q', '-m', "chore(tasks): claim $id on dark factory lane $Lane") | Out-Null
            if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return @{ Id = $id } }
            # Only a claim that was pushed is traced as 'claim', so the log counts claims
            # truly; a refused push is 'race', and an unheard one is no race at all.
            if (Test-RemoteReachable) { Write-Trace $id 'race' 'another lane pushed first; picking again' 'DarkYellow' }
        }
        return @{ Wait = $true; Why = 'claim kept losing races' }
    } finally { $lock.Dispose() }
}

function Invoke-FastTests {
    # One fast-test run of this checkout: whether it was green, and what failed. A hung test
    # would hold the integrate lock, and so every lane, for ever: the blame collector kills
    # a test host that stops making progress, and the run counts as red.
    $output = @(& dotnet test $Root --no-build -nologo --filter 'TestCategory!=Integration' --blame-hang-timeout 10m 2>&1 | ForEach-Object { "$_" })
    return [pscustomobject]@{ Green = ($LASTEXITCODE -eq 0); Failed = (Get-FailedTestNames $output); Projects = @(Get-FailedTestProjects $output) }
}

function Test-GreenAlone {
    # Runs only the named test projects' fast tests, with no other test project competing
    # for the machine. True when every one is green, false when any is red or cannot be found.
    param([string[]]$Projects)
    if (-not $Projects.Count) { return $false }
    foreach ($project in $Projects) {
        $path = Join-Path $Root "$project\$project.csproj"
        if (-not (Test-Path $path)) { return $false }
        & dotnet test $path --no-build -nologo --filter 'TestCategory!=Integration' --blame-hang-timeout 10m 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { return $false }
    }
    return $true
}

function Test-Green {
    # Build and fast tests in this checkout, after a rebase put other lanes' work under ours.
    # Returns '' when green, or why not. Under six lanes' load a timing-sensitive test can
    # fail once and pass on the next run, which is no reason to throw a finished task away,
    # so a red run is run once more and only red twice counts (BL-898), unless the projects
    # that failed pass when run alone (AF-0092).
    param([string]$Id = '-')
    & dotnet build $Root -nologo -v q 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { return 'build failed' }
    $first = Invoke-FastTests
    if ($first.Green) { return '' }
    Write-Trace $Id 'flaky?' "fast tests red ($($first.Failed)); running them once more" 'DarkYellow'
    $second = Invoke-FastTests
    if ($second.Green) {
        Write-Trace $Id 'flaky' "failed once, passed on the rerun: $($first.Failed)" 'Yellow'
        return ''
    }
    # A concurrency-sensitive test can stay red through two full runs while every lane builds
    # at once, and then park every finished task in the window (AF-0092: BL-1647 and eight
    # more, on EveryMember_ManyConcurrentCallers_...). So the projects that failed are run
    # once more on their own: green alone means the red came from the machine's load, not
    # from this task's change, which red alone would still show.
    # A run red without naming a project vouches for nothing smaller, so both must name one.
    $projects = @(@($first.Projects) + @($second.Projects) | Select-Object -Unique)
    if ($first.Projects.Count -and $second.Projects.Count -and (Test-GreenAlone $projects)) {
        Write-Trace $Id 'flaky' "failed twice in the full run, passed alone ($($projects -join ', ')): $($first.Failed); then $($second.Failed)" 'Yellow'
        return ''
    }
    return "fast tests failed twice ($($first.Failed); then $($second.Failed))"
}

function Repair-IntegrationBreak {
    # One repair run for a finished task that went red only on the rebased tree (AF-0051),
    # while this lane still holds the integrate lock, so no other lane's push moves the tree
    # under it. Returns '' once build and fast tests are green and committed, or why not.
    param([string]$Id, [string]$Red)
    Write-Trace $Id 'repair' "$Red after the rebase; one run to fix it on the combined tree" 'DarkYellow'
    Set-HeartbeatStep @('repair', "$Red on the shared branch")
    $text = $RepairPrompt.Replace('{RED}', $Red)
    Invoke-TaskRun -Id $Id -Text $text -Deny $LaneForbidden -Suffix '-repair' -Minutes 30 | Out-Null
    # The repair's work counts only once committed: what is pushed is HEAD, not the worktree.
    if (Test-WorktreeDirty $Root) {
        Invoke-Git @('add', '-A') | Out-Null
        Invoke-Git @('commit', '-q', '-m', "fix: repair $Id on top of the other lanes' work") | Out-Null
    }
    $still = Test-Green -Id $Id
    if ($still) { return "$Red, and still $still after a repair run" }
    Write-Trace $Id 'repair' 'green on the combined tree after the repair run' 'Green'
    return ''
}

function Invoke-Integrate {
    # Rebases this lane's commits onto the shared branch, checks them, and pushes. Returns
    # '' on success or why it could not.
    param([string]$Id, [string]$State)
    Set-HeartbeatTask $Id
    Write-Heartbeat 'integrate' 'waiting for the integrate lock'
    $lock = Enter-Lock
    Write-Heartbeat 'integrate'
    try {
        foreach ($attempt in 1..3) {
            # Finished work is parked only for what origin refused, never because origin was
            # out of reach (AF-0025): each attempt starts once origin answers.
            if (-not (Wait-RemoteReachable -Id $Id)) { return 'origin stayed out of reach for an hour' }
            if (-not (Invoke-Git @('fetch', '-q', 'origin', $Branch))) { Start-Sleep -Seconds 10; continue }
            if (-not (Invoke-Git @('rebase', '-q', "origin/$Branch"))) {
                if (Test-Rebasing) {
                    Write-Trace $Id 'resolve' 'rebase conflict; resolving' 'DarkYellow'
                    Invoke-TaskRun -Id $Id -Text $ResolvePrompt -Deny $ResolveForbidden -Suffix '-resolve' -Minutes 45 | Out-Null
                    if (Test-Rebasing) { & git -C $Root rebase --abort 2>&1 | Out-Null; return 'rebase conflict the resolver could not settle' }
                } else { return 'rebase failed' }
            }
            # Lanes number the tasks they file from their own copy of the board, so two
            # lanes can file the same ID. The other lane's is already shared and keeps it;
            # ours are renumbered, with our own references, before anyone else sees them.
            $dedupe = (Invoke-Board @('dedupe', '-Since', "origin/$Branch")) | Where-Object { $_ -match '->' }
            if ($dedupe) {
                Invoke-Git @('add', '-A') | Out-Null
                Invoke-Git @('commit', '-q', '-m', "chore(tasks): renumber task IDs another lane took first`n`n$($dedupe -join "`n")") | Out-Null
                foreach ($line in $dedupe) { Write-Trace $Id 'renum' $line 'DarkYellow' }
            }
            # Keep Done short enough for Stewart to read at a glance. Only the lane holding
            # this lock archives, on top of every other lane's work, so two lanes never
            # move the same finished tasks into different folders.
            $archived = (Invoke-Board @('archive', '-WhenDoneIsLong')) | Where-Object { $_ -match '^Archived ' }
            if ($archived) {
                Invoke-Git @('add', '-A', '--', 'Tasks/Done') | Out-Null
                Invoke-Git @('commit', '-q', '-m', "chore(tasks): archive Done once it grew past the short list`n`n$archived") | Out-Null
                Write-Trace $Id 'archive' $archived
            }
            if ($State -eq 'Done') {
                Set-HeartbeatStep @('verify', 'build and fast tests on the shared branch')
                $red = Test-Green -Id $Id
                if ($red) { $red = Repair-IntegrationBreak -Id $Id -Red $red }
                if ($red) { return "$red after rebasing onto the other lanes' work" }
                Write-Trace $Id 'verify' 'build and fast tests green on the shared branch'
            }
            if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return '' }
        }
        return 'push kept being refused'
    } finally { $lock.Dispose() }
}

function Invoke-Park {
    # Work that will not integrate is kept on a branch of its own, and the task goes to
    # back to Backlog on the shared branch, so a later run picks it up from that branch and
    # fixes what broke. Integration trouble is Claude's to solve, not Stewart's.
    # Returns '' once the task has left Doing on the shared branch, or a line for the lane's
    # summary and the end-of-shift report saying it has not (BL-1071). The push that failed
    # to integrate often failed because the remote was out of reach, so the move is retried
    # for several minutes, waiting -RetrySeconds between tries.
    param([string]$Id, [string]$Why, [int[]]$RetrySeconds = @(10, 30, 60, 120, 300))
    $park = "factory/$Id-lane-$Lane-$Stamp"
    # A local branch too: lanes may not fetch, but every worktree sees local branches.
    Invoke-Git @('branch', '-f', $park, 'HEAD') | Out-Null
    Invoke-Git @('push', '-q', 'origin', "HEAD:refs/heads/$park") | Out-Null
    $lock = Enter-Lock
    try {
        foreach ($wait in @(0) + $RetrySeconds) {
            if ($wait) { Start-Sleep -Seconds $wait }
            if (-not (Sync-Lane)) { continue }
            if ((Get-TaskState $Id) -ne 'Doing') { return '' }
            Invoke-Board @('move', '-Id', $Id, '-To', 'Backlog', '-Reason', "Lane $Lane could not integrate: $Why. The work is on branch $park; start with git cherry-pick --no-commit $park and fix it.") | Out-Null
            Invoke-Git @('add', '-A', 'Tasks') | Out-Null
            Invoke-Git @('commit', '-q', '-m', "chore(tasks): park $Id - $Why") | Out-Null
            if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return '' }
        }
    } finally { $lock.Dispose() }
    return "$Id PARK NOT PUSHED  $Why, and its move to Backlog never reached $Branch either, so it is still in Doing there. The work is on local branch $park; the next shift returns the task to Backlog."
}

function Get-ParkedBranches {
    # The branches Invoke-Park kept task $Id's work on, local or on origin, by short name.
    param([string]$Id, [string]$Repo = $Root)
    $refs = @(git -C $Repo for-each-ref --format='%(refname:short)' "refs/heads/factory/$Id-lane-*" "refs/remotes/origin/factory/$Id-lane-*" 2>$null)
    return @($refs | Where-Object { $_ } | ForEach-Object { "$_" -replace '^origin/', '' } | Sort-Object -Unique)
}

function Test-WorktreeDirty {
    # Whether the worktree at $Dir exists and has uncommitted work.
    param([string]$Dir)
    if (-not (Test-Path (Join-Path $Dir '.git'))) { return $false }
    return [bool](@(git -C $Dir status --porcelain 2>$null) | Where-Object { $_ })
}

function Get-OrphanAction {
    # What shift start does with task $Id, in Doing but held by no lane of the previous shift
    # (BL-1071): adopt it into the one lane worktree with uncommitted work, so that lane
    # resumes it; return it to Backlog when no worktree has work and a parked branch holds
    # it; otherwise refuse, saying why. -DirtyLanes are the lanes not already adopted whose
    # worktrees have uncommitted work.
    param([string]$Id, [string[]]$ParkedBranches, [int[]]$DirtyLanes)
    $dirty = @($DirtyLanes | Where-Object { $_ })
    if ($dirty.Count -eq 1) { return [pscustomobject]@{ Action = 'adopt'; Lane = $dirty[0]; Branch = ''; Why = '' } }
    if ($dirty.Count -gt 1) {
        return [pscustomobject]@{ Action = 'refuse'; Lane = 0; Branch = ''; Why = "$Id is in Doing and held by no lane, and lanes $($dirty -join ', ') have uncommitted work; which one is its work cannot be told" }
    }
    $parked = @($ParkedBranches | Where-Object { $_ } | Sort-Object)
    if ($parked.Count) { return [pscustomobject]@{ Action = 'backlog'; Lane = 0; Branch = $parked[-1]; Why = '' } }
    return [pscustomobject]@{ Action = 'refuse'; Lane = 0; Branch = ''; Why = "$Id is in Doing and held by no lane, no lane worktree has uncommitted work and no parked branch factory/$Id-lane-* exists" }
}

function Stop-ShiftStart {
    # A shift that refuses to start says why: in the trace, and in the alarm that is the
    # end-of-shift notice, so the factory never stops silently (BL-1071). The caller exits.
    param([string]$Why, [scriptblock]$Notify = { param([string[]]$Reasons) Set-OwnTabLabel 'ALARM, read'; Invoke-Alarm -Reasons $Reasons })
    Write-Trace '-' 'refuse' $Why 'Red'
    & $Notify -Reasons @("SHIFT REFUSED  $Why", 'No shift runs until this is cleared and a shift is started again.')
}

if ($TestPark) {
    # Lane 9 of a made-up shift parks two tasks in a throwaway repository with a bare origin:
    # the first while origin answers, the second after origin has gone.
    $temp = Join-Path ([IO.Path]::GetTempPath()) "df-park-$PID"
    $origin = Join-Path $temp 'origin.git'
    $repo = Join-Path $temp 'lane-9'
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    git init -q --bare -b work $origin 2>&1 | Out-Null
    git clone -q $origin $repo 2>&1 | Out-Null
    git -C $repo config user.name t; git -C $repo config user.email t@t
    git -C $repo checkout -q -b work 2>&1 | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $repo 'Tasks\Doing'), (Join-Path $repo 'Tasks\Backlog') | Out-Null
    foreach ($id in 'BL-001', 'BL-002') {
        Set-Content -Path (Join-Path $repo "Tasks\Doing\$id-park-me.md") -Encoding UTF8 -Value @(
            '---', "id: $id", 'title: Park me', 'priority: Normal', 'assignee: Claude', 'pipeline: direct',
            'depends-on: []', 'touches: [x]', 'requirement: none', 'created: 2026-10-01', 'completed:', '---',
            "# $id - Park me", '', '## Goal', '', '## Context', '', '## Acceptance criteria', '', '- [x] Parked.', '', '## Notes', '', '## Log', '', '- 2026-10-01: Created.')
    }
    git -C $repo add -A 2>&1 | Out-Null
    git -C $repo commit -q -m board 2>&1 | Out-Null
    git -C $repo push -q origin work 2>&1 | Out-Null
    $script:Root = $repo; $script:Branch = 'work'; $script:Lane = 9; $script:Stamp = 'test'
    $script:LanesDir = $temp; $script:LockFile = Join-Path $temp 'integrate.lock'; $script:LogDir = Join-Path $temp 'logs'
    $env:CLAUDE_PROJECT_DIR = $repo
    $quiet = { param([string[]]$Reasons) $script:Alarmed = $Reasons }
    $failed = 0
    $cases = @(
        ,@('an origin that answers is reachable at once', 'True', { "$(Wait-RemoteReachable -Minutes 0 -PollSeconds 0)" })
        ,@('a park pushes its move to Backlog, naming the branch', "'' Backlog factory/BL-001-lane-9-test", {
            Set-Content -Path (Join-Path $repo 'work.txt') -Value 'work'
            git -C $repo add -A 2>&1 | Out-Null; git -C $repo commit -q -m work 2>&1 | Out-Null
            $r = Invoke-Park -Id 'BL-001' -Why 'push kept being refused' -RetrySeconds @()
            $shared = @(git -C $repo ls-tree -r --name-only origin/work Tasks) -join ' '
            $log = (git -C $repo show 'origin/work:Tasks/Backlog/BL-001-park-me.md') -join ' '
            "'$r' $(if ($shared -match 'Backlog/BL-001-' -and $shared -notmatch 'Doing/BL-001-') { 'Backlog' } else { 'Doing' }) $(if ($log -match 'branch (factory/BL-001-lane-9-test);') { $Matches[1] } else { 'no branch in the Log' })" })
        ,@('a park that cannot push says so for the summary', 'BL-002 PARK NOT PUSHED, still in Doing, branch kept', {
            git -C $repo remote set-url origin (Join-Path $temp 'gone.git') 2>&1 | Out-Null
            # The git failures it traces are expected here, so they are not printed.
            $r = Invoke-Park -Id 'BL-002' -Why 'push kept being refused' -RetrySeconds @(0) 6>$null
            "$(if ($r -match '^BL-002 PARK NOT PUSHED ') { 'BL-002 PARK NOT PUSHED' } else { "'$r'" }), $(if ($r -match 'still in Doing') { 'still in Doing' } else { 'state not said' }), $(if ((Get-ParkedBranches 'BL-002' $repo) -contains 'factory/BL-002-lane-9-test') { 'branch kept' } else { 'no branch' })" })
        ,@('an origin out of reach is waited for, then reported unreachable', 'False offline', {
            New-Item -ItemType Directory -Force -Path $script:LogDir | Out-Null
            $script:TraceFile = Join-Path $script:LogDir 'trace.log'
            $r = Wait-RemoteReachable -Minutes 0 -PollSeconds 0 6>$null
            $traced = (Get-Content -Path $TraceFile -ErrorAction SilentlyContinue) -join ' '
            "$r $(if ($traced -match 'offline +origin out of reach') { 'offline' } else { 'not traced' })" })
        ,@('an orphan with a parked branch and clean lanes goes to Backlog', 'backlog factory/BL-002-lane-9-test', {
            $a = Get-OrphanAction -Id 'BL-002' -ParkedBranches (Get-ParkedBranches 'BL-002' $repo) -DirtyLanes @(@(9) | Where-Object { Test-WorktreeDirty $repo })
            "$($a.Action) $($a.Branch)" })
        ,@('an orphan with uncommitted work in one lane is adopted by it', 'adopt 9', {
            Set-Content -Path (Join-Path $repo 'half-done.txt') -Value 'half'
            $a = Get-OrphanAction -Id 'BL-002' -ParkedBranches (Get-ParkedBranches 'BL-002' $repo) -DirtyLanes @(@(9) | Where-Object { Test-WorktreeDirty $repo })
            "$($a.Action) $($a.Lane)" })
        ,@('an orphan with no work anywhere is refused, saying why', 'refuse no parked branch', {
            $a = Get-OrphanAction -Id 'BL-003' -ParkedBranches (Get-ParkedBranches 'BL-003' $repo) -DirtyLanes @()
            "$($a.Action) $(if ($a.Why -match 'no parked branch factory/BL-003-lane-\* exists') { 'no parked branch' } else { $a.Why })" })
        ,@('a refused start raises the alarm with its reason', 'SHIFT REFUSED  BL-003 is in Doing', {
            $script:Alarmed = @()
            Stop-ShiftStart -Why 'BL-003 is in Doing' -Notify $quiet
            "$($script:Alarmed | Select-Object -First 1)" }))
    foreach ($case in $cases) {
        $got = & $case[2]
        if ($case[1] -ceq $got) { Write-Host "PASS $($case[0]): $got" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $got" -ForegroundColor Red; $failed++ }
    }
    Remove-Item -Recurse -Force -Path $temp -ErrorAction SilentlyContinue
    exit $(if ($failed) { 1 } else { 0 })
}

function Write-LaneSummary {
    param([string[]]$Lines)
    $dir = Join-Path $LogDir "lanes-$Stamp"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Set-Content -Path (Join-Path $dir "lane-$Lane.txt") -Value $Lines -Encoding UTF8
}

# Lane state, in <repo>.logs\lanes-<stamp>\: lane-<n>.pid (the lane's process, so the coordinator
# can tell a dead lane from a busy one) and lane-<n>.task (the task it holds in Doing, so a
# restarted lane - or the next shift - resumes it instead of losing it).
function Get-LaneStatePath {
    param([int]$N, [string]$Kind, [string]$ForStamp = $Stamp)
    return (Join-Path (Join-Path $LogDir "lanes-$ForStamp") "lane-$N.$Kind")
}

function Set-LaneState {
    param([string]$Kind, [string]$Value)
    $path = Get-LaneStatePath $Lane $Kind
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    if ($Value) { Set-Content -Path $path -Value $Value -Encoding ASCII } else { Remove-Item $path -ErrorAction SilentlyContinue }
}

function Get-LaneState {
    param([int]$N, [string]$Kind, [string]$ForStamp = $Stamp)
    $path = Get-LaneStatePath $N $Kind $ForStamp
    if (Test-Path $path) { return "$(Get-Content $path -TotalCount 1)".Trim() }
    return ''
}

# Tasks a lane of this shift sent back to Backlog (requeued or parked), one ID a line in
# <repo>.logs\lanes-<stamp>\requeued.txt. No lane of the same shift claims one again: a
# task that fell short once - BL-1525 missed its timing target under eight lanes' load -
# would only fall short again on the next lane, handing its work through the stash each
# time (AF-0072). The next shift tries it afresh.
function Get-ShiftRequeuedPath { return (Join-Path (Join-Path $LogDir "lanes-$Stamp") 'requeued.txt') }

function Add-ShiftRequeued {
    param([string]$Id)
    $path = Get-ShiftRequeuedPath
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    Add-Content -Path $path -Value $Id -Encoding ASCII
}

function Get-ShiftRequeued {
    $path = Get-ShiftRequeuedPath
    if (-not (Test-Path $path)) { return @() }
    return @(Get-Content $path | ForEach-Object { "$_".Trim() } | Where-Object { $_ } | Select-Object -Unique)
}

function Test-LaneAlive {
    # True while lane <n>'s process runs. No pid file yet means it is still starting.
    param([int]$N, [string]$ForStamp = $Stamp)
    $lanePid = Get-LaneState $N 'pid' $ForStamp
    if (-not $lanePid) { return $true }
    $p = Get-Process -Id ([int]$lanePid) -ErrorAction SilentlyContinue
    return [bool]($p -and $p.ProcessName -match 'powershell')
}

function Test-TokensAvailable {
    # Asks Claude for one word. $true when it answers, $false when the usage limit (or any
    # API failure) refuses it. Used where a run failed without saying why.
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $env:ComSpec
        $psi.Arguments = "/d /c claude -p --model $ProbeModel --output-format stream-json --verbose --max-turns 1 2>nul"
        $psi.WorkingDirectory = $Root
        $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $p = [System.Diagnostics.Process]::Start($psi)
        $p.StandardInput.Write('Reply with the single word OK.')
        $p.StandardInput.Close()
        $read = $p.StandardOutput.ReadToEndAsync()
        if (-not $p.WaitForExit(120000)) { & taskkill /T /F /PID $p.Id 2>&1 | Out-Null; return $false }
        $out = $read.Result
    } catch { return $false }
    return ($out -match '"type":"result"' -and $out -notmatch '"status":"rejected"' -and $out -notmatch '"is_error":true')
}

function Test-ApiFailure {
    # A run that died on the API rather than on the work: no result at all, or an error
    # result the API gave (the usage limit in a form Get-OutOfTokensUntil did not know, an
    # overload, an outage).
    param($Run)
    if ($Run.TimedOut) { return $false }
    $r = $script:RunResult
    if ($null -eq $r) { return ($Run.ExitCode -ne 0) }
    return ([bool]($r.PSObject.Properties['is_error'] -and $r.is_error) -and
        ([bool]$r.PSObject.Properties['api_error_status'] -or "$($r.terminal_reason)" -eq 'api_error'))
}

function Test-BudgetSpent {
    # Whether the last run stopped because it reached the -TaskBudgetUsd cost cap.
    $r = $script:RunResult
    return ($null -ne $r -and "$($r.subtype)" -eq 'error_max_budget_usd')
}

function Wait-ForTokensByProbe {
    # Holds this runner until a one-word probe is answered, checking every 5 minutes, and
    # returns how long it waited.
    param([string]$Id)
    $began = Get-Date
    Write-Trace $Id 'tokens' 'run failed on the API; waiting until Claude answers again' 'Yellow'
    Write-Heartbeat 'tokens' 'waiting until Claude answers again'
    while (-not (Test-TokensAvailable)) {
        if ($Lane) { try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane waiting for the API" } catch { } }
        # Refreshed every minute of the wait, so the page never shows this lane as stale.
        foreach ($minute in 1..5) { Write-Heartbeat; Start-Sleep -Seconds 60 }
    }
    Write-Trace $Id 'resume' 'Claude answers again; running the task again' 'Green'
    return ((Get-Date) - $began)
}

if ($TestHeartbeat) {
    # Lane 1 through its phases in a temporary log root, printing its file after each. No
    # board, git or Claude: the task is a made-up BL-000 with a title given here.
    $Lane = 1
    $LogDir = Join-Path ([IO.Path]::GetTempPath()) "DarkFactoryHeartbeat-$Stamp"
    $WritesHeartbeat = $true
    $heartbeatFile = Get-LaneStatePath $Lane 'heartbeat.json'
    try {
        Write-Heartbeat 'starting'
        Get-Content -Raw $heartbeatFile
        Write-Heartbeat 'claim'
        Get-Content -Raw $heartbeatFile
        Set-HeartbeatTask 'BL-000' -Title 'Rehearse the heartbeat file'
        # The model fields Set-ModelChoice fills from the task file (BL-1705).
        $script:Beat.Model = 'sonnet'; $script:Beat.ModelWhy = 'direct pipeline'
        Write-Heartbeat 'run'
        Set-HeartbeatStep @('build', '')
        Get-Content -Raw $heartbeatFile
        Write-Heartbeat 'integrate'
        Get-Content -Raw $heartbeatFile
        Set-HeartbeatTask ''
        Write-Heartbeat 'finished'
        Get-Content -Raw $heartbeatFile
        # Three made-up lanes, written out of order, merged into status.json and built into
        # the board branch's commit. Never pushed: lanes are denied git push, and the first
        # real push is the next shift's.
        foreach ($fake in @(
            @{ Lane = 3; Task = $null; Title = $null; Phase = 'wait'; Step = 'nothing can start yet' },
            @{ Lane = 1; Task = 'BL-001'; Title = 'Rehearse lane one'; Phase = 'run'; Step = 'build' },
            @{ Lane = 2; Task = 'BL-002'; Title = 'Rehearse lane two'; Phase = 'integrate'; Step = '' })) {
            $Lane = $fake.Lane
            $script:Beat.Task = $null
            Set-HeartbeatTask $fake.Task -Title $fake.Title
            if ($fake.Task) { $script:Beat.Model = 'opus'; $script:Beat.ModelWhy = 'feature pipeline' }
            Write-Heartbeat $fake.Phase $fake.Step
        }
        # A made-up Auto step, so the merged file carries an autoLanes object.
        Set-AutoLanesStatus -Lanes 4 -Target 5.24 -Binding 'weekly pace' -Reason 'lanes 3 -> 4 (weekly pace allows 5.2)'
        $json = Get-BoardStatusJson -Branch 'work/dark-factory'
        $json
        New-BoardCommit $json
    } finally { Remove-Item $LogDir -Recurse -Force -ErrorAction SilentlyContinue }
    exit 0
}

# ---- auto lanes: the shift's reads and writes (ADR-0130 items 1, 5, 7 and 8)

$AutoLanesFile = Join-Path $LanesDir 'auto-lanes.json'
# A detached worktree the coordinator reads the board's capacity from, so its own checkout
# is pulled only at shift end: lanes run its copy of this script.
$AutoBoardDir = Join-Path $LanesDir 'auto-board'
# The samples of the last 3 hours, the rates last metered, the saved auto-lanes.json, the
# machine cap, and whether a capacity failure has been traced yet.
$script:Auto = @{ Samples = @(); FiveHourRate = $null; WeeklyRate = $null; Saved = $null; MachineCap = 16; CapacityTraced = $false; LastLow = $false }

function Read-JsonFile {
    # The file's JSON, or $null when it is missing or does not parse.
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    try { return (Get-Content -Raw -Path $Path | ConvertFrom-Json) } catch { return $null }
}

function Get-MachineProbeNeed {
    # Why the machine cap must be measured again - no probe yet, an incomplete one, or other
    # hardware - or '' while machine-lanes.json still describes this PC.
    param($Record)
    if (-not $Record) { return 'no probe yet' }
    if (-not $Record.complete) { return 'the last probe was incomplete' }
    if ("$($Record.rule)" -ne $MachineProbeRule) { return 'the probe rule changed' }
    if ([int]$Record.logicalProcessors -ne [Environment]::ProcessorCount) { return 'the processor count changed' }
    $memoryGB = [double](Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB
    if ([math]::Abs([double]$Record.memoryGB - $memoryGB) -gt 1) { return 'the memory size changed' }
    return ''
}

function Get-MachineCap {
    # This PC's lane cap and the line saying where it came from. -Probe measures it when
    # machine-lanes.json is missing, incomplete or from other hardware; without -Probe the
    # cap is then unknown and does not limit the lanes.
    param([switch]$Probe)
    $record = Read-JsonFile $MachineFile
    $need = Get-MachineProbeNeed $record
    if ($need -and -not $Probe) { return [pscustomobject]@{ Cap = 16; Text = "machine cap unknown ($need)" } }
    if ($need) {
        Write-Trace '-' 'probe' "measuring the machine cap first: $need" 'Cyan'
        Invoke-MachineProbe | Out-Null
        $record = Read-JsonFile $MachineFile
        if (-not $record) { return [pscustomobject]@{ Cap = 16; Text = 'machine cap unknown (the probe wrote no file)' } }
    }
    $cap = [math]::Max(1, [int]$record.cap)
    return [pscustomobject]@{ Cap = $cap; Text = "machine cap $cap lanes (probed $(([datetime]$record.probedAt).ToString('yyyy-MM-dd')))" }
}

function Get-BoardCapacity {
    # How many tasks the board at -Ref could run at once, from the capacity command of the
    # task board in the $AutoBoardDir worktree checked out at -Ref. Created returns whether
    # this call made the worktree; Capacity is $null, with Why, when it could not be read.
    param([string]$Ref)
    $created = $false
    $saved = $env:CLAUDE_PROJECT_DIR
    try {
        if (-not (Test-Path (Join-Path $AutoBoardDir '.git'))) {
            New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
            git -C $Root worktree add -q --detach $AutoBoardDir $Ref 2>&1 | Out-Null
            if ($LASTEXITCODE) { return [pscustomobject]@{ Capacity = $null; Created = $false; Why = "cannot add the worktree $AutoBoardDir" } }
            $created = $true
        }
        git -C $AutoBoardDir checkout -q --detach $Ref 2>&1 | Out-Null
        if ($LASTEXITCODE) { return [pscustomobject]@{ Capacity = $null; Created = $created; Why = "cannot check out $Ref in $AutoBoardDir" } }
        $env:CLAUDE_PROJECT_DIR = $AutoBoardDir
        $out = (& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $AutoBoardDir '.claude\skills\task-board\task-board.ps1') capacity 2>&1) -join "`n"
        if ($out -match '(?m)^Capacity (\d+):') { return [pscustomobject]@{ Capacity = [int]$Matches[1]; Created = $created; Why = '' } }
        return [pscustomobject]@{ Capacity = $null; Created = $created; Why = "the board printed no capacity: $(Get-Short $out 60)" }
    } catch {
        return [pscustomobject]@{ Capacity = $null; Created = $created; Why = $_.Exception.Message }
    } finally { $env:CLAUDE_PROJECT_DIR = $saved }
}

function Remove-AutoBoard {
    # Deletes the $AutoBoardDir worktree.
    git -C $Root worktree remove --force $AutoBoardDir 2>&1 | Out-Null
    git -C $Root worktree prune 2>&1 | Out-Null
}

function Get-AutoLaneStep {
    # One Auto step's computation from -Samples, newest last: both burn rates (the saved
    # ones while the samples give none), the pace target and the next lane count.
    param([object[]]$Samples, [int]$Current, [int]$Capacity, [int]$MachineCap, $Saved, [bool]$PreviousLow = $false)
    $fiveHourRate = Get-BurnRate -Samples $Samples -Window FiveHour
    if ($null -eq $fiveHourRate -and $Saved -and $null -ne $Saved.fiveHourRatePerLane) { $fiveHourRate = [double]$Saved.fiveHourRatePerLane }
    $weeklyRate = Get-BurnRate -Samples $Samples -Window Week
    if ($null -eq $weeklyRate -and $Saved -and $null -ne $Saved.weeklyRatePerLane) { $weeklyRate = [double]$Saved.weeklyRatePerLane }
    $pace = $null
    if (@($Samples).Count) {
        $pace = Get-PaceTarget -Sample @($Samples)[-1] -FiveHourRate $fiveHourRate -WeeklyRate $weeklyRate -WeeklyPace $WeeklyPace.IsPresent `
            -StopAtUsage $StopAtUsage -StopAtWeeklyUsage $StopAtWeeklyUsage
    }
    $next = Get-NextLaneCount -Current $Current -Pace $pace -Capacity $Capacity -MachineCap $MachineCap -MaxLanes $MaxLanes -PreviousLow $PreviousLow
    return [pscustomobject]@{ FiveHourRate = $fiveHourRate; WeeklyRate = $weeklyRate; Pace = $pace; Next = $next }
}

function Save-AutoLanes {
    # Writes auto-lanes.json: -Count lanes and the rates last metered, for the next shift's
    # cold start. A temporary file and a rename, so a reader never sees half of one.
    param([int]$Count)
    $record = [ordered]@{
        schema = 1; lanes = $Count; savedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        fiveHourRatePerLane = $(if ($null -ne $script:Auto.FiveHourRate) { [math]::Round([double]$script:Auto.FiveHourRate, 3) } else { $null })
        weeklyRatePerLane = $(if ($null -ne $script:Auto.WeeklyRate) { [math]::Round([double]$script:Auto.WeeklyRate, 3) } else { $null })
    }
    try {
        New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
        $temp = "$AutoLanesFile.tmp"
        [IO.File]::WriteAllText($temp, ($record | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
        Move-Item -Force -Path $temp -Destination $AutoLanesFile
    } catch { Write-Trace '-' 'lanes' "cannot save $($AutoLanesFile): $($_.Exception.Message)" 'Yellow' }
}

function Format-Rate {
    # A rate or target with one decimal, invariant culture; 'none yet' for $null, 'unbounded' for infinity.
    param($Value)
    if ($null -eq $Value) { return 'none yet' }
    if ([double]::IsPositiveInfinity([double]$Value)) { return 'unbounded' }
    return ([double]$Value).ToString('0.0', [Globalization.CultureInfo]::InvariantCulture)
}

if ($AutoLanesReport) {
    # The shift-start reads and one step's computation from the newest readings in $LogDir,
    # printed; never the probe, a lane, a claim or a push. The board is read at origin/-Branch
    # when -Branch is given, otherwise at this checkout's commit.
    $machine = Get-MachineCap
    Write-Host $machine.Text
    $ref = (git -C $Root rev-parse HEAD).Trim()
    if ($Branch) {
        git -C $Root fetch -q origin $Branch 2>&1 | Out-Null
        $ref = "origin/$Branch"
    }
    $board = Get-BoardCapacity -Ref $ref
    if ($board.Created) { Remove-AutoBoard }
    $saved = Read-JsonFile $AutoLanesFile
    $capacity = $board.Capacity
    if ($null -eq $capacity) { Write-Host "capacity unknown ($($board.Why)); counted as $MaxLanes"; $capacity = $MaxLanes }
    else { Write-Host "capacity $capacity (board at $ref)" }
    $start = Get-AutoStartCount -Saved $saved -Capacity $capacity -MachineCap $machine.Cap
    $reading = Get-UsageReading
    $samples = @()
    if ($reading) { $samples = @(New-UsageSample -At (Get-Date) -Reading $reading -ActiveLanes 0) }
    $step = Get-AutoLaneStep -Samples $samples -Current $start.Count -Capacity $capacity -MachineCap $machine.Cap -Saved $saved
    $savedNote = if ($saved) { " (saved $($saved.savedAt))" } else { '' }
    Write-Host "5-hour rate $(Format-Rate $step.FiveHourRate)$(if ($null -ne $step.FiveHourRate) { ' points/hour/lane' })$savedNote"
    Write-Host "weekly rate $(Format-Rate $step.WeeklyRate)$(if ($null -ne $step.WeeklyRate) { ' points/hour/lane' })$savedNote"
    if ($reading) {
        Write-Host ("usage 5-hour {0}% (reset {1}), weekly {2}% (reset {3})" -f [math]::Round($reading.FiveHour * 100),
            $reading.FiveHourResets.ToString('HH:mm'), [math]::Round($reading.Week * 100), $reading.WeekResets.ToString('ddd HH:mm'))
    } else { Write-Host "usage unknown (no reading in $LogDir)" }
    if ($step.Pace) { Write-Host "5-hour target $(Format-Rate $step.Pace.FiveHour) lanes, weekly target $(Format-Rate $step.Pace.Weekly) lanes$(if (-not $WeeklyPace) { ' (not pacing weekly)' })" }
    else { Write-Host '5-hour target none yet, weekly target none yet' }
    Write-Host "start count $($start.Count) ($($start.Why))"
    Write-Host $step.Next.Reason
    exit 0
}

# ---------------------------------------------------------------------------- restart

function Select-LanesToStop {
    # The lanes -Restart may stop now, from -Phases (lane number -> heartbeat phase, '' when
    # it has none): all but a lane mid-claim, which could leave a task in Doing that no
    # lane holds, or mid-integration, which could leave a half-finished rebase (BL-895).
    param([hashtable]$Phases)
    return @($Phases.Keys | Where-Object { $Phases[$_] -notin 'claim', 'integrate' } | Sort-Object)
}

if ($TestRestart) {
    $phases = @{ 1 = 'run'; 2 = 'integrate'; 3 = 'claim'; 4 = 'tokens'; 5 = 'wait'; 6 = 'finished'; 7 = '' }
    $got = (Select-LanesToStop $phases) -join ','
    $ok = $got -ceq '1,4,5,6,7'
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) stop run, tokens, wait, finished and no heartbeat; wait for claim and integrate: $got" -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    $none = (Select-LanesToStop @{ 1 = 'claim'; 2 = 'integrate' }).Count
    $ok2 = $none -eq 0
    Write-Host "$(if ($ok2) { 'PASS' } else { 'FAIL' }) nothing stops while every lane claims or integrates: $none" -ForegroundColor $(if ($ok2) { 'Green' } else { 'Red' })
    exit $(if ($ok -and $ok2) { 0 } else { 1 })
}

if ($Restart) {
    # This checkout's coordinator only: another repository's factory runs its own copy.
    $mine = [regex]::Escape($PSCommandPath)
    $coordinator = Get-CimInstance Win32_Process | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -match "-File\s+`"?$mine`"?(\s|$)" -and
        $_.CommandLine -notmatch '\s-Lane\s' -and $_.CommandLine -notmatch '\s-(Restart|NewTab)\b'
    } | Select-Object -First 1
    if (-not $coordinator) { Write-Host 'No dark factory shift is running in this checkout.'; exit 1 }
    # The same arguments, so a fixed lane count, -WeeklyPace or -Hours carry over.
    $rest = ($coordinator.CommandLine -split [regex]::Escape((Split-Path $PSCommandPath -Leaf)), 2)[1]
    $forward = @([regex]::Matches("$rest".Trim().TrimStart('"').Trim(), '"[^"]*"|\S+') | ForEach-Object { $_.Value })
    $branchNow = "$(git -C $Root rev-parse --abbrev-ref HEAD)".Trim()
    if ($forward -notcontains '-ShiftBranch' -and $branchNow -notin 'master', 'main', 'HEAD') { $forward += @('-ShiftBranch', $branchNow) }
    $lanesDir = Get-ChildItem $LogDir -Directory -Filter 'lanes-*' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
    $stamp = if ($lanesDir) { $lanesDir.Name.Substring(6) } else { '' }
    Write-Host "Stopping the shift's coordinator (pid $($coordinator.ProcessId)) first, so it restarts no lane."
    taskkill /PID $coordinator.ProcessId /T /F 2>&1 | Out-Null
    while ($stamp) {
        $phases = @{}
        foreach ($n in 1..16) {
            $lanePid = Get-LaneState $n 'pid' $stamp
            if (-not $lanePid -or -not (Get-Process -Id ([int]$lanePid) -ErrorAction SilentlyContinue | Where-Object ProcessName -eq 'powershell')) { continue }
            $beat = Get-LaneStatePath $n 'heartbeat.json' $stamp
            $phases[$n] = if (Test-Path $beat) { "$((Get-Content $beat -Raw | ConvertFrom-Json).phase)" } else { '' }
        }
        if (-not $phases.Count) { break }
        foreach ($n in Select-LanesToStop $phases) {
            taskkill /PID ([int](Get-LaneState $n 'pid' $stamp)) /T /F 2>&1 | Out-Null
            Write-Host "$(Get-Date -Format 'HH:mm:ss') lane $n stopped ($(if ($phases[$n]) { $phases[$n] } else { 'no heartbeat' }); $(Get-LaneState $n 'task' $stamp))"
        }
        $waiting = @($phases.Keys | Where-Object { $phases[$_] -in 'claim', 'integrate' })
        if ($waiting.Count) { Start-Sleep -Seconds 5 }
    }
    $where = Start-Detached -Label 'DF shift starting' -Dir $Root -ScriptArgs $forward
    Write-Host "New shift started ($(if ($where.Tab) { "herdr tab $($where.Tab)" } else { "pid $($where.Process.Id)" })) with: $($forward -join ' ')"
    exit 0
}

# ---------------------------------------------------------------------------- shift

# Every launcher and self-test has exited by now, so this process runs work: mark it and
# everything it starts (LANE MARKER in the header).
$env:CURL_DARK_FACTORY_LANE = Get-LaneMarker -ForLane $Lane
$LaneMarkerTrace = "lane-marker=CURL_DARK_FACTORY_LANE=$env:CURL_DARK_FACTORY_LANE"

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
# Shifts start between audits (BL-1022): while Audit\RunAudit.ps1 runs, refuse, or with
# -Continuous wait and look again every 5 minutes. Lanes are started by a shift that already
# passed this, so only the coordinator or a single runner checks.
if (-not $Lane) {
    while ($true) {
        $decision = Get-ShiftStartDecision @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Select-Object CommandLine) $Continuous.IsPresent
        if ($decision -eq 'start') { break }
        if ($decision -eq 'refuse') { Write-Trace '-' 'refuse' 'An audit is running; shifts start between audits.' 'Red'; exit 1 }
        Write-Trace '-' 'audit' 'An audit is running; shifts start between audits. Looking again in 5 minutes.' 'Yellow'
        Start-Sleep -Seconds 300
    }
}
# Logs used to be written to logs\ inside the checkout. Move them beside it, where the
# next shift looks for the lanes it adopts - but never while a lane still writes there.
$oldLogDir = Join-Path $Root 'logs'
if (-not $Lane -and -not $LogRoot -and (Test-Path $oldLogDir)) {
    # Only the newest shift can still be running; a lane is a powershell process.
    $newest = Get-ChildItem $oldLogDir -Directory -Filter 'lanes-*' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
    $pids = if ($newest) { @(Get-ChildItem $newest.FullName -Filter 'lane-*.pid') } else { @() }
    $writing = @($pids | Where-Object { $p = "$(Get-Content $_.FullName -TotalCount 1)".Trim(); $p -match '^\d+$' -and (Get-Process -Id ([int]$p) -ErrorAction SilentlyContinue | Where-Object ProcessName -eq 'powershell') })
    if (-not $writing.Count) {
        Get-ChildItem $oldLogDir -Force | Move-Item -Destination $LogDir -Force -ErrorAction SilentlyContinue
        if (-not (Get-ChildItem $oldLogDir -Force -ErrorAction SilentlyContinue)) { Remove-Item $oldLogDir -Force -ErrorAction SilentlyContinue }
        Write-Trace '-' 'logs' "moved logs\ to $LogDir"
    }
}
$shiftEnd = (Get-Date).AddHours($Hours)

# ------------------------------------------------ coordinator: start lanes, wait, alarm

# Auto always coordinates lanes, even at one lane.
if (($AutoLanes -or $LaneCount -gt 1) -and -not $Lane) {
    try { $Host.UI.RawUI.WindowTitle = "Dark factory - $(if ($AutoLanes) { 'auto' } else { $LaneCount }) lanes" } catch { }
    if ($ShiftBranch) { $moved = Restore-ShiftBranch -Branch $ShiftBranch; if ($moved) { Write-Trace '-' 'branch' $moved 'Red' } }
    $branch = (git -C $Root rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -in 'master', 'main') { Stop-ShiftStart "on $branch; switch to a feature branch"; exit 1 }
    if (Get-Dirty) { Stop-ShiftStart 'working tree not clean; commit or stash first'; exit 1 }
    git -C $Root fetch -q origin $branch
    $syncRefusal = Sync-CheckoutWithOrigin -Repo $Root -Branch $branch
    if ($syncRefusal) { Stop-ShiftStart $syncRefusal; exit 1 }
    # Tasks in Doing are only allowed when a previous shift's lane holds each of them and that
    # lane is dead - a shift stopped mid-task, or killed while waiting for tokens. Those lanes
    # are adopted: their worktrees are left as they are and they resume the task. An orphan,
    # held by no lane, is settled by Get-OrphanAction (BL-1071).
    $stuck = @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue | ForEach-Object { Get-TaskIdFromFileName $_.Name })
    $adopt = @{}
    if ($stuck.Count) {
        $previous = Get-ChildItem $LogDir -Directory -Filter 'lanes-*' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
        if ($previous) {
            $prevStamp = $previous.Name.Substring(6)
            foreach ($n in 1..16) {
                $held = Get-LaneState $n 'task' $prevStamp
                if (-not $held -or $stuck -notcontains $held) { continue }
                if ((Get-LaneState $n 'pid' $prevStamp) -and (Test-LaneAlive $n $prevStamp)) { Stop-ShiftStart "lane $n of shift $prevStamp is still running $held"; exit 1 }
                $adopt[$n] = $held
            }
        }
        $orphans = @($stuck | Where-Object { $adopt.Values -notcontains $_ })
        if ($orphans.Count) {
            $dirty = @(1..16 | Where-Object { -not $adopt.ContainsKey($_) -and (Test-WorktreeDirty (Join-Path $LanesDir "lane-$_")) })
            $returned = @()
            foreach ($id in $orphans) {
                $action = Get-OrphanAction -Id $id -ParkedBranches (Get-ParkedBranches $id) -DirtyLanes $dirty
                if ($action.Action -eq 'refuse') { Stop-ShiftStart $action.Why; exit 1 }
                if ($action.Action -eq 'adopt') {
                    $adopt[$action.Lane] = $id
                    $dirty = @($dirty | Where-Object { $_ -ne $action.Lane })
                    Write-Trace $id 'adopt' "held by no lane; lane $($action.Lane)'s worktree has uncommitted work, so that lane resumes it" 'Yellow'
                    continue
                }
                Invoke-Board @('move', '-Id', $id, '-To', 'Backlog', '-Reason', "Returned from Doing at shift start: no lane held it and no lane worktree had uncommitted work. The work is on branch $($action.Branch); start with git cherry-pick --no-commit $($action.Branch) and fix it.") | Out-Null
                if ((Get-TaskState $id) -ne 'Backlog') { Stop-ShiftStart "$id is in Doing and held by no lane, and moving it to Backlog failed"; exit 1 }
                $returned += $id
                Write-Trace $id 'requeue' "held by no lane; back to Backlog, work on $($action.Branch)" 'Yellow'
            }
            if ($returned.Count) {
                git -C $Root add -A Tasks 2>&1 | Out-Null
                git -C $Root commit -q -m "chore(tasks): return $($returned -join ', ') to Backlog - in Doing and held by no lane" 2>&1 | Out-Null
                git -C $Root push -q origin "HEAD:$branch" 2>&1 | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    git -C $Root reset -q --hard "origin/$branch" 2>&1 | Out-Null
                    Stop-ShiftStart "$($returned -join ', ') in Doing and held by no lane; returning them to Backlog could not be pushed to origin/$branch"; exit 1
                }
            }
        }
        if (@($adopt.Keys | Where-Object { $_ -gt $LaneCount }).Count) { $LaneCount = [int]($adopt.Keys | Measure-Object -Maximum).Maximum }
    }

    function Get-ShiftCapacity {
        # The board's capacity on origin/<branch>, read in the auto-board worktree; the
        # coordinator's own checkout is not pulled. -Fallback when it cannot be read, traced once.
        param([int]$Fallback)
        git -C $Root fetch -q origin $branch 2>&1 | Out-Null
        $board = Get-BoardCapacity -Ref "origin/$branch"
        if ($null -ne $board.Capacity) { return $board.Capacity }
        if (-not $script:Auto.CapacityTraced) {
            $script:Auto.CapacityTraced = $true
            Write-Trace '-' 'lanes' "cannot read the board's capacity ($($board.Why)); using $Fallback" 'Yellow'
        }
        return $Fallback
    }

    if ($AutoLanes) {
        # Sized before any lane starts: the machine cap, probed now while nothing builds if
        # machine-lanes.json no longer describes this PC, then the start count from the last
        # Auto shift, capped by the ceilings. Adopted lanes raise it, as with a fixed count.
        $machine = Get-MachineCap -Probe
        $script:Auto.MachineCap = $machine.Cap
        Write-Trace '-' 'lanes' $machine.Text 'Cyan'
        $script:Auto.Saved = Read-JsonFile $AutoLanesFile
        $start = Get-AutoStartCount -Saved $script:Auto.Saved -Capacity (Get-ShiftCapacity -Fallback $MaxLanes) -MachineCap $machine.Cap
        $why = $start.Why
        if ($LaneCount -gt $start.Count) { $why = "lane $LaneCount adopted from the previous shift" }
        $LaneCount = [math]::Max($LaneCount, $start.Count)
        Write-Trace '-' 'lanes' "lanes auto: starting at $LaneCount ($why)" 'Cyan'
        Set-AutoLanesStatus -Lanes $LaneCount -Target $null -Binding 'no burn rate' -Reason "lanes auto: starting at $LaneCount ($why)"
    } elseif ($LaneCount -gt 1) {
        # A fixed count starts no more lanes than the board can run at once (BL-1384, AF-0041),
        # so lanes do not open the shift polling "every ready task overlaps one in Doing";
        # capacity steps add the rest, one every 5 minutes, as the capacity rises.
        $adoptedMax = if ($adopt.Count) { [int]($adopt.Keys | Measure-Object -Maximum).Maximum } else { 0 }
        $start = Get-FixedStartCount -Requested ([int]$Lanes) -Capacity (Get-ShiftCapacity -Fallback ([int]$Lanes)) -Adopted $adoptedMax
        $LaneCount = $start.Count
        Write-Trace '-' 'lanes' "lanes: starting at $LaneCount ($($start.Why))" 'Cyan'
    }

    # The previous shift stopped claiming work near the end of its session; this one starts
    # on a fresh session, and the wait does not count against -Hours.
    # Known from here, so the wait for a fresh session below keeps watching CI (BL-1031).
    $script:CiWatchBranch = $branch
    $weekly = Wait-ForFreshSession
    if ($weekly) { Write-Trace '-' 'shift' $weekly 'Red'; Set-OwnTabLabel 'ALARM, read'; Invoke-Alarm -Reasons @($weekly); exit 2 }
    $shiftEnd = (Get-Date).AddHours($Hours)

    Set-OwnTabLabel ''
    Write-Trace '-' 'shift' "start  $LaneCount lanes$(if ($AutoLanes) { ' (auto)' })  branch=$branch model=$Model until $($shiftEnd.ToString('HH:mm'))  $LaneMarkerTrace" 'Cyan'
    New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $LogDir "lanes-$Stamp") | Out-Null
    # A lane gets the time left in the shift, not a fresh -Hours: -Lanes Auto adds and
    # restarts lanes mid-shift, and each must stop claiming when the shift's time is up.
    $laneArgsFor = {
        param([int]$N)
        $hoursLeft = [Math]::Max(0.01, ($shiftEnd - (Get-Date)).TotalHours).ToString([System.Globalization.CultureInfo]::InvariantCulture)
        @('-Lane', $N, '-Branch', $branch, '-Hours', $hoursLeft, '-MaxTasks', $MaxTasks,
          '-TaskMinutes', $TaskMinutes, '-TaskBudgetUsd', $TaskBudgetUsd.ToString([System.Globalization.CultureInfo]::InvariantCulture), '-Model', $Model, '-LogRoot', "`"$LogDir`"", '-ShiftStamp', $Stamp)
    }
    $procs = @()
    $laneTabs = @{}
    $summaries = Join-Path $LogDir "lanes-$Stamp"
    # The lanes running now; scaling adds and retires lanes, so it changes size mid-shift.
    $activeLanes = New-Object System.Collections.Generic.List[int]
    # Every lane started this shift, retired ones included, for the end-of-shift report.
    $startedLanes = New-Object System.Collections.Generic.List[int]
    # Tabs already closed when their lane retired, so the shift end leaves them alone.
    $closedTabs = @{}
    # The non-SUMMARY lines of summaries a retired lane wrote before it was added again.
    $retiredLines = @()

    function Start-Lane {
        # Starts lane $N in its worktree $LanesDir\lane-<n> and records its process or tab.
        # An adopted lane, or a dead one restarted with -KeepWorktree, keeps its work in
        # place; any other gets a clean factory/lane-<n> at origin/<branch>. Returns where
        # it runs, e.g. "herdr tab 12" or "pid 4242".
        param([int]$N, [string]$Label = 'starting', [switch]$KeepWorktree)
        $dir = Join-Path $LanesDir "lane-$N"
        if ($adopt.ContainsKey($N)) {
            Set-Content -Path (Get-LaneStatePath $N 'task') -Value $adopt[$N] -Encoding ASCII
            Write-Trace '-' 'lane' "lane $N adopts $($adopt[$N]) from the previous shift, work in place"
            $adopt.Remove($N)
        } elseif (-not $KeepWorktree) {
            if (-not (Test-Path (Join-Path $dir '.git'))) {
                git -C $Root worktree add -q --detach $dir "origin/$branch" 2>&1 | Out-Null
            }
            git -C $dir stash push -q --include-untracked -m "darkfactory lane-$N before $Stamp" 2>&1 | Out-Null
            git -C $dir checkout -q -B "factory/lane-$N" "origin/$branch" 2>&1 | Out-Null
            git -C $dir reset -q --hard "origin/$branch" 2>&1 | Out-Null
        }
        $started = Start-Detached -Label (Get-LaneTabLabel $N $Label) -Dir $dir -ScriptArgs (& $laneArgsFor $N)
        $script:procs += $started
        $laneTabs[$N] = $started.Tab
        $closedTabs.Remove($N)
        if (-not $startedLanes.Contains($N)) { $startedLanes.Add($N) }
        if ($started.Tab) { return "herdr tab $($started.Tab)" }
        return "pid $($started.Process.Id)"
    }

    function Add-Lane {
        # Starts one more lane, the lowest free number up to 16, and returns it; $null when
        # 16 are active. A lane that retired earlier this shift has its summary kept for the
        # alarm and renamed out of the lane-*.txt set before it starts again.
        $n = Get-LaneToAdd -Active @($activeLanes) -Max 16
        if ($null -eq $n) { return $null }
        $summary = Join-Path $summaries "lane-$n.txt"
        if (Test-Path $summary) {
            $script:retiredLines += @(Get-Content $summary | Where-Object { $_.Trim() -and $_ -notmatch '^SUMMARY ' })
            Rename-Item -Path $summary -NewName "lane-$n.retired-$(Get-Date -Format 'HHmmss').log"
        }
        Remove-Item (Get-LaneStatePath $n 'retire') -ErrorAction SilentlyContinue
        Remove-Item (Get-LaneStatePath $n 'pid') -ErrorAction SilentlyContinue
        $where = Start-Lane -N $n
        $activeLanes.Add($n)
        Write-Trace '-' 'lane' "lane $n added ($where)"
        return $n
    }

    function Request-LaneRetire {
        # Asks the highest-numbered active lane not already retiring, an idle one first, to
        # stop after its current task, and returns it; $null when every active lane is
        # already retiring.
        $retiring = @($activeLanes | Where-Object { Test-Path (Get-LaneStatePath $_ 'retire') })
        $n = Get-LaneToRetire -Active @($activeLanes) -Retiring $retiring -Idle @(Get-IdleLanes)
        if ($null -eq $n) { return $null }
        Set-Content -Path (Get-LaneStatePath $n 'retire') -Value (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') -Encoding ASCII
        Write-Trace '-' 'lane' "lane $n asked to retire after its current task"
        return $n
    }

    function Read-LaneSummary {
        # Lane $N's summary: its SUMMARY line, and the stall lines after it.
        param([int]$N)
        $laneStalls = @()
        $laneSummary = ''
        foreach ($line in Get-Content (Join-Path $summaries "lane-$N.txt")) {
            if ($line -match '^SUMMARY ') { $laneSummary = $line }
            elseif ($line.Trim()) { $laneStalls += $line }
        }
        return [pscustomobject]@{ Summary = $laneSummary; Stalls = $laneStalls }
    }

    function Test-LaneSummaryClean {
        # A lane that ended cleanly has nothing left to read in its tab. One that blocked a
        # task or stalled keeps its tab for Stewart.
        param($Report)
        return [bool]($Report.Summary -and -not $Report.Stalls.Count -and $Report.Summary -notmatch 'blocked=[1-9]')
    }

    function Invoke-AutoLaneStep {
        # One -Lanes Auto step (ADR-0130 items 2 to 5): sample the usage, meter the rates,
        # and add one lane or retire straight down to the step's count, highest lane numbers
        # first (BL-823). Holds without asking while the tokens are low or the shift's time is up.
        $retiring = @($activeLanes | Where-Object { Test-Path (Get-LaneStatePath $_ 'retire') })
        $current = @($activeLanes | Where-Object { $retiring -notcontains $_ }).Count
        $low = Get-UsageStop
        $hold = if ($low) { "tokens low: $low" } elseif ((Get-Date) -gt $shiftEnd) { 'shift time up' } else { '' }
        if ($hold) {
            Write-Trace '-' 'lanes' "lanes $current held ($hold)" 'DarkGray'
            Set-AutoLanesStatus -Lanes $current -Target $script:AutoLanesStatus.target -Binding $script:AutoLanesStatus.binding
            return
        }
        $now = Get-Date
        $reading = Get-UsageReading -ThisShift
        if ($reading) {
            $busy = @($activeLanes | Where-Object { (Test-LaneAlive $_) -and (Get-LaneState $_ 'task') }).Count
            $script:Auto.Samples = @(@($script:Auto.Samples) + @(New-UsageSample -At $now -Reading $reading -ActiveLanes $busy) |
                Where-Object { $_.At -ge $now.AddHours(-3) })
        }
        $capacity = Get-ShiftCapacity -Fallback $current
        $step = Get-AutoLaneStep -Samples $script:Auto.Samples -Current $current -Capacity $capacity -MachineCap $script:Auto.MachineCap `
            -Saved $script:Auto.Saved -PreviousLow $script:Auto.LastLow
        $script:Auto.FiveHourRate = $step.FiveHourRate
        $script:Auto.WeeklyRate = $step.WeeklyRate
        # A low step that retired a lane starts the count again; one that held arms the next.
        $script:Auto.LastLow = [bool]($step.Next.Low -and -not $step.Next.Changed)
        # status.json's autoLanes follows every step; the reason and time only a change.
        $target = if ($step.Pace) { $step.Next.Desired } else { $null }
        Set-AutoLanesStatus -Lanes $current -Target $target -Binding $step.Next.Binding
        if (-not $step.Next.Changed) { Write-Trace '-' 'lanes' $step.Next.Reason 'DarkGray'; return }
        Write-Trace '-' 'lanes' $step.Next.Reason 'Cyan'
        if ($step.Next.Lanes -gt $current) {
            if ($null -eq (Add-Lane)) { Write-Trace '-' 'lanes' 'no lane to add' 'Yellow'; return }
            $script:LaneCount = $step.Next.Lanes
        } else {
            # Request-LaneRetire traces each lane it asks; one that finds none left stops the run.
            $retired = 0
            foreach ($i in 1..($current - $step.Next.Lanes)) {
                if ($null -eq (Request-LaneRetire)) { break }
                $retired++
            }
            if (-not $retired) { Write-Trace '-' 'lanes' 'no lane to retire' 'Yellow'; return }
            $script:LaneCount = $current - $retired
        }
        try { $Host.UI.RawUI.WindowTitle = "Dark factory - $($script:LaneCount) lanes (auto)" } catch { }
        Set-AutoLanesStatus -Lanes $script:LaneCount -Target $target -Binding $step.Next.Binding -Reason $step.Next.Reason
        Save-AutoLanes $script:LaneCount
    }

    function Get-IdleLanes {
        # The active lanes holding no task: claiming, waiting on overlapping touches, or starting.
        return @($activeLanes | Where-Object { -not (Get-LaneState $_ 'task') })
    }

    function Invoke-CapacityLaneStep {
        # One capacity step of a fixed -Lanes N shift (BL-1374, AF-0032): idle lanes beyond
        # the board's capacity retire, and lanes come back one a step, up to N, as it rises.
        $retiring = @($activeLanes | Where-Object { Test-Path (Get-LaneStatePath $_ 'retire') })
        $current = @($activeLanes | Where-Object { $retiring -notcontains $_ }).Count
        if ((Get-UsageStop) -or (Get-Date) -gt $shiftEnd) { return }
        $capacity = Get-ShiftCapacity -Fallback $current
        $idle = @(Get-IdleLanes | Where-Object { $retiring -notcontains $_ })
        $step = Get-CapacityLaneCount -Current $current -Capacity $capacity -Requested $requestedLanes -Idle $idle.Count
        if (-not $step.Changed) { return }
        Write-Trace '-' 'lanes' $step.Reason 'Cyan'
        if ($step.Lanes -gt $current) {
            if ($null -eq (Add-Lane)) { Write-Trace '-' 'lanes' 'no lane to add' 'Yellow' }
            return
        }
        foreach ($i in 1..($current - $step.Lanes)) { if ($null -eq (Request-LaneRetire)) { break } }
    }

    # The lane count a fixed shift asked for; capacity steps never go above it.
    $requestedLanes = if ($AutoLanes) { $LaneCount } else { [math]::Max([int]$Lanes, $LaneCount) }
    foreach ($n in 1..$LaneCount) {
        $where = Start-Lane -N $n
        $activeLanes.Add($n)
        Write-Trace '-' 'lane' "lane $n started in $(Join-Path $LanesDir "lane-$n") ($where)"
        Start-Sleep -Seconds 15
    }
    $restarts = @{}
    # A lane is finished once it has written its summary. A herdr tab has no process to
    # watch, so the summaries are the signal; a lane that dies without one is given up on
    # after the shift's length plus one task's time limit and its overtime run (AF-0042).
    $giveUp = $shiftEnd.AddMinutes($TaskMinutes + [math]::Max(30, [int]($TaskMinutes / 4)) + 30)
    $tick = Get-Date
    $nextAutoStep = (Get-Date).AddMinutes(15)
    $nextCapacityStep = (Get-Date).AddMinutes(5)
    while ((Get-Date) -lt $giveUp) {
        # The coordinator announces the usage limit for every lane, and lanes waiting for a
        # new session add that wait to their shift, so the coordinator waits longer too.
        Update-LimitNotice
        Invoke-LimitProbe
        if (Test-WaitingForSession) { $giveUp = $giveUp.Add((Get-Date) - $tick) }
        $tick = Get-Date
        $finished = @(Get-ChildItem $summaries -Filter 'lane-*.txt' -ErrorAction SilentlyContinue |
            ForEach-Object { [int]($_.BaseName -replace '^lane-', '') })
        # A lane asked to retire that has written its summary is done: it leaves the active
        # set, and its tab closes now if there is nothing in it to read.
        foreach ($n in @($activeLanes)) {
            if ($finished -notcontains $n -or -not (Test-Path (Get-LaneStatePath $n 'retire'))) { continue }
            $activeLanes.Remove($n) | Out-Null
            $report = Read-LaneSummary $n
            Write-Trace '-' 'lane' "lane $n retired ($($report.Summary -replace '^SUMMARY ', ''))" 'Cyan'
            if (Test-LaneSummaryClean $report) {
                Close-HerdrTab $laneTabs[$n] "lane $n retired cleanly" (Get-LaneTabLabel $n)
                $closedTabs[$n] = $true
            }
        }
        if (Test-LanesFinished -Active @($activeLanes) -Finished $finished) { break }
        # A lane whose process is gone without a summary died - killed, crashed, or closed.
        # Start it again in the same worktree; it resumes the task it held. Five tries each.
        foreach ($n in @($activeLanes)) {
            if (Test-Path (Join-Path $summaries "lane-$n.txt")) { continue }
            if (Test-LaneAlive $n) { continue }
            if ($restarts[$n] -ge 5) { continue }
            $restarts[$n] = 1 + [int]$restarts[$n]
            Remove-Item (Get-LaneStatePath $n 'pid') -ErrorAction SilentlyContinue
            $held = Get-LaneState $n 'task'
            Close-HerdrTab $laneTabs[$n] "lane $n died; its restart gets a new tab"
            Start-Lane -N $n -Label "restart $($restarts[$n])$(if ($held) { " $held" })" -KeepWorktree | Out-Null
            Write-Trace '-' 'lane' "lane $n had died; restarted ($($restarts[$n]) of 5)$(if ($held) { ", resuming $held" })" 'Yellow'
        }
        # -Lanes Auto steps every 15 minutes, but never while the shift waits for tokens.
        if ($AutoLanes -and (Get-Date) -ge $nextAutoStep -and -not (Test-WaitingForSession)) {
            Invoke-AutoLaneStep
            $nextAutoStep = (Get-Date).AddMinutes(15)
        }
        # A fixed count of lanes steps to the board's capacity every 5 minutes (BL-1374).
        if (-not $AutoLanes -and $requestedLanes -gt 1 -and (Get-Date) -ge $nextCapacityStep -and -not (Test-WaitingForSession)) {
            Invoke-CapacityLaneStep
            $nextCapacityStep = (Get-Date).AddMinutes(5)
        }
        # The coordinator is the board branch's one writer for a lane shift.
        Publish-BoardStatusIfDue -Branch $branch
        # And the one that sees every CI run: it files a High task for each new failure.
        Invoke-CiWatch -Branch $branch
        $running =@($procs | Where-Object { $_.Tab -or -not $_.Process.HasExited }).Count
        if ($running -eq 0) { break }
        Start-Sleep -Seconds 5
    }

    # Something may have checked out another branch here during the shift; pulling into it
    # would fast-forward the wrong branch.
    $moved = Restore-ShiftBranch -Branch $branch
    if ($moved) { Write-Trace '-' 'branch' "$moved; not pulling" 'Red' }
    else { git -C $Root pull -q --ff-only origin $branch 2>&1 | Out-Null }
    $stalls = @()
    # Every lane started this shift reports, retired ones included. A lane that blocked a
    # task or stalled keeps its tab for Stewart; so does a lane that never wrote a summary.
    foreach ($file in Get-ChildItem $summaries -Filter 'lane-*.txt' -ErrorAction SilentlyContinue) {
        $n = [int]($file.BaseName -replace '^lane-', '')
        $report = Read-LaneSummary $n
        if ($report.Summary) { Write-Trace '-' 'lane' ($report.Summary -replace '^SUMMARY ', '') 'Cyan' }
        $stalls += $report.Stalls
        if ($closedTabs.ContainsKey($n)) { continue }
        if (Test-LaneSummaryClean $report) {
            Close-HerdrTab $laneTabs[$n] "lane $n ended cleanly" (Get-LaneTabLabel $n)
        }
        elseif ($report.Summary -match 'blocked=[1-9]') { Set-HerdrTabLabel $laneTabs[$n] (Get-LaneTabLabel $n 'BLOCKED, read') }
        else { Set-HerdrTabLabel $laneTabs[$n] (Get-LaneTabLabel $n 'STALLED, read') }
    }
    # Runs, tasks and US dollars per model across every lane of the shift (BL-1705).
    Write-Trace '-' 'shift' (Get-ModelCostSummary $LogDir "^BL-\d+-$Stamp-L\d+[-.]") 'Cyan'
    foreach ($n in @($startedLanes)) {
        if (-not (Test-Path (Join-Path $summaries "lane-$n.txt"))) { Set-HerdrTabLabel $laneTabs[$n] (Get-LaneTabLabel $n 'no report, read') }
    }
    Publish-BoardStatus -Branch $branch -State 'ended'
    Write-Trace '-' 'shift' "end  $LaneCount lanes$(if ($AutoLanes) { ' (auto)' })" 'Cyan'
    if ($AutoLanes) { Save-AutoLanes $LaneCount }
    # The audit office's cadence (BL-1022): say when an audit is due, and hold a milestone's
    # merge until it has run. Other reasons do not hold the merge.
    $auditCadence = Get-AuditCadence (Get-AuditDueFromMaster -Branch $branch)
    if ($auditCadence.Line) { Show-AuditNotice $auditCadence.Line }
    $mergeLine = if ($auditCadence.HoldMerge) { $auditCadence.MergeMessage } else { Invoke-MergeToMaster -Branch $branch }
    Write-Trace '-' 'merge' $mergeLine 'Cyan'
    # The merge waited for CI on the shift's last commit; a failure there finished after the
    # loop above stopped watching, so file it now (BL-1031).
    Invoke-CiWatch -Branch $branch -Final
    Remove-CiWatch
    $reasons = @($stalls) + @($retiredLines) + @(Get-WaitingOnStewart)
    # -Continuous: while the board still has ready work, the next shift starts itself, so
    # the factory keeps going without anyone - Stewart or a Claude session - to restart it.
    $stillReady = (Invoke-Board @('next')) -join "`n"
    if ($Continuous -and (Get-NextTaskId $stillReady)) {
        foreach ($r in $reasons) { Write-Trace '-' 'note' (Get-Short $r 100) 'Yellow' }
        # An Auto shift hands on Auto, not the count it ended at; the next one starts from
        # the count auto-lanes.json saved.
        $forward = @('-Lanes', $(if ($AutoLanes) { 'Auto' } else { $LaneCount }), '-Hours', $Hours, '-MaxTasks', $MaxTasks, '-TaskMinutes', $TaskMinutes, '-TaskBudgetUsd', $TaskBudgetUsd.ToString([System.Globalization.CultureInfo]::InvariantCulture), '-Model', $Model, '-HeartbeatMinutes', $HeartbeatMinutes, '-Continuous', '-ShiftBranch', $branch)
        if ($AutoLanes) { $forward += @('-MaxLanes', $MaxLanes, '-MinStartLanes', $MinStartLanes) }
        if ($WeeklyPace) { $forward += '-WeeklyPace' }
        if ($QuietAlarm) { $forward += '-QuietAlarm' }
        $next = Start-Detached -Label "DF shift starting" -Dir $Root -ScriptArgs $forward
        Write-Trace '-' 'shift' "work is still ready; next shift started ($(if ($next.Tab) { "herdr tab $($next.Tab)" } else { "pid $($next.Process.Id)" }))" 'Cyan'
        # The next shift has its own tab; this one has only notes, and they are in the log.
        if ($next.Tab) { Close-OwnHerdrTab 'shift handed over to the next one' }
        exit 0
    }
    if ($reasons.Count -gt 0) { Set-OwnTabLabel 'ALARM, read'; Invoke-Alarm -Reasons $reasons; exit 2 }
    try { $Host.UI.RawUI.WindowTitle = 'Dark factory - shift complete' } catch { }
    Close-OwnHerdrTab 'shift complete with nothing waiting on Stewart'
    exit 0
}

# ------------------------------------------------ one runner: this checkout, or a lane

try { $Host.UI.RawUI.WindowTitle = if ($Lane) { "Dark factory - lane $Lane" } else { 'Dark factory - running' } } catch { }

if ($Lane) {
    if (-not $Branch) { Write-Trace '-' 'refuse' 'a lane needs -Branch' 'Red'; exit 1 }
    $branch = $Branch
} else {
    if ($ShiftBranch) { $moved = Restore-ShiftBranch -Branch $ShiftBranch; if ($moved) { Write-Trace '-' 'branch' $moved 'Red' } }
    $branch = (git -C $Root rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -in 'master', 'main') { Stop-ShiftStart "on $branch; switch to a feature branch"; exit 1 }
    if (Get-Dirty) { Stop-ShiftStart 'working tree not clean; commit or stash first'; exit 1 }
    $stuck = @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue)
    if ($stuck.Count) { Stop-ShiftStart "task already in Doing: $($stuck[0].Name)"; exit 1 }
}

Write-Trace '-' 'shift' "start  branch=$branch model=$Model until $($shiftEnd.ToString('HH:mm'))  $LaneMarkerTrace" 'Cyan'

$done = 0; $blocked = 0; $requeued = 0; $stalls = @(); $failStreak = 0; $attempted = @{}
# Parks whose move to Backlog never reached the shared branch, for the lane's summary.
$parkLines = @()
$stopWhy = ''
# The task to run again once the usage limit resets; it is still claimed.
$resumeId = ''
# How many times in a row the current task's run died on the API.
$apiRetries = 0
# The task whose run was killed at -TaskMinutes in Doing and now gets one overtime run of
# $overtimeMinutes with its work in place, instead of a stash and Blocked (AF-0042).
$overtimeId = ''
$overtimeMinutes = [math]::Max(30, [int]($TaskMinutes / 4))

Write-Heartbeat 'starting'
if ($Lane) {
    Set-LaneState 'pid' "$PID"
    # A restarted or adopted lane finishes the task it held, from the work in its worktree.
    $held = Get-LaneState $Lane 'task'
    if ($held -and (Get-TaskState $held) -eq 'Doing') {
        $resumeId = $held
        Write-Trace $held 'resume' 'this lane held the task when it stopped; carrying on from its work' 'Green'
    } elseif ($held) {
        # It stopped after the run but before its work reached the shared branch: integrate
        # those commits now, before a claim resets the worktree and loses them.
        $ahead = [int](git -C $Root rev-list --count "origin/$branch..HEAD" 2>$null)
        $heldState = Get-TaskState $held
        if ($ahead -gt 0 -and $heldState -in 'Done', 'Blocked', 'Backlog') {
            $problem = Invoke-Integrate -Id $held -State $heldState
            if ($problem) {
                $unpushed = Invoke-Park -Id $held -Why $problem
                if ($unpushed) { $parkLines += $unpushed; Write-Trace $held 'PARKED' "$problem; the move to Backlog was not pushed either" 'Red' }
                else { Write-Trace $held 'PARKED' "$problem; back to Backlog" 'Yellow' }
            }
            else { Write-Trace $held 'push' "integrated the stopped lane's work into $branch" }
        }
        Set-LaneState 'task' ''
    }
}

while ($true) {
  try {
    # A task cut off by the usage limit is finished first, whatever else says stop.
    $resuming = [bool]$resumeId
    if (-not $resuming) {
        if ((Get-Date) -gt $shiftEnd) { $stopWhy = 'time up'; break }
        if ($MaxTasks -gt 0 -and ($done + $blocked + $stalls.Count) -ge $MaxTasks) { $stopWhy = 'max tasks'; break }
        if ($failStreak -ge 2) { $stopWhy = 'runs failing'; break }
        # The coordinator asked this lane to retire; its last task is already integrated.
        if ($Lane -and (Test-Path (Get-LaneStatePath $Lane 'retire'))) { $stopWhy = 'retired'; break }
        $low = Get-UsageStop
        if ($low) { $stopWhy = "tokens low: $low"; break }
    }

    if ($resuming) {
        $id = $resumeId
        $resumeId = ''
    } elseif ($Lane) {
        Set-HeartbeatTask ''
        Set-OwnTabLabel 'empty'
        Write-Heartbeat 'claim'
        $claim = Invoke-Claim -Skip (@($attempted.Keys) + @(Get-ShiftRequeued) | Select-Object -Unique)
        if ($claim.None) { $stopWhy = 'nothing ready'; break }
        if ($claim.Wait) { Write-Trace '-' 'wait' $claim.Why 'DarkGray'; Write-Heartbeat 'wait' (Get-Short $claim.Why 80); Start-Sleep -Seconds 60; continue }
        $id = $claim.Id
    } else {
        Set-HeartbeatTask ''
        Write-Heartbeat 'claim'
        $requeued = @(Invoke-Requeue)
        if ($requeued.Count) {
            git -C $Root add -A Tasks 2>&1 | Out-Null
            git -C $Root commit -q -m "chore(tasks): requeue $($requeued -join ', ') - blockers Done" 2>&1 | Out-Null
            git -C $Root push -q 2>&1 | Out-Null
        }
        $next = (Invoke-Board @('next')) -join "`n"
        $id = Get-NextTaskId $next
        if (-not $id) { $stopWhy = 'nothing ready'; break }
        if ($attempted.ContainsKey($id)) { $stopWhy = "$id offered twice"; $stalls += "$id offered again after a run"; break }
    }
    $attempted[$id] = $true
    # Before the lane's task file is rewritten, whose time a resumed task's start comes from.
    Set-HeartbeatTask $id
    if ($Lane) { Set-LaneState 'task' $id }

    Set-ModelChoice $id
    if (-not $resuming) { Write-Trace $id 'claim' "$(Get-Short (Get-TaskTitle $id))$(Format-ModelChoice)" 'Cyan' }
    if ($Lane) { Set-OwnTabLabel "$id $(Get-TaskTitle $id)" }
    Write-Heartbeat 'run'
    $inOvertime = $overtimeId -eq $id
    $overtimeId = ''
    # A fresh claim of a task that came back with work in progress starts from that work (AF-0091).
    $stashNote = if ($resuming) { '' } else { Restore-TaskStash $id }
    $run = if ($inOvertime) { Invoke-TaskRun $id -Minutes $overtimeMinutes -Overtime } else { Invoke-TaskRun $id -Resume:$resuming -Note $stashNote }
    $state = Get-TaskState $id

    # A run killed at its time limit keeps its claim and its work for one overtime run, so
    # work with passing tests is finished rather than stashed and Blocked (AF-0042).
    if ($run.TimedOut -and $state -eq 'Doing' -and -not $inOvertime) {
        Write-Trace $id 'overtime' "timed out after $TaskMinutes min; one more run of $overtimeMinutes min with its work in place" 'Yellow'
        $overtimeId = $id
        $resumeId = $id
        continue
    }

    # Out of tokens is not a stall. The task keeps its claim and its partial work - nothing
    # is stashed or blocked - the shift waits for the new session and runs it again, and
    # the wait is added to the shift so it costs no working time.
    $until = if ($run.TimedOut) { $null } else { Get-OutOfTokensUntil }
    if ($until -and $state -in 'Doing', 'Backlog') {
        $waited = Wait-ForNewSession -Id $id -Until $until
        $shiftEnd = $shiftEnd.Add($waited)
        Write-Trace '-' 'shift' "waited $(Format-Span $waited) for tokens; shift now ends $($shiftEnd.ToString('HH:mm'))" 'Cyan'
        $resumeId = $id
        continue
    }
    # A run that died on the API without naming the usage limit gets the same treatment,
    # three times in a row at most: wait until Claude answers, then run it again.
    if ($state -eq 'Doing' -and (Test-ApiFailure $run) -and $apiRetries -lt 3) {
        $apiRetries++
        $waited = Wait-ForTokensByProbe -Id $id
        $shiftEnd = $shiftEnd.Add($waited)
        $resumeId = $id
        continue
    }
    $apiRetries = 0

    if ($state -eq 'Doing') {
        $why = if ($run.TimedOut -and $inOvertime) { "timed out after $TaskMinutes min and again after $overtimeMinutes min of overtime" }
            elseif ($run.TimedOut) { "timed out after $TaskMinutes min" }
            elseif (Test-BudgetSpent) { "stopped at its $($script:RunBudgetUsd) US dollar cost cap (2.7 times the median run, at most -TaskBudgetUsd), so split the task" } else { "run ended in Doing, exit $($run.ExitCode)" }
        Save-StrayChanges $id
        Invoke-Board @('move', '-Id', $id, '-To', 'Blocked', '-Reason', "Stewart: dark factory $why; see $(Join-Path $LogDir "$id-$Stamp$LaneTag.jsonl")") | Out-Null
        git -C $Root add -A Tasks 2>&1 | Out-Null
        git -C $Root commit -q -m "chore(tasks): block $id - dark factory $why" 2>&1 | Out-Null
        if (-not $Lane) { git -C $Root push -q 2>&1 | Out-Null }
        $state = Get-TaskState $id
    }
    Save-StrayChanges $id

    if ($Lane -and $state -in 'Done', 'Blocked', 'Backlog') {
        $problem = Invoke-Integrate -Id $id -State $state
        if ($problem) {
            $unpushed = Invoke-Park -Id $id -Why $problem
            if ($unpushed) { $parkLines += $unpushed; Write-Trace $id 'PARKED' "$problem; the move to Backlog was not pushed either" 'Red' }
            else { Write-Trace $id 'PARKED' "$problem; back to Backlog" 'Yellow' }
            $state = 'Parked'
        } else {
            Write-Trace $id 'push' "integrated into $branch"
        }
    }

    if ($Lane) { Set-LaneState 'task' '' }

    if ($state -eq 'Done') {
        $done++; $failStreak = 0
        $outcome = 'done'
        Write-Trace $id 'DONE' (Get-Short (Get-LastLogLine $id)) 'Green'
    } elseif ($state -eq 'Blocked') {
        $blocked++
        if ($null -eq $script:RunResult -or $run.TimedOut) { $failStreak++ } else { $failStreak = 0 }
        $outcome = 'BLOCKED (see Tasks\Blocked)'
        Write-Trace $id 'BLOCKED' (Get-Short (Get-LastLogLine $id)) 'Yellow'
    } elseif ($state -in 'Backlog', 'Parked') {
        # Waiting on other tasks, a widened touches, or work that would not integrate: back
        # in the queue for a later run, not a stall.
        $requeued++; $failStreak = 0
        $outcome = 'requeued'
        if ($Lane) { Add-ShiftRequeued $id }
        Write-Trace $id 'REQUEUE' (Get-Short (Get-LastLogLine $id)) 'Yellow'
    } else {
        $failStreak++
        $stalls += "$id STALLED  ended in $state, exit $($run.ExitCode)"
        $outcome = "STALLED (ended in $state)"
        Write-Trace $id 'STALL' "ended in $state, exit $($run.ExitCode)" 'Red'
    }
    # The run's output is in its .jsonl log and the trace; the tab shows only what is next.
    Show-LaneEmpty "$id $outcome at $(Get-Date -Format 'HH:mm'); empty, waiting for the next task. Trace: $TraceFile"
  } catch {
    $stopWhy = 'script error'
    $stalls += "FACTORY SCRIPT ERROR  $($_.Exception.Message)"
    Write-Trace '-' 'ERROR' (Get-Short $_.Exception.Message) 'Red'
    break
  }
}

Write-Trace '-' 'shift' "end ($stopWhy)  done=$done blocked=$blocked requeued=$requeued stalled=$($stalls.Count)" 'Cyan'
Write-Trace '-' 'shift' (Get-ModelCostSummary $LogDir "^BL-\d+-$Stamp$LaneTag[-.]") 'Cyan'
if ($stopWhy -eq 'runs failing') { $stalls = @('FACTORY STALLED - two runs in a row failed; check ' + $LogDir) + $stalls }
Set-HeartbeatTask ''
Write-Heartbeat 'finished' (Get-Short $stopWhy 80)
if (-not $Lane) { Publish-BoardStatus -Branch $branch -State 'ended' }

if ($Lane) {
    # The coordinator raises one alarm for every lane; a lane only reports.
    # Every line after SUMMARY reaches the coordinator's end-of-shift report: stalls, and
    # parks whose move to Backlog was never pushed (BL-1071).
    Write-LaneSummary (@("SUMMARY lane $Lane ended ($stopWhy): done=$done blocked=$blocked requeued=$requeued stalled=$($stalls.Count)  $(Get-ModelCostSummary $LogDir "^BL-\d+-$Stamp$LaneTag[-.]")") + $stalls + $parkLines)
    # A clean lane leaves an empty tab to close; one that blocked, stalled or could not park says read.
    $endLine = "Lane $Lane ended at $(Get-Date -Format 'HH:mm') ($stopWhy): done=$done blocked=$blocked requeued=$requeued stalled=$($stalls.Count)"
    if (-not $stalls.Count -and -not $parkLines.Count -and -not $blocked) { Show-LaneEmpty $endLine 'empty, close' }
    else { Show-LaneEmpty (@($endLine) + $stalls + $parkLines -join "`n") $(if ($stalls.Count -or $parkLines.Count) { 'STALLED, read' } else { 'BLOCKED, read' }) }
    try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane finished" } catch { }
    exit 0
}

$reasons = @($stalls) + @(Get-WaitingOnStewart)
if ($reasons.Count -gt 0) {
    Invoke-Alarm -Reasons $reasons
    exit 2
}
try { $Host.UI.RawUI.WindowTitle = 'Dark factory - shift complete' } catch { }
exit 0
