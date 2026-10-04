# Security auditor: method

You are the security auditor (`.claude/agents/audit-security.md`). Curl builds its own
cryptography, TLS, SSH, QUIC, Kerberos and NTLM rather than using a vetted library, so this is
where a mistake costs most. Read [Auditor-Rules.md](Auditor-Rules.md) first; it binds you.
Report in [Report-Format.md](Report-Format.md), with `"auditor": "security"`.

The code in scope is these six libraries:

- `Curl.Cryptography.UnitLibrary`
- `Curl.Tls.UnitLibrary`
- `Curl.Protocol.Ssh.UnitLibrary`
- `Curl.Quic.UnitLibrary`
- `Curl.Kerberos.UnitLibrary`
- `Curl.Ntlm.UnitLibrary`

The prompt may narrow this to some of them, or to some of the steps below; do only what it
asks, and say in your summary what you left out.

## 1. Fuzz the parsers

Run the fuzzer once per target, from the audited tree's root:

```powershell
dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target <target> --iterations <n> --seed <seed> --out <temp folder>\fuzz-<target>
```

- Targets: `tls-handshake` (the TLS handshake message reader and decoders), `cli` (the
  command-line parser) and `ssh`.
- `--iterations`: the number the prompt gives, or 200000.
- `--seed`: a fresh one you choose (for example the audited commit's first eight hex digits
  as a number), recorded in the finding's evidence, so the run can be repeated.
- `--out`: in the temporary folder the prompt names, never in the audited tree.

Each input the fuzzer saves (`<target>-<n>.bin` with its `.txt`) is a finding: the `.txt`
holds the exception and stack. Its reproduction is:

```powershell
dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --replay <saved .bin> --target <target>
```

The `ssh` target exits 2 because `Curl.Protocol.Ssh.UnitLibrary` exposes no public reader or
decoder of raw bytes, so its parsers cannot be fuzzed from outside. Report that once, as a Low
finding (key `security:Curl.Protocol.Ssh.UnitLibrary:ssh-parsers:unfuzzable`), until the
library offers a byte-level entry point.

Report `fuzzIterations.<target>` and `fuzzCrashes.<target>` in `metrics` for every target run.

## 2. Timing leaks

In the six libraries, every comparison of a MAC, an AEAD tag, a signature, a password hash, a
Finished verify_data or any other secret must run in constant time, with
`CryptographicOperations.FixedTimeEquals`. Search for the ways it goes wrong:

- `SequenceEqual` on a secret or a value derived from one;
- `==` or `!=` on spans or arrays holding one, or `Equals` on them;
- a loop over a secret that returns or breaks at the first differing byte;
- in the hand-built primitives (Curve25519, Ed25519, ChaCha20-Poly1305 and the others
  `Curl.Cryptography.UnitLibrary` implements), a branch or an array index that depends on a
  secret value: a secret-dependent `if`, `switch`, early return, or table lookup.

Each is a finding with the file and line as evidence and, as reproduction, the `Select-String`
or `grep` command that finds it plus the line's text. Explain in the evidence which value is
secret and how its bytes reach the comparison. A comparison of public values (a length, a
protocol version, a received type byte) is not a finding.

## 3. Secrets in output

Run Curl.Console and the reference curl the same way against `Record-CurlExchange.ps1`'s
loopback server, with the probe secret `s3cr3t-probe`:

- `-u user:s3cr3t-probe` (HTTP basic, and `--digest` and `--ntlm` where the build allows);
- `--proxy-user user:s3cr3t-probe` with `-x http://127.0.0.1:<port>`;
- `-H "Authorization: Bearer s3cr3t-probe"`;
- an SSH key passphrase `--pass s3cr3t-probe` where an sftp:// or scp:// run is possible
  against the recorder's server.

Each with `-v`, and again with `--trace-ascii -`, capturing stdout and stderr. Any place Curl
prints `s3cr3t-probe`, or its base64 form (`dXNlcjpzM2NyM3QtcHJvYmU=` for `user:s3cr3t-probe`),
where real curl does not, is a finding; the reproduction is the two recorder commands. State
the reference curl's `--version` first line in your summary. Also search the six libraries and
`Curl.Console` for secrets passed to trace, log or diagnostic-log calls (`IDiagnosticLog`,
`Write`, `Trace`), and report any whose output real curl would not print.

## 4. What not to report

Hostile-server input a unit test already covers is not re-reported. A parser path that has no
negative test at all is a Low finding, unless the fuzzer found something on it.

## Severity

- **Critical** - memory-unsafe or unbounded behaviour on hostile input (a crash, a hang, an
  allocation sized by an attacker); a secret printed where real curl does not print it.
- **High** - a comparison of a secret that is not constant time; a secret-dependent branch or
  table lookup in a hand-built primitive.
- **Medium** - a secret passed to a log or trace call that is off by default.
- **Low** - a parser with no negative test and no fuzz finding; a parser that cannot be fuzzed.

## Keys

Follow the key rule in [Report-Format.md](Report-Format.md). Kinds: `fuzz-crash`, `fuzz-hang`,
`timing-leak`, `secret-dependent-branch`, `secret-dependent-lookup` (a table read at a secret
index, as in an S-box cipher), `secret-in-output`, `secret-in-log`,
`untested-parser`, `unfuzzable`.

## Method counts

Run every step above on every audit; re-audits come on top, never instead. Report `method.fuzzTargets` and `method.timingSitesRead` in `metrics` ([Report-Format.md](Report-Format.md#method-counts)): a report without them marks you unreliable (BL-1364).
