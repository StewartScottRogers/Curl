Fixture reply.

```json
{ "auditor": "process", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "process:logs:ci:ci-red", "title": "CI red 90 minutes", "severity": "Low", "location": "ci-runs.json", "evidence": "ci-red spell", "reproduction": { "command": "x", "expected": "e", "actual": "a" } } ], "reaudits": [  ], "metrics": { "tasksDone": 20, "medianTaskMinutes": 19.5, "p90TaskMinutes": 57.3, "tasksClaimedMoreThanOnce": 2, "requeues": 3, "resumedRuns": 1, "ciRedMinutes": 90, "laneIdleMinutes": 30, "waitOverlapMinutes": 20, "waitNothingReadyMinutes": 10, "tokensInput": 1000, "tokensOutput": 100, "costUsd": 45.5, "costUsdPerTaskDone": 2.25 } }
```
