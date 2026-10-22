# ADR-0753 — Lanes run on a slim context, with Sonnet for routine feature work, a turn cap and a pre-claim check

- Status: Accepted
- Date: 2026-10-21
- Task: BL-3001
- Approved by Stewart (2026-10-21: "do -> My suggestion is all five plus the accounting fix"); the
  details decided by Claude under Stewart's delegation.

## Context

Stewart asked for ways to make the Claude Max plan's 5-hour and weekly limits carry more lane work. A
measurement of all 227 lane runs from 2026-10-19 17:00 to 2026-10-21 (their stream-json logs) found:

- 95% of cost was Opus, and feature runs were 81% of it.
- About 39% was a fixed context of about 55K tokens that every call reads again: CLAUDE.md, the lane
  prompt, 42 tools including three MCP servers, about 100 skills from user-level plugins, and a
  user-level SessionStart hook.
- 27% went to runs that ended without Done; runs of 40 or more calls were 19% of runs and 45% of cost.
- Code exploration (grep, cat, whole-file reads of up to 105K characters) was about 17.5%; build and
  test output only 1.6%, since lanes already filter it.
- Killed runs were logged at a cost of 0, so the per-task cost cap undercounted them (about $23 over
  the two days).

## Decision

1. **Slim lane context** (`DarkFactory\TaskRun.ps1`). Lane runs pass `--setting-sources project,local`
   (no user-level settings, hooks or plugins), `--strict-mcp-config` with an empty MCP config, a
   settings file that turns off the claude.ai connectors and excludes only the root CLAUDE.md,
   `--append-system-prompt-file DarkFactory\LaneRules.md` (the rules a lane needs, about 130 lines),
   `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1` and `--exclude-dynamic-system-prompt-sections`. The project's
   settings, permission rules and hooks, including the audit-path guard, still load, and so do the
   project folders' own CLAUDE.md files. Measured: the first call's context fell from 50,967 to 29,301
   tokens.
2. **Sonnet for routine feature work** (`DarkFactory\ModelChoice.ps1`). A feature task whose title
   matches `$RoutineFeatureTitles` (an upstream unit program port, a harness stand-in, a test-script
   interpretation, a feature report to the harness, a file split, a test-strength audit fix and the
   like) and names none of `$HardWorkMarkers` (security, crypto, TLS, QUIC, HTTP/2, HTTP/3, SSH,
   Kerberos, NTLM, DoH, concurrency, framing) in its title or touches runs on Sonnet. Front-matter
   `model:`, `-Model`, retried tasks, CI fixes, the protocol pipeline and security or crypto libraries
   keep their earlier precedence. On the two days' data this moves about a third of feature cost.
3. **Pre-claim check** (`DarkFactory\Claim.ps1`, `Get-TaskUnreadyWhy`). A lane passes over a task whose
   touches are empty or `*`, or a feature or protocol task whose title names 10 or more upstream cases
   (titles starting List, Record, Report, Check, Mark or Exclude, and a task already handed back once,
   are exempt). It is traced once per shift and listed in the shift's `unready.txt`.
4. **Exploration limits** (`DarkFactory\Prompts.ps1`). The run prompts ask for searches before reads,
   capped output, one read of each file (whole when short, one offset and limit when long), never
   re-reading persisted tool results, and `git diff --stat` first. The earlier cost rules from
   AF-0105 to AF-0111, AF-0172, AF-0190, AF-0204 to AF-0206, AF-0268 and AF-0309 are kept as rules,
   without their long evidence text, which shortens each main prompt by about 2,300 characters.
5. **Turn cap** (`-MaxRunTurns`, default 80). A run that reaches it goes back to Backlog to be split.
   80, not 60: on the two days' data 60 would have stopped 23 runs that finished Done, 80 stops 3.
6. **Killed runs' cost** (`DarkFactory\TaskBudget.ps1`, `Get-RunLogCost`). A run log with no result, or
   a result of 0 while usage is logged, is priced from its per-message usage with per-model list-price
   estimates, and counts toward task spend, recent-run costs and the per-model cost summary.

## Consequences

- Expected saving: roughly 25 to 40% of lane tokens together; each part is checked against the run
  logs after a shift or two.
- CLAUDE.md and `DarkFactory\LaneRules.md` both hold the rules a lane needs, so a rule a lane needs is
  written in both.
- A routine title can hide design work; such a run on Sonnet that comes back is retried on Opus, and
  `model: opus` in front matter always wins.
- A task the pre-claim check passes over waits in Backlog until a planner or an interactive session
  fixes its touches or splits it.
- Self-tests: `-TestLaneFlags`, `-TestTaskReadiness`, and new cases in `-TestModelChoice` and
  `-TestTaskBudget`.
