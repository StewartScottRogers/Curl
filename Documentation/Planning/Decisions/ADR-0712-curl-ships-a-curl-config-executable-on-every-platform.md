# ADR-0712 — Curl ships a `curl-config` executable on every platform

- Status: Accepted
- Date: 2026-10-19
- Task: BL-2853
- Decided by Claude under Stewart's delegation.

## Context

Upstream installs `curl-config` beside `curl`: a script that reports the libcurl build's
version, protocols, features and build flags. Upstream cases 1013 and 1014 compare
`curl --version`'s `Protocols:` and `Features:` lines with `curl-config --protocols` and
`--feature`; 1022 and 1023 compare the version and numeric version with `--version` and
`--vernum`. The harness skips all four because Curl ships no `../curl-config`.

## What was measured

Run on Windows on 2026-10-19 against the Windows reference (ADR-0018), `curl 8.22.0
(x86_64-w64-mingw32)`, `Schannel`, `/ucrt64/bin/curl`:

- `ls /ucrt64/bin | grep -i curl` lists `curl.exe`, `libcurl-4.dll`, `libcurl-openssl-4.dll`
  and `wcurl`: **the Windows reference build has no `curl-config`**, as the curl.se Windows
  zip has none. `curl-config --version` there is "No such file or directory".
- Linux and macOS were not run in this Windows-only lane. Upstream's own `curl-config.in`
  (8.22.0) and the `libcurl-dev` / Homebrew packages install one; those are the reference
  there, and BL-2854 pins their answers from the curl 8.22.0 tarball's script, not from a
  guess.
- `Libcurl`'s version information is `Curl.Api.UnitLibrary.EngineContracts.CurlVersionInfo`
  (ADR-0506); its `VersionNumber` is `0x081600` (8.22.0).

## Decision

**(a) Ship `curl-config` on all platforms.** The default answer to every question is what a
complete reimplementation needs, and cases 1013, 1014, 1022 and 1023 can only run against a
real executable. Where the platform's curl ships none (Windows) Curl still does, so the same
four cases are measurable on every platform; it is a superset of the reference, not a
difference a script could trip on.

### Projects

- `Curl.Config` — the executable, assembly name `curl-config`, native AOT, a thin host
  (an exception to the `Curl.<Area>.UnitLibrary` rule, like `Curl.Console` and `Curl.Mcp`).
- `Curl.Config.UnitLibrary` — the option answers, reading `Libcurl`'s version information.
- `Curl.Config.UnitTests` — tests both. All three sit in `Curl.slnx` in the flat alphabetical
  run, between `Curl.Cli.UnitLibrary`'s tests and `Curl.Conformance.*`, and carry the four
  quality gates. BL-2854 files them with `/new-project`.

### Answers (same on every platform unless stated)

| Option | Answer |
|---|---|
| `--version` | `libcurl 8.22.0` |
| `--vernum` | `081600` |
| `--protocols` | `curl --version`'s `Protocols:` names, upper-cased, one per line, in that order |
| `--feature` | the `Features:` names as upstream's `curl-config` spells them (upper-cased where upstream does), one per line |
| `--ssl-backends` | `Schannel` on Windows, `OpenSSL` elsewhere (the platform's curl) |
| `--built-shared` | `no` (Curl links no shared libcurl) |
| `--prefix` | the directory above the executable's directory |
| `--ca` | the default CA bundle path, empty line when Curl has none |
| `--cc` | `dotnet` |
| `--cflags`, `--libs`, `--static-libs` | an empty line (nothing to compile or link against) |
| `--configure` | an empty line |
| `--checkfor <ver>` | exit 0 when 8.22.0 is at least `<ver>`, else the upstream message on stdout and exit 1 |
| `--help` | upstream's usage text, exit 0 |
| an unknown option | `unknown option: <option>` and the usage, both on standard output, exit 1, as the 8.22.0 tarball's `curl-config.in` writes them (measured by BL-2854) |

An empty answer for the compiler options is truthful: a .NET program has no C flags.

### The harness

The runner no longer skips the four cases. The postcheck `%PERL ... test1013.pl|test1022.pl
../curl-config ...` is interpreted in C# in the manner of `UpstreamTest610Script`, running the
built `curl-config` and comparing as the Perl script does.

## Consequences

- BL-2854 implements this and is edited to match (its `touches` gain the three projects and
  `Curl.slnx`).
- A new Glossary entry, `curl-config`, records what it does.
- A test reads both `curl --version` and `curl-config` from `Libcurl`'s version information so
  they cannot drift.

## Amendment (BL-2854, 2026-10-21)

Decided by Claude under Stewart's delegation. Read from curl 8.22.0's `curl-config.in`:

- An unknown option prints `unknown option: <option>` and the usage on **standard output**, not
  standard error, and exits 1; no option at all prints the usage and exits 1. Curl does the same.
- `--features` is the option upstream's usage names, and case 1014 runs it; `--feature` is accepted
  too, as the script accepts both.
- `--version`, `--vernum`, `--help` and `--checkfor` end the run; other options print in turn.
- `--checkfor` compares field by field with `cut` and `sh test` semantics (a field that is not a
  number fails every comparison), so `--checkfor` with no version says
  `requested version  is newer than existing 8.22.0` and exits 1, as the script does.
- `--protocols` is libcurl's `curl_version_info` protocols with `ipfs` and `ipns` added beside
  `http`, sorted and upper-cased, because the curl tool adds those two to `curl -V` and upstream's
  configure lists them in `curl-config`.
- The `Curl.Config` host holds one statement forwarding to `CurlConfigProgram.RunAsync`; the
  gated code is `Curl.Config.UnitLibrary`. `Measure-CodeQuality.ps1` does not list the host.
- The conformance harness runs the same `CurlConfigCommand` in process in place of `../curl-config`,
  rather than starting the executable, so the four cases stay unit tests with no child process.
