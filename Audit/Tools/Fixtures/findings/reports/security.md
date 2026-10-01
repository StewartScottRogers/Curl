Fixture reply from the security auditor.

```json
{ "auditor": "security", "commit": "abc1234", "fingerprint": "f", "findings": [ { "key": "security:Curl.Cryptography.UnitLibrary/Rc4.cs:NextKeyStreamByte:secret-dependent-lookup", "title": "RC4 lookup depends on the key", "severity": "High", "location": "Curl.Cryptography.UnitLibrary/Rc4.cs:113", "evidence": "still", "reproduction": { "command": "Write-Output x", "expected": "e", "actual": "a" } }, { "key": "security:Curl.Tls.UnitLibrary/TlsMac.cs:Verify:timing-leak", "title": "FixedTimeEquals replaced by SequenceEqual", "severity": "High", "location": "Curl.Tls.UnitLibrary/TlsMac.cs:40", "evidence": "SequenceEqual on the MAC", "reproduction": { "command": "Write-Output x", "expected": "e", "actual": "a" } } ], "reaudits": [], "metrics": {} }
```
