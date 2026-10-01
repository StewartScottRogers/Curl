Fixture reply from the performance auditor.

```json
{ "auditor": "performance", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "performance:Curl.Console/CurlCommandRunner.cs:headers-verbose:slow", "title": "headers-verbose is slow", "severity": "High", "location": "Curl.Console/CurlCommandRunner.cs:1", "evidence": "2.5x", "reproduction": { "command": "Write-Output x", "expected": "e", "actual": "a" } } ], "reaudits": [ { "finding": "AF-0005", "reproduces": false, "evidence": "1.1x now" } ], "metrics": {} }
```
