Fixture reply.

```json
{ "auditor": "security", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "security:Curl.Tls.UnitLibrary/TlsMac.cs:Verify:timing-leak", "title": "SequenceEqual on the MAC", "severity": "High", "location": "Curl.Tls.UnitLibrary/TlsMac.cs:40", "evidence": "SequenceEqual", "reproduction": { "command": "x", "expected": "e", "actual": "a" } } ], "reaudits": [  ], "metrics": { "method.fuzzTargets": 2, "method.timingSitesRead": 12 } }
```
