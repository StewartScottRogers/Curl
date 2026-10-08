# Gap scorecards

One scorecard per gap run, `<yyyy-MM-dd_HHmm>.md`, named by the run stamp, and
`history.json`, every run's scores (ADR-0433). Every field is defined in
[`../Instructions/Gap-Format.md`](../Instructions/Gap-Format.md), section 8.

## Scorecard sections

In this order, each saying `None.` when it has nothing to list:

1. `Run`: commit, platform, target, newest, reference.
2. `Scores`: one row per area and an overall row - X, Y, %, unmeasured, excluded, change
   since the last run.
3. `Against the newest version`: the same scores against the newest release.
4. `New gaps`: findings this run opened.
5. `Closed`: findings this run closed.
6. `Regressions`: findings this run reopened.
7. `Unmeasured by reason`: area, reason and count.

X is the items that match. Y is match + gap + unmeasured, so an unmeasured item counts
against the score.

## history.json

An array, oldest first, one entry appended per run:

```json
[
  {
    "stamp": "2026-10-09_1430",
    "commit": "d15d115db0a1b2c3d4e5f60718293a4b5c6d7e8f",
    "platform": "windows",
    "targetVersion": "8.21.0",
    "newestVersion": "8.22.0",
    "areas": { "options": { "x": 250, "y": 260 } },
    "overall": { "x": 250, "y": 260 },
    "newest": { "x": 249, "y": 263 }
  }
]
```
