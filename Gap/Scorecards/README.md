# Gap scorecards

One scorecard per gap run, `<yyyy-MM-dd_HHmm>.md`, named by the run stamp, and
`history.json`, every run's scores (ADR-0433). Every field is defined in
[`../Instructions/Gap-Format.md`](../Instructions/Gap-Format.md), section 8.

## Scorecard sections

In this order, each saying `None.` when it has nothing to list:

1. `Run`: commit, platforms measured, any missing platform and why, target, newest, each
   platform's reference.
2. `Scores`: the headline, the lowest platform's overall score, so no platform hides
   behind another (ADR-0505); then one row per area and an overall row, one column per
   platform - X of Y, %, change since the last run on that platform.
3. `Platforms`: per platform, one row per area and an overall row - X, Y, %, unmeasured,
   excluded, change.
4. `Against the newest version`: the same scores against the newest release.
5. `New gaps`: findings this run opened, with severity and platform.
6. `Closed`: findings this run closed.
7. `Regressions`: findings this run reopened.
8. `Unmeasured by reason`: area, platform, reason and count.

X is the items that match. Y is match + gap + unmeasured, so an unmeasured item counts
against the score.

## history.json

An array, oldest first, one entry appended per run and measured platform:

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
