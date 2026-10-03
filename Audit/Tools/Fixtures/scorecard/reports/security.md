Fixture reply.

```json
{ "auditor": "security", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "security:Curl.Tls.UnitLibrary/Other.cs:Open:timing-leak", "title": "Something else", "severity": "High", "location": "Curl.Tls.UnitLibrary/Other.cs:10", "evidence": "not the planted one", "reproduction": { "command": "x", "expected": "e", "actual": "a" } } ], "reaudits": [ { "finding": "AF-0009", "reproduces": true, "evidence": "x" } ], "metrics": { "method.fuzzTargets": 2, "method.timingSitesRead": 12 } }
```
