# ADR-1239: Factory tasks run on sonnet first, and on opus only on a retry

- Status: Accepted
- Date: 2026-11-11
- Decided by: Stewart ("lets try and dial back the AI models we are using and only dial them back up temporarily if you are having problems. We are burning through too many tokens")
- Supersedes: the opus clauses of BL-1705's rule and BL-3001's routine-feature list

## Context

The dark factory's model rule (`DarkFactory/ModelChoice.ps1`) sent every `feature` and
`protocol` task, every task touching a hand-built security or crypto library, every retried
task and every High CI-fix task to opus. Only docs, direct and a short list of routine feature
titles ran on sonnet. In practice nearly every claim ran on opus: all thirteen claims of shift
20261111-082724's first wave did. Token use was too high.

## Decision

1. A task's first run is on sonnet, whatever its pipeline, library or priority.
2. A task moves to opus when its Log shows an earlier claim that came back (`Doing -> Backlog`
   or `Doing -> Blocked`: a requeue, a park, a hand-back or a failed run). This is the
   temporary step up for a task that sonnet had trouble with.
3. A task's front-matter `model:` and a `-Model` other than `auto` still win over the rule, so
   a task known to need opus from the start can say `model: opus`.
4. The rule never picks haiku, as before.

The routine-title list, the hard-work markers and the crypto-library list are removed, because
nothing reads them any more.

## Consequences

- Most first runs cost less. A task sonnet cannot finish costs one extra sonnet run before its
  opus retry.
- Security and crypto work also starts on sonnet. The audit office, the quality gates and CI
  still check the result. A task that should never start on sonnet gets `model: opus`.
- `RunDarkFactory.ps1 -TestModelChoice` proves the rule.
