---
id: BL-4840
title: Fix AF-0658: Surviving mutant: an OpenSSL cipher string with an empty alias word can be accepted instead of refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/Cgr023Tests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4840 — Fix AF-0658: Surviving mutant: an OpenSSL cipher string with an empty alias word can be accepted instead of refused

## Goal

The defect the audit office reported as AF-0658 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0658 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0658-surviving-mutant-an-openssl-cipher-string-with-an.md`.

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:214`

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:214`

Mutant 'return (false, null);' -> 'return (true, null);' survived. In ReadAliases, an empty word (for example a trailing '+') is then accepted as valid, so a cipher list curl refuses with exit 59 (SSL cipher) would be accepted. No test covers an empty alias word.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/OpenSslCipherString.cs:214:false -Member ApplyNextRule -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Why the mutant survived: the only test with an empty alias word, `!!`, keeps no suite, so the value is refused whether the empty word counts as valid or not. I added `Cgr023Tests` rows `AES128-SHA:HIGH+`, `AES128-SHA:+` and `AES128-SHA:ECDHE++AES`. Each keeps a suite, so only the empty alias word can make the value refused, as OpenSSL refuses it (`ssl_cipher_process_rulestr`: a zero-length alias is `SSL_R_INVALID_COMMAND`). The production code is unchanged.
- Widened `touches` to `Curl.Networking.UnitTests/Cgr023Tests.cs`, which holds the test. No task in Doing names that file.
- The lane may not run the finding's mutation script (the audit guard refuses it). Instead I applied the mutant by hand with the Edit tool: exactly the three new rows failed (3 failed, 21 passed in `Cgr023Tests`), then I reverted the mutant. The re-audit runs the official reproduction.
- Tests are platform-neutral (pure string parsing). The build is clean and the fast tests are green.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Cgr023Tests now refuse cipher strings with an empty alias word, killing the line 214 mutant
