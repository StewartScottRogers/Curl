Fixture reply.

```json
{ "auditor": "truthfulness", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "truthfulness:Documentation/Planning/Decisions/ADR-0280-x.md:exit:stale-adr", "title": "ADR-0280 says 82", "severity": "High", "location": "Documentation/Planning/Decisions/ADR-0280-x.md:29", "evidence": "stale", "reproduction": { "command": "x", "expected": "e", "actual": "a" } }, { "key": "truthfulness:Curl.Tls.UnitLibrary/TlsMac.cs:Verify:false-doc-comment", "title": "TlsMac doc says FixedTimeEquals", "severity": "Low", "location": "Curl.Tls.UnitLibrary/TlsMac.cs:40", "evidence": "code uses SequenceEqual", "reproduction": { "command": "x", "expected": "e", "actual": "a" } } ], "reaudits": [  ], "metrics": { "method.names": 60, "method.docComments": 60, "method.documentStatements": 20, "method.adrs": 10, "method.scriptStatements": 30 } }
```
