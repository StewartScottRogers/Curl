# ADR-0693: CI runs on work/dark-factory finish instead of being cancelled by the next push

- Status: Accepted
- Date: 2026-10-18
- Decided by Claude under Stewart's delegation.

## Context

`ci.yml`'s concurrency group was `ci-${{ github.ref }}` with `cancel-in-progress: true`. Each push
to a ref cancelled that ref's running CI run. A CI run takes 15 to 25 minutes, and the quality job
is the longest.

ADR-0689 let the dark factory run up to 6 lanes on 2026-10-18, and lanes then pushed
`work/dark-factory` every few minutes. Every run was cancelled before it finished. From 18:41 UTC
to past 22:20 UTC, no CI run on `work/dark-factory` completed. Nothing tested Linux or macOS, the
coordinator's CI watch (which ignores cancelled runs) filed nothing, and the shift-end merge, which
needs a green run on its exact commit, had none.

## Decision

`cancel-in-progress` is `false` on `work/dark-factory` and `true` on every other ref. GitHub
Actions then lets a running run finish, keeps at most one run waiting in the group, and replaces
that waiting run with each newer push. So a run always completes every 15 to 25 minutes, and the
next one tests the newest commit.

## Consequences

- CI tests `work/dark-factory` about every 20 minutes however often lanes push. A failure names
  the batch of commits since the last finished run, and the coordinator files it as before.
- Commits between two finished runs are tested only as part of the later one. The CI watch still
  finds which test failed, and `--log-failed` names it.
- At the end of a shift, the shift's last commit waits behind at most one running run before its
  own run starts, so the shift-end merge can take up to one run's time longer.
- Pull requests, `audit`, `gap` and feature branches keep cancelling a superseded run, where only
  the newest commit matters.
