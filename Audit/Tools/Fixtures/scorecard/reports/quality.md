Fixture reply.

```json
{ "auditor": "quality", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "quality:Curl.Cli.UnitTests/ParserTests.cs:Parse_Empty_Throws:weak-assertion", "title": "Parse_Empty_Throws only checks not-null", "severity": "Low", "location": "Curl.Cli.UnitTests/ParserTests.cs:30", "evidence": "IsNotNull", "reproduction": { "command": "x", "expected": "e", "actual": "a" } } ], "reaudits": [  ], "metrics": { "method.librariesMutated": 1, "method.testsRead": 50 } }
```
