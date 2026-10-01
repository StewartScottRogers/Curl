Fixture reply from the quality auditor.

```json
{ "auditor": "quality", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "quality:Curl.Core.UnitTests/UrlTests.cs:Parse_Port_Rejects:name-lies", "title": "Port test name lies", "severity": "Medium", "location": "Curl.Core.UnitTests/UrlTests.cs:12", "evidence": "still lies", "reproduction": { "command": "Write-Output x", "expected": "e", "actual": "a" } }, { "key": "quality:Curl.Cli.UnitTests/ParserTests.cs:Parse_Empty_Throws:weak-assertion", "title": "Parse_Empty_Throws only checks not-null", "severity": "Low", "location": "Curl.Cli.UnitTests/ParserTests.cs:30", "evidence": "IsNotNull only", "reproduction": { "command": "Write-Output x", "expected": "e", "actual": "a" } } ], "reaudits": [], "metrics": {} }
```
