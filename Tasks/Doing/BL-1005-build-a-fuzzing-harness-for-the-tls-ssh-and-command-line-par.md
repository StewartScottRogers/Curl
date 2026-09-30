---
id: BL-1005
title: Build a fuzzing harness for the TLS, SSH and command-line parsers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1000]
touches: [Audit/Tools/Fuzz]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1005 — Build a fuzzing harness for the TLS, SSH and command-line parsers

## Goal

`dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target <name> --iterations N --seed S --out <dir>` feeds mutated hostile input to Curl's public parsers and saves every input that makes one throw an unexpected exception or run too long, with the exception, so the security auditor (BL-1010) can reproduce it.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1005`.
No fuzzing package (base class library only); a C# file-based app, as CLAUDE.md allows
for tools, referencing the libraries under test with `#:project` directives.

Isolate it from the product's build gates the way `.github/gource/` does: that folder's
`Directory.Build.props`, `Directory.Build.targets` and `Directory.Packages.props` stop
MSBuild from importing the root ones (CA1502, XML docs, the AOT check) for its
file-based apps. Copy that arrangement into `Audit/Tools/Fuzz/`; the referenced
libraries still build with their own root settings.

Targets (public entry points confirmed on 2026-09-29):

- `tls-handshake`: `Curl.Tls.UnitLibrary`'s `HandshakeMessageReader.Read(ReadOnlySpan<byte>)`,
  then the body decoders `ServerHello.Decode`, `CertificateMessage.Decode`,
  `EncryptedExtensions.Decode`, `NewSessionTicket.Decode`, `CertificateRequest.Decode`,
  `CertificateVerify.Decode`, `Finished.Decode`, `EchConfigList.Decode`. They return
  `TlsDecodeResult<T>`; a decode failure is expected, an exception is not.
- `cli`: `Curl.Cli.UnitLibrary`'s `CommandLineParser.Parse(IReadOnlyList<string>, Func<string,bool>)`
  with `pathExists` returning false; argument lists built from real option names
  (`CommandLineOptionTable`) and mutated values. A `CommandLineParseResult` carrying a
  refusal is expected; an exception is not.
- `ssh`: the public decoders `Curl.Protocol.Ssh.UnitLibrary` exposes (its internals are
  visible only to its tests; find the public packet and message readers with `grep
  "public static" Curl.Protocol.Ssh.UnitLibrary`, and list the ones chosen in the app's
  header comment). If it exposes none that take raw bytes, the target reports that and
  exits 2, and the task's Notes say so for BL-1010 to raise as a finding.

Engine: seeded `System.Random`; a seed corpus per target (valid inputs written in the
app, e.g. a well-formed ServerHello, plus any files in `--corpus <dir>`); mutations: bit
flip, byte set to 0x00/0xFF/0x7F/0x80, length-field inflate and deflate, truncate,
duplicate a slice, splice two seeds. Per input, a `Stopwatch`; over `--max-ms` (default
1000) is a hang. Each crash or hang is saved once per distinct exception type and top
stack frame as `<out>/<target>-<n>.bin` plus `<target>-<n>.txt` (exception, stack,
seed, iteration). Exit 0 when nothing was found, 1 when something was.

## Acceptance criteria

- [ ] `dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target tls-handshake --iterations 20000 --seed 1 --out <tmp>` completes and prints a summary line with iterations, inputs per second, crashes and hangs.
- [ ] The same for `--target cli` and `--target ssh` (or, for `ssh`, the documented exit 2 and message).
- [ ] `--self-test` proves detection: a built-in fake target that throws `IndexOutOfRangeException` on inputs starting `0xFF` produces a saved `.bin` that starts `0xFF` and a `.txt` naming the exception, and a fake target that sleeps 2 s is reported as a hang.
- [ ] `--replay <file.bin> --target <name>` reruns one saved input and prints the outcome.
- [ ] The same seed and iteration count give the same summary numbers on a second run.
- [ ] `dotnet build Curl.slnx -warnaserror` is unaffected (the app is not in the solution) and no `PackageReference` is added anywhere.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
