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

    This script holds the help, the parameters and a loader; the code is in DarkFactory\
    beside it, one file per area (heartbeat, herdr tabs, alarm, auto lanes, CI watch,
    claim, park, integrate, the coordinator, the runner, ...), each with its own self-tests,
    dot-sourced in a fixed order. Lanes and new tabs are always started as this script.

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
    -TestPlatform proves the PowerShell executable and data folder the factory picks for this OS (BL-3919).

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

    Each headless run is capped with claude's --max-budget-usd at 4 times the median cost
    of the newest 40 task runs in the log folder, never below $3 and never above
    -TaskBudgetUsd (default $10; AF-0004, AF-0033, ADR-0288, ADR-0407, ADR-0478). claude
    checks the cap between turns, so a run can end one turn's cost above it; with fewer
    than 10 logged runs the cap is -TaskBudgetUsd itself. A turn that waits
    on subagents spends all of theirs before the next check, so the prompt allows one
    subagent at a time (AF-0052: five in parallel took BL-1458 $1.77 past its cap; AF-0097:
    five Sonnet test-writers in one message cost BL-1486 $2.97 of its $4.65; AF-0098: three
    test-writers, then on Opus, in one message took BL-1585 to $4.49; AF-0100: three Sonnet
    test-writers, each started in the background in its own message, took BL-1555 to
    $4.33; AF-0102: three test-writers on Opus in one message took BL-1586 to $4.23; AF-0104: four Sonnet test-writers in one message took BL-1487 to $3.88). What the
    task's own runs of the last 24 hours cost comes off its next run's cap, down to $2, so a
    requeued task stays near one cap in all (AF-0095: BL-1488's two claims cost $6.56; AF-0099:
    BL-1683's $2.46 run requeued for a held project, and its next claim spent $1.96 more),
    except a run that sent it back to Backlog waiting on a `depends-on` ID not yet Done with
    nothing to stash, which the shift waives with a .prerequisite-requeue.txt marker beside
    its log (ADR-0812: AF-0387, AF-0404 to AF-0406). Once
    those runs leave less than $2 of the cap (AF-0326: a $1.02 claim of BL-2970 only requeued), a fresh claim is not run but
    goes to Blocked (AF-0096: BL-1609's five claims cost $5.17). A task run that
    reaches the cap stops; like a timed-out run, its partial work is stashed, and one bounded
    split run (BL-3091, ADR-0759) files smaller tasks and defers the original. The task goes
    to Blocked only when that split is skipped (offered once already) or fails.
    A task Blocked by that refusal ("so it was not run again") comes back by itself
    (BL-2117): before every claim, the shift's first included, the requeue that returns
    tasks whose blockers are Done also moves it to Backlog once its runs of the last 24
    hours leave $2 or more of the cap. One whose own run stopped at the cap stays Blocked.
    -TaskBudgetUsd 0 removes the cap. -TestTaskBudget proves the arithmetic.

    LANE CONTEXT AND TURN CAP

    Every call of a headless run re-reads its fixed context, so runs load only what a lane
    uses (BL-3001): project and local settings but not ~\.claude\settings.json (no user
    hooks, plugins or their skills and agents; the project's hooks, the audit-path guard
    among them, still run), no MCP server or claude.ai connector, no auto memory, and
    DarkFactory\LaneRules.md in place of the root CLAUDE.md; a project folder's own
    CLAUDE.md still loads when the run works there. Each run is also capped at
    -MaxRunTurns turns (default 80, 0 for none); a run that reaches it stops, and its task,
    too big for one run, gets the cost cap's bounded split run at once rather than a trip
    back to Backlog for its next claim to split (ADR-1155). The task and lane prompts name
    the cap and ask the run to hand back what is left 15 turns before it (AF-0482).
    -TestLaneFlags proves the flags.

    NO THIRD CLAIM

    A task is claimed at most twice and requeued at most once (ADR-1155, AF-0465 to
    AF-0474). A run that splits a task for size files every part, the rest of the task
    included, as tasks of their own and moves the task to Deferred, never back to Backlog
    to wait on its own subtasks; a run out of time hands its task back with its work for the
    shift to stash, without filing the rest as a task it waits on. And when a run sends a
    task back to Backlog whose Log already shows a Doing -> Backlog move (a requeue or a
    park; not a claim released unstarted at the shift's end), the shift splits it with the
    same bounded split run, carrying any stash to the new tasks; a split skipped or failed
    blocks it for Stewart to triage. -TestTaskStash proves it.

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
         protocol, or feature unless its title is routine and names no hard work (BL-3001:
         $RoutineFeatureTitles and $HardWorkMarkers in DarkFactory\ModelChoice.ps1, such as
         an upstream unit program port or a harness stand-in with no TLS, crypto, QUIC,
         HTTP/2 or /3, SSH, Kerberos, NTLM, DoH or concurrency in it, which runs on sonnet),
         its Log shows an earlier claim that came back ("Doing ->
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

    Stopping a lane stops its process tree and the MSBuild worker nodes found under it, and a
    lane's runs build with node reuse off (MSBUILDDISABLENODEREUSE=1), so none is left behind;
    a node is never stopped by name, so Visual Studio's and other checkouts' are untouched (BL-4348).

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
    lanes, then each lane as soon as that lane is neither claiming nor integrating nor
    within two minutes of taking its task (AF-0129), and
    starts a new shift with the old one's arguments, which adopts the stopped lanes and
    their tasks (BL-895). A parameter passed beside -Restart replaces the old one's value,
    so `RunDarkFactory.cmd -Restart -MaxLanes 12` restarts with a new lane maximum. Only
    this checkout's shift is touched.

    A shift keeps this checkout on its branch: if something switched it (Visual Studio did,
    once), the coordinator switches it back before its shift-end pull, and -Continuous
    hands the next shift -ShiftBranch so it does the same before it starts (BL-809).

    At the end of every shift the coordinator merges the branch into master through a
    pull request, by Stewart's standing permission - only when the CI workflow passed on
    Windows, Linux and macOS for the exact commit being merged.

    After that merge, one headless run reviews what the shift merged with three of the PR
    Review Toolkit plugin's reviewers: errors, types and code. The audit office's quality and
    truthfulness auditors already cover tests and comments, and the code-simplifier edits
    code, so those are left out. It files a task for each Critical or Important issue, High
    for Critical. Under -Continuous with work ready, the next shift starts first and this
    process runs the review beside it, so nothing waits for it; lanes claim what it files.
    With nothing ready, it runs first, and the next shift starts if it filed work. The
    review never holds or undoes the merge: CI is the merge gate (ADR-0710). It is skipped
    when the plugin is not installed.

    A -Continuous shift that ends with nothing claimable (only cost-capped or interactive-only
    tasks, or none) does not exit: it shows what waits on Stewart once, then every
    -IdlePollMinutes (default 5, 1 to 60) pulls the shift branch and looks for a claimable
    task, writing a heartbeat with phase idle and step polling that the board page shows.
    When one is claimable it starts the next shift by the same hand-over as above; at the
    -Hours deadline, or once -StopAtUsage or -StopAtWeeklyUsage says no shift may start, it
    stops polling and ends as a shift without -Continuous does: the alarm when something
    waits on Stewart, else it closes (ADR-0758).

    A commit that only changes the board and is pushed on its own - a claim, requeue,
    park, return or CI-watch filing - says [skip ci], so it starts no CI run and cancels
    none: with lanes claiming every minute or two, every run was once cancelled for two
    hours after a fix was in (AF-0089). When the commit to merge is one of those, the
    coordinator starts a CI run on it by hand before it waits for the result.

    CI WATCH

    Lanes test only on Windows, so a lane shift's coordinator watches CI for them (BL-987).
    Every -HeartbeatMinutes (3 when publishing is off) it reads the newest finished CI runs
    on the shift's branch that passed or failed - a cancelled run is read only when a job
    ran into its timeout-minutes, whose hang is its failure, filed as a High "Fix CI hang
    in <job> (<assembly>)" task (ADR-1173); with several lanes pushing most cancelled
    runs prove nothing - and reads each failed run's failures once
    from `gh run view <id> --log-failed`: every "Failed <TestName>" line, or every compiler
    "error" line when the build broke. Over the last six such runs, newest first, a failure
    is a regression when it fails in the newest run and broke the build, failed on two
    platforms, failed in the run before too, or found no run to confirm it within 5
    minutes; it is flaky when it failed once with a passing run on each side, or failed
    again after passing. For each one it files a High task with task-board.ps1 new -
    "Fix CI failure <test> on Linux and macOS", "Fix flaky CI test <test> that failed once
    on Linux" or "Fix CI build error CS1002 in X.cs on macOS" - naming the platforms, the
    run and its link, the first failing commit and the error message, with `touches` set to
    the test project and its library. A failed job step that names no test (BL-3028) gets
    the files its `::error file=` lines name and the files declaring each member "outside a
    gate", or else Measure-CodeQuality.ps1 for a quality step and Curl.slnx for any other;
    no CI task is filed with empty touches, which no lane would take. When the newest run is
    green and its commit contains the filing of a Backlog "Fix CI failure", "Fix CI build" or
    "Fix flaky CI test" task, the watch moves that task to Deferred, "already fixed, CI green
    on <sha>", pushed the same way (ADR-0756); a task in Doing is left to its lane.
    It also reads the run still running once one of its jobs has failed, from that job's
    log, so a Linux test failure is filed when its job ends rather than when the Quality
    gates job lets the run end; and while the newest finished run is red, it cancels the
    running run when the run waiting behind it carries a CI-fix task into Tasks/Done that
    the running commit lacks, so the fix is tested at once (ADR-0811).
    It files nothing while a Backlog, Doing or Blocked
    task names the test, or when a finished task names it and the failing run predates that
    task's last commit. The task is committed in a detached worktree,
    <repo>.lanes\ci-watch, and pushed straight to the shift's branch, so lanes claim it on
    their next claim. A CI fix goes first (ADR-1158): a claim takes the oldest ready CI-fix
    task before anything task-board.ps1 next offers, even when its touches overlap a task in
    Doing (that lane rebases over the fix); a lane's requeue of one keeps no other lane of
    the shift from it; when it has waited 2 minutes with every lane busy, the coordinator
    adds a lane for it (traced "BL-### lanes lane <n> added for this CI fix: ..."), up to 16,
    which the next lane step retires once idle; and a lane integrating one waits for the
    integrate lock ahead of the others. Each filing is traced "ci filed: ..." and whispered through
    .claude\hooks\whisper-milestone.ps1. Each run it examines is traced "ci run <id> ...":
    green, or each failure and whether it was filed, covered by a task, or is waiting for a
    run to confirm it - once per run, and again when that changes. The watch also runs while
    a coordinator waits for a fresh session before its shift starts, and once more at the
    end of a shift, after the merge has waited for CI on the last commit; that last look
    files a one-platform failure at once instead of waiting 5 minutes (BL-1031).

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
                 not overlap any task in Doing, or a ready CI fix before it, whatever it
                 overlaps (ADR-1158) - moves it to Doing, commits and pushes
                 that move. The push is the lock: if another lane got there first, the
                 push is refused, traced as "race", and the lane picks again. Only
                 a pushed claim is traced as "claim".
      offline    Before a claim or an integration attempt the lane checks that
                 origin answers, and waits up to an hour for it when it does not, so
                 a GitHub outage never parks finished work or counts as a lost race
                 (AF-0025, BL-1281).
      run        /task-run in the lane's worktree. The run commits but never pushes.
      integrate  The lane rebases its commits onto the shared branch - a run that sent its
                 task back to Backlog or Blocked commits the code it finished and tested,
                 and those commits are checked as below unless they change only task
                 files (AF-0496) - builds the whole
                 solution, runs the fast tests of the test projects its commits can
                 affect - those of every project they change and of every project that
                 depends on one, through a ProjectReference or a linked file; all of them
                 when a shared build file or a .csproj changes, none when the commits
                 change no project - and pushes (BL-2967, -TestAffectedTests). CI still
                 runs every test on Windows, Linux and macOS. Red fast tests are run once more, with the
                 failing test names traced as "flaky?"; only red twice counts (BL-898), and not even
                 then when the projects that failed, or never reported passing, pass when run alone (AF-0092, AF-0093, AF-0101, AF-0103). The lane rebases once
                 before taking the lock, and a conflict gets a headless run to resolve it
                 with the lock released, so other lanes go on integrating; a rebase under
                 the lock that still conflicts releases it for another resolve, up to 3
                 resolves in all, and only a rebase made under the lock is pushed
                 (BL-2967). -TestIntegrateLock proves it. A finished task whose build or tests go red on the rebased
                 tree gets one headless repair run there, still holding the lock,
                 traced as "repair" (AF-0051). Work that still will not integrate is pushed to its own branch,
                 factory/<ID>-lane-<n>-<stamp>, and the task goes back to Backlog on the
                 shared branch, retried for several minutes; a park whose move is never
                 pushed is in the lane's summary and the end-of-shift report (BL-1071).

    Claims and integrations hold ..\<repo>.lanes\integrate.lock, so they happen one at
    a time; runs, and the resolves of rebase conflicts, overlap freely. This window coordinates: it starts the lanes, waits
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
      change    Below the unpaced floor, min(-UnpacedLanes, ceilings), straight up to the
                floor in one step; above it, up at most one lane per step, so the meter
                samples each count. Below the floor the board's capacity is also read
                every 2 minutes, and a step runs early when it would add lanes. Down
                straight to max(1, floor(desired + 0.25)) in one step, the highest lane
                numbers first; a retiring lane finishes and integrates its task first. A
                pace target must be low two steps running before lanes retire ("low once"
                holds); a ceiling retires at once (BL-823). Each step traces
                "lanes a -> b (reason)", e.g. "lanes 3 -> 4 (5-hour pace allows 4.9)". It
                holds while the tokens are low or the shift's time is up.
      unpaced   Up to -UnpacedLanes lanes (default 3) are added without reading the burn
                rate, all in one step: the pace decides only the lanes above them and
                never retires below them ("lanes 1 -> 3 (up to 3 lanes run unpaced)").
                The ceilings still cap them, and -StopAtUsage still ends claims.
      split     When the board's capacity binds two steps running and a ready task waits
                on overlapping touches, task-board.ps1 hotspots names the shared files;
                the coordinator files and pushes one High task, "Split <path> so tasks
                touching it can run in parallel", for the top one no live task's title
                names. At most one a step; "hotspots unavailable" when the board has no
                such command.

    auto-lanes.json is saved on each change and at shift end, and -Continuous hands on
    -Lanes Auto. -AutoLanesReport prints one step's reading without starting a lane, and exits.

    LIVE BOARD

    Every runner keeps a heartbeat file, <repo>.logs\lanes-<stamp>\lane-<n>.heartbeat.json
    (the single runner is lane 0): one lane object of ADR-0129's status.json schema 1 -
    the task it holds, its title, the phase (starting, claim, run, integrate, wait, tokens, idle,
    finished), the last tool step and when the task started. It is rewritten on every phase
    change, on each new tool step and at least every 60 seconds during a run or a wait,
    through a temporary file and a rename, so a reader never sees half of one. Lanes never
    push it. Every -HeartbeatMinutes (default 3; 0 turns publishing off) the coordinator
    merges the shift's heartbeat files into one status.json, sorted by lane, and
    force-pushes it to the board branch as a single parentless commit built with plumbing
    (hash-object, mktree, commit-tree), so its checkout's working tree and index never
    change. It publishes once more at shift end with "state": "ended" and every lane
    finished. A single-runner shift publishes its own lane 0 the same way, from where it
    writes its heartbeat. This script force-pushes two branches: the board branch (Stewart,
    2026-09-28) and, at the shift-end merge, the pin branch factory/merge-<stamp>; a failed heartbeat push is traced once and never stops the shift. The
    live board page reads it. -TestHeartbeat rehearses the files, the merge and the
    commit, without pushing.

    COORDINATOR WATCHDOG

    A coordinator that hangs would stop the CI watch, the heartbeats and every lane restart
    while the lanes carry on (BL-4353). Two guards. The coordinator's gh calls and its git
    fetch, push, pull, ls-remote and clone calls run with a 180 second timeout; a call that
    gives no answer is stopped, traced ("timeout") and exits 124, and the loop goes on. And
    each pass of its loop the coordinator stamps <repo>.logs\lanes-<stamp>\coordinator.stamp
    (its pid, the time, the shift's end and its arguments). Every lane checks the stamp once
    a minute with its heartbeat; when the loop has not completed for 3 heartbeat intervals
    (-HeartbeatMinutes, 3 when publishing is off) and the shift has time left, the first lane
    to create coordinator.replacing stops the coordinator and what it started (a stuck git or
    gh) - never a lane - and starts a new coordinator with the same arguments, -Hours set to
    the time left, the shift's -ShiftStamp and -TakeOverShift. That coordinator starts no
    lane: it takes the shift's running lanes as its own, traces "watchdog", and goes on.
    A replacement not stamping within 10 minutes is replaced again. -TestCoordinatorWatchdog
    proves the decision, the stamp, the processes stopped and the timeout.

    MACHINE PROBE (-ProbeMachine)

    Measures how many lanes this PC sustains, since parallel builds are a shift's CPU
    and memory peak. After one untimed warm-up build it runs k = 1, 2, ... concurrent
    `dotnet build --no-incremental` of this checkout, each into its own artifacts folder
    under <repo>.lanes\probe, and records each step's wall time and lowest free memory.
    A step passes when every build succeeds and free memory stays at least 20% of RAM;
    wall time is recorded but decides nothing (BL-812). The cap is the largest passing k (at
    least 1), found at the first failing step, 16, or -ProbeMaxLanes. The result goes to
    <repo>.lanes\machine-lanes.json (marked incomplete when -ProbeMaxLanes cut it short)
    and the probe folder is deleted. Its builds run with MSBuild node reuse off, and any worker
    node still found under a build when its step ends is stopped, so no step leaves nodes that
    hold memory for the next (BL-4348). Run it only when no shift is building.

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
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAffectedTests
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestTaskIds
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAdrIds
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestTaskBudget
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestModelChoice
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestClaimDuplicates
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestTaskReadiness
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestIntegrateLock
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestLaneFlags
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestPlatform
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
    # --max-budget-usd); the cap is 4 times the median recent run, at least $3, up to
    # this, and a run that reaches it gets one bounded split run that files smaller tasks and
    # defers the original (Blocked only if the split is skipped or fails). 0 means no cap (ADR-0288, ADR-0407, ADR-0478, ADR-0759).
    [double]$TaskBudgetUsd = 10,
    # The most turns a single headless run may take (claude's --max-turns); a run that
    # reaches it stops, since its task is too big for one run, and its task gets the bounded
    # split run (ADR-1155). 0 means no cap (BL-3001).
    [ValidateRange(0, 1000)][int]$MaxRunTurns = 80,
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
    # Prove the coordinator watchdog's stale-stamp decision, the stamp file, the processes a
    # replacement stops and the timeout on gh and git calls (BL-4353), and exit.
    [switch]$TestCoordinatorWatchdog,
    # Start as the replacement for a coordinator that stopped working: with -ShiftStamp it
    # keeps that shift's running lanes instead of starting any. Started by a lane's watchdog.
    [switch]$TakeOverShift,
    # Prove how failing test names are read from dotnet test output, and exit.
    [switch]$TestFlakyTests,
    # Prove, on a throwaway repository, which test projects a lane's commits select for the
    # fast tests under the integrate lock (BL-2967), and exit.
    [switch]$TestAffectedTests,
    # Prove how the CI watch reads failures from a failed run's log and which it files, and exit.
    [switch]$TestCiWatch,
    # Prove that task IDs of three digits or more (BL-992, BL-1003) are read from next output,
    # -Reason text, status lines and file names, and that a wait logs next's reason line
    # rather than a WARNING printed before it, and exit.
    [switch]$TestTaskIds,
    # Prove, on scratch repositories, that an ADR a lane added under a number another lane
    # pushed first is renumbered, with its heading and citations, and that base ADRs never are (BL-3834), and exit.
    [switch]$TestAdrIds,
    # Prove the cost cap follows 4 times the median recent run cost (AF-0033), and exit.
    [switch]$TestTaskBudget,
    # Prove the choice of model per task (BL-1705), and exit.
    [switch]$TestModelChoice,
    # Prove the audit cadence: the shift-end audit check and the start refusal (BL-1022), and exit.
    [switch]$TestAuditCadence,
    # Prove the audio-off file silences the alarm, chimes and spoken notices, and that they
    # sound as before without it, playing nothing (BL-1456), and exit.
    [switch]$TestPlatform,
    [switch]$TestAudioOff,
    # Prove the CURL_DARK_FACTORY_LANE marker's value for a lane, a coordinator and a
    # launcher, and that a claude -p run inherits it, and exit.
    [switch]$TestLaneMarker,
    # Prove, on a throwaway repository, that a park pushes its move to Backlog or reports
    # that it could not, how shift start settles a task in Doing held by no lane (adopted,
    # or back to Backlog when no work for it exists, refused only when a lane branch holds
    # commits beyond the shift branch), that a claim past the shift's end is released to
    # Backlog, and that a refused start raises the alarm (BL-1071, BL-4347), and exit.
    [switch]$TestPark,
    # Prove, on a throwaway repository, that a claim applies its task's own shift stash and
    # no other, and that a stash that no longer applies leaves the worktree clean and the
    # run its hash (AF-0091), and exit.
    [switch]$TestTaskStash,
    # Prove, on a throwaway repository, that a claim on a board where two live tasks share an
    # ID renumbers the later one, pushes that and claims, and that a claim the board refuses
    # for another reason is traced with the board's own words (BL-2014), and exit.
    [switch]$TestClaimDuplicates,
    [switch]$TestTaskReadiness,
    # Prove, on a throwaway repository with a stand-in resolver, that an integration pushes,
    # that a rebase conflict is resolved with the integrate lock released and the rebased
    # work pushed, and that a resolver that fails leaves no rebase in progress (BL-2967), and exit.
    [switch]$TestIntegrateLock,
    # Prove the trace labels an Edit or Write whose input names its file as path or
    # notebook_path or not at all, as it does one naming file_path (BL-2036), and exit.
    [switch]$TestToolLabels,
    # Prove a headless run is launched with the slim lane context (no user settings, MCP
    # servers or root CLAUDE.md; LaneRules.md instead) and the turn cap (BL-3001), and exit.
    [switch]$TestLaneFlags,
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
    # With -Continuous: how often, in minutes, a shift that ended with nothing claimable looks
    # at the board again, starting the next shift once a task is claimable.
    [ValidateRange(1, 60)][int]$IdlePollMinutes = 5,
    # Stop claiming new tasks once this share of the 5-hour usage window is used, so the
    # tasks already running finish, integrate and push before the tokens run out. The next
    # shift waits for a fresh 5-hour window. 1 means never stop early.
    [ValidateRange(0.1, 1)][double]$StopAtUsage = 0.85,
    # The same for the weekly usage window, which is days from resetting, so the shift
    # runs it closer to empty (Stewart, 2026-09-28); a used-up weekly window is waited out until the
    # weekly reset, with a notice rather than the alarm (BL-806).
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
    # -Lanes Auto adds lanes up to this many without reading the burn rate; the pace decides
    # only the lanes above it, and never retires below it (Stewart, 2026-10-21: "Lets not
    # check token use for new lanes until after we are running more than 3 lanes"). The
    # board, the machine and -MaxLanes still cap it, and -StopAtUsage still ends claims.
    [ValidateRange(1, 16)][int]$UnpacedLanes = 3,
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

# The factory's code is in DarkFactory\ beside this script, one file per area, and is
# dot-sourced here in the order it ran when it was one file: top-level statements set the
# shift's state as they are reached, and a self-test or one-shot switch (-TestAlarm,
# -ProbeMachine, -Restart, ...) runs and exits as soon as its file is reached, so it needs
# every file before it. Windows PowerShell's exit in a dot-sourced file ends only that file,
# so each file sets $AreaFileFinished on its last line: a file that did not get there
# exited, and this script exits with its exit code. In the files $PSScriptRoot,
# $PSCommandPath and $PSBoundParameters describe the file, not this script, so they use
# $FactoryScriptDir, $FactoryScript and $FactoryParameters instead.
$FactoryScript = $PSCommandPath
$FactoryScriptDir = $PSScriptRoot
$FactoryParameters = $PSBoundParameters
foreach ($AreaFile in @(
        'Platform',
        'Setup',
        'Heartbeat',
        'HerdrTabs',
        'Alarm',
        'Usage',
        'AutoLanes',
        'MachineProbe',
        'OutOfTokens',
        'TaskIds',
        'AdrIds',
        'TaskBudget',
        'ModelChoice',
        'Requeue',
        'AffectedTests',
        'ShiftBranch',
        'AuditCadence',
        'AudioOff',
        'ShiftEnd',
        'CiWatch',
        'TaskStash',
        'Prompts',
        'ToolLabels',
        'TaskRun',
        'Claim',
        'Park',
        'Integrate',
        'LaneState',
        'AutoLanesState',
        'Restart',
        'ShiftStart',
        'ShiftCoordinator',
        'ShiftRunner'
    )) {
    $AreaFileFinished = $false
    . (Join-Path $FactoryScriptDir "DarkFactory\$AreaFile.ps1")
    if (-not $AreaFileFinished) { exit $LASTEXITCODE }
}
