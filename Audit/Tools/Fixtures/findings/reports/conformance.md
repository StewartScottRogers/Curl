Fixture reply from the conformance auditor.

```json
{ "auditor": "conformance", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "conformance:--libcurl:refused-option", "title": "--libcurl refused", "severity": "Medium", "location": "Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:1", "evidence": "refused again", "reproduction": { "command": "Write-Output x", "expected": "e", "actual": "a" } } ], "reaudits": [], "metrics": {} }
```
