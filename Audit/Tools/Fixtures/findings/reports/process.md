Fixture reply from the process auditor.

```json
{ "auditor": "process", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "process:logs:BL-88:redone-work", "title": "BL-88 claimed five times", "severity": "High", "location": "logs/DarkFactory-20261001-120001-L1.log:1", "evidence": "five claims", "reproduction": { "command": "Write-Output x", "expected": "e", "actual": "a" } }, { "key": "process:Curl.Tls.UnitLibrary/TlsRecord.cs:Read:unchecked-length", "title": "Read slices before its length check", "severity": "Low", "location": "Curl.Tls.UnitLibrary/TlsRecord.cs:7", "evidence": "same project, another file", "reproduction": { "command": "Write-Output x", "expected": "e", "actual": "a" } } ], "reaudits": [ { "finding": "AF-0005", "reproduces": false, "evidence": "claimed once now" } ], "metrics": {} }
```
