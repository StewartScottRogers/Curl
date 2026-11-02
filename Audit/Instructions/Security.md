# Security auditor: method

You are the security auditor (`.claude/agents/audit-security.md`). Curl builds its own
cryptography, TLS, SSH, QUIC, Kerberos and NTLM rather than using a vetted library, so this is
where a mistake costs most. Read [Auditor-Rules.md](Auditor-Rules.md) first; it binds you.
Report in [Report-Format.md](Report-Format.md), with `"auditor": "security"`.

Steps 1 and 2 cover these six libraries; step 3 covers every library and host, since a
secret can be printed by any of them (the AWS_SIGV4 line is Curl.Engine's, the CONNECT tunnel's
Curl.Networking's):

- `Curl.Cryptography.UnitLibrary`
- `Curl.Tls.UnitLibrary`
- `Curl.Original.Protocol.Ssh.UnitLibrary`
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

The `ssh` target fuzzes `Curl.Original.Protocol.Ssh.UnitLibrary`'s readers of server bytes through
its public `SshWireDecoders` (ADR-0394): the packet reader, zlib inflation, `KEXINIT`, SFTP
attributes and host-key signatures, every method on every input.

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
loopback server, with the probe secret `s3cr3t-probe`. Run every probe below on every audit:
each one reaches a different line that prints "with user" or "auth using", and a probe left out
leaves its line unread.

- `-u user:s3cr3t-probe` with each server auth scheme that writes `Server auth using <scheme>
  with user`: plain (Basic), `--basic`, `--digest`, `--ntlm`, `--negotiate`, `--anyauth`, and
  `--oauth2-bearer s3cr3t-probe` (Bearer), where the build allows each;
- `--aws-sigv4 "aws:amz:us-east-1:s3" -u AKID:s3cr3t-probe` (AWS Signature Version 4, which
  writes `Server auth using AWS_SIGV4 with user`), and the `--httpsig-*` options with
  `-u user:s3cr3t-probe` where the build has them (`Server auth using HTTPSIG`);
- `--proxy-user user:s3cr3t-probe` with `-x http://127.0.0.1:<port>`, with each proxy auth
  scheme (`--proxy-basic`, `--proxy-digest`, `--proxy-ntlm`), which write `Proxy auth using`;
- a CONNECT tunnel: the same `--proxy-user` with `--proxytunnel` and an `http://` URL, and with
  an `https://` URL through `-x` (curl tunnels https:// through an HTTP proxy with CONNECT),
  which write `Proxy auth using ...` and `Establishing HTTP proxy tunnel to`; the recorder's
  server need not finish the tunnel, only answer the CONNECT;
- `-H "Authorization: Bearer s3cr3t-probe"`;
- `ws://127.0.0.1:<port>/` with `-u user:s3cr3t-probe` where the build has WebSocket;
- an SSH key passphrase `--pass s3cr3t-probe` where an sftp:// or scp:// run is possible
  against the recorder's server.

Each with `-v`, and again with `--trace-ascii -`, capturing stdout and stderr. Any place Curl
prints `s3cr3t-probe`, or its base64 form (`dXNlcjpzM2NyM3QtcHJvYmU=` for `user:s3cr3t-probe`,
`QUtJRDpzM2NyM3QtcHJvYmU=` for `AKID:s3cr3t-probe`), where real curl does not, is a finding; the
reproduction is the two recorder commands. State the reference curl's `--version` first line in
your summary, and list every probe you ran and any the build refused.

Then scan every library and host - not only the six above - for secrets passed to a verbose,
trace, log or diagnostic writer:

```powershell
powershell -NoProfile -File Audit/Tools/Find-SecretsInDiagnostics.ps1 -Root <audited tree> -OutFile <temp folder>\secrets.json
```

It lists every call to a `Report*`, `Info`, `Warn*`, `Trace*`, `Debug`, `Verbose*` or `Log*`
member, or a `Write` on an error, verbose, trace or diagnostic writer, whose arguments pass an
identifier ending in password, passphrase, secret, key or token. Read every candidate: file the
ones whose output real curl would not print (`secret-in-output` when it reaches `-v` or a
`--trace` by default, `secret-in-log` otherwise), and say in your summary how many you read and
filed. A candidate on a line a probe above reaches is confirmed or cleared by that probe.

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

Run every step above on every audit; re-audits come on top, never instead. Report `method.fuzzTargets`, `method.timingSitesRead`, and `secretProbesRun` and `secretCandidatesRead` (step 3's probes and scan candidates) in `metrics` ([Report-Format.md](Report-Format.md#method-counts)): a report without them marks you unreliable (BL-1364).
