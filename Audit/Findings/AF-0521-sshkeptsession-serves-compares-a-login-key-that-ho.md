---
id: AF-0521
title: SshKeptSession.Serves compares a login key that holds the password and pass phrase with string ==
auditor: security
severity: High
status: accepted
reason: Stewart accepted it on 2026-11-01 ("accept all").
key: security:Curl.Original.Protocol.Ssh.UnitLibrary/SshKeptSession.cs:SshKeptSession.Serves:timing-leak
reproduction: none
task: BL-4456
tasks: BL-4456
found: 2026-11-01
found-at: 4c39311e3e4524ac6950cb2cba36f5b3f2a6ff2e
scorecard: 2026-11-01_1016.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0521 - SshKeptSession.Serves compares a login key that holds the password and pass phrase with string ==

## Summary

High finding from the security auditor at `Curl.Original.Protocol.Ssh.UnitLibrary/SshKeptSession.cs:41`: SshKeptSession.Serves compares a login key that holds the password and pass phrase with string ==. Reported by an auditor flagged unreliable in 2026-11-01_1016.md.

## Evidence

Location: `Curl.Original.Protocol.Ssh.UnitLibrary/SshKeptSession.cs:41`

Line 36 builds the login key as string.Join('\0', UserName, Password, PrivateKeyPath, PublicKeyPath, PrivateKeyPassphrase). Line 41, `internal bool Serves(string key) => key == loginKey;`, compares two of these keys with ordinal string equality, which stops at the first differing character. The password and pass phrase are secrets, so the comparison is not constant time, as the method requires for any secret. Upstream curl compares the credentials for connection reuse with Curl_timestrcmp. Exploitability is low: both keys come from the user's own command line or config (the session's login and the next URL's login). No remote party controls either value, so I found no attacker-observable oracle. Reported at the rubric's severity for a non-constant-time secret comparison.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ssh.UnitLibrary/SshKeptSession.cs -Pattern 'key == loginKey'
```

- Expected: No match: the keys (or the password and pass phrase within them) are compared with CryptographicOperations.FixedTimeEquals over their bytes.
- Actual: Curl.Original.Protocol.Ssh.UnitLibrary\SshKeptSession.cs:41:    internal bool Serves(string key) => key == loginKey;

## Re-audits

## Log

- 2026-11-01: filed proposed.
- 2026-11-01: proposed -> accepted. Stewart: accept all.
