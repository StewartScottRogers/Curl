// Fuzz.cs - feeds mutated hostile input to Curl's public parsers and saves every input that
// makes one throw an unexpected exception or run too long (BL-1005, ADR-0267). The security
// auditor (BL-1010) runs it and raises what it finds. Base class library only, no fuzzing
// package: a seeded System.Random mutating a built-in seed corpus.
//
// Usage, from the repository root:
//   dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --target <name> --iterations N --seed S --out <dir>
//       [--max-ms 1000] [--corpus <dir>]
//   dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --replay <file.bin> --target <name> [--max-ms 1000]
//   dotnet run Audit/Tools/Fuzz/Fuzz.cs -- --self-test
//
// Targets:
//   tls-handshake  Curl.Tls: HandshakeMessageReader.Read on the input, then the decoder for
//                  the message type it read (ServerHello, NewSessionTicket,
//                  EncryptedExtensions, CertificateMessage, CertificateRequest,
//                  CertificateVerify, Finished), and EchConfigList.Decode on the whole input.
//                  A decode failure (TlsDecodeResult with an alert) is expected; an exception
//                  is not.
//   cli            Curl.Cli: CommandLineParser.Parse(arguments, pathExists: false, a prompt
//                  that answers "x", a data reader that finds no file). The input is the
//                  arguments as UTF-8 joined by NUL bytes, seeded from real option names in
//                  CommandLineOptionTable. A refusal is expected; an exception is not.
//   ssh            Curl.Protocol.Ssh: every SshWireDecoders method on the whole input -
//                  CountWholePacketsAsync (unprotected binary packets, back to back),
//                  TryInflatePayload (one zlib-compressed packet payload), TryDecodeKexInit
//                  (a KEXINIT payload, message number 20 first), TryDecodeSftpAttributes (SFTP
//                  attributes, flags first) and TryDecodeHostKeySignature (strings: host-key
//                  algorithm, host key blob, signature blob; then the exchange hash). Seeded
//                  with one well-formed input per method. A refusal (false, or a packet count
//                  short of the whole input) is expected; an exception is not (AF-0011,
//                  BL-1285).
//
// Mutations, one to three per input: bit flip; a byte set to 0x00, 0xFF, 0x7F or 0x80; a
// length field (one or two bytes) inflated or deflated; truncate; duplicate a slice; splice
// two seeds. Each input runs on a worker with a --max-ms limit: an exception is a crash, over
// the limit is a hang. Each is saved once per distinct exception type and top stack frame (one
// key for all hangs) as <out>/<target>-<n>.bin with <target>-<n>.txt: the exception, its stack,
// the seed and the iteration. Exit 0 when nothing was found, 1 when something was, 2 for an
// unknown target or bad arguments.
//
// The same --seed and --iterations give the same iterations, crashes and hangs; only the
// inputs per second depend on the machine.

#:project ../../../Curl.Tls.UnitLibrary/Curl.Tls.UnitLibrary.csproj
#:project ../../../Curl.Cli.UnitLibrary/Curl.Cli.UnitLibrary.csproj
#:project ../../../Curl.Protocol.Ssh.UnitLibrary/Curl.Protocol.Ssh.UnitLibrary.csproj

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Curl.Cli;
using Curl.Protocol.Ssh;
using Curl.Tls;

return FuzzApp.Run(args);

static class FuzzApp
{
    public static int Run(string[] args)
    {
        var options = ParseArguments(args);
        if (options is null) { return 2; }
        if (options.SelfTest) { return SelfTest.Run(); }

        var target = Targets.Find(options.Target);
        if (target is null)
        {
            Console.Error.WriteLine($"Unknown target '{options.Target}'. Targets: tls-handshake, cli, ssh.");
            return 2;
        }

        if (options.Replay is not null)
        {
            byte[] input;
            try { input = File.ReadAllBytes(options.Replay); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Console.Error.WriteLine($"Cannot read {options.Replay}: {e.Message}");
                return 2;
            }

            var outcome = Runner.RunOne(target, input, options.MaxMs);
            Console.WriteLine($"replay {Path.GetFileName(options.Replay)} on {target.Name}: {outcome.Describe()}");
            return outcome.Kind == OutcomeKind.Ok ? 0 : 1;
        }

        var corpus = target.Seeds().ToList();
        if (options.Corpus is not null)
        {
            corpus.AddRange(Directory.GetFiles(options.Corpus).OrderBy(f => f, StringComparer.Ordinal).Select(File.ReadAllBytes));
        }

        var summary = Runner.Fuzz(target, corpus, options.Iterations, options.Seed, options.MaxMs, options.Out!);
        Console.WriteLine(summary.Line());
        return summary.Crashes + summary.Hangs > 0 ? 1 : 0;
    }

    static Options? ParseArguments(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            try
            {
                switch (args[i])
                {
                    case "--target": o.Target = Value(); break;
                    case "--iterations": o.Iterations = int.Parse(Value()); break;
                    case "--seed": o.Seed = int.Parse(Value()); break;
                    case "--out": o.Out = Value(); break;
                    case "--max-ms": o.MaxMs = int.Parse(Value()); break;
                    case "--corpus": o.Corpus = Value(); break;
                    case "--replay": o.Replay = Value(); break;
                    case "--self-test": o.SelfTest = true; break;
                    default: Console.Error.WriteLine($"Unknown argument {args[i]}."); return null;
                }
            }
            catch (Exception e) when (e is ArgumentException or FormatException or OverflowException)
            {
                Console.Error.WriteLine(e.Message);
                return null;
            }
        }

        if (o.SelfTest) { return o; }
        if (o.Target is null) { Console.Error.WriteLine("Give --target tls-handshake, cli or ssh."); return null; }
        if (o.Replay is null && o.Out is null) { Console.Error.WriteLine("Give --out <dir> for saved inputs."); return null; }
        return o;
    }
}

sealed class Options
{
    public string? Target;
    public int Iterations = 10000;
    public int Seed;
    public string? Out;
    public int MaxMs = 1000;
    public string? Corpus;
    public string? Replay;
    public bool SelfTest;
}

sealed record FuzzTarget(string Name, Func<IEnumerable<byte[]>> Seeds, Action<byte[]> Run);

enum OutcomeKind { Ok, Crash, Hang }

sealed record Outcome(OutcomeKind Kind, Exception? Exception)
{
    public string Key => Kind switch
    {
        OutcomeKind.Hang => "hang",
        OutcomeKind.Crash => $"{Exception!.GetType().FullName} at {TopFrame(Exception)}",
        _ => "ok",
    };

    public string Describe() => Kind switch
    {
        OutcomeKind.Ok => "ok (no exception, within the time limit)",
        OutcomeKind.Hang => "hang (over the time limit)",
        _ => $"crash: {Exception!.GetType().FullName}: {Exception.Message} at {TopFrame(Exception)}",
    };

    static string TopFrame(Exception e)
    {
        var frame = new StackTrace(e, false).GetFrames().FirstOrDefault(f => f.GetMethod() is not null);
        var method = frame?.GetMethod();
        return method is null ? "(no frame)" : $"{method.DeclaringType?.FullName}.{method.Name}";
    }
}

sealed record Summary(string Target, int Iterations, double PerSecond, int Crashes, int Hangs, int Saved)
{
    public string Line() =>
        $"{Target}: iterations {Iterations}, inputs/s {PerSecond:0}, crashes {Crashes}, hangs {Hangs}, saved {Saved}";
}

static class Runner
{
    public static Outcome RunOne(FuzzTarget target, byte[] input, int maxMs)
    {
        Exception? caught = null;
        var work = Task.Run(() =>
        {
            try { target.Run(input); }
            catch (Exception e) { caught = e; }
        });
        // A worker that never returns is left running; the hang is recorded and fuzzing goes on.
        if (!work.Wait(maxMs)) { return new Outcome(OutcomeKind.Hang, null); }
        return caught is null ? new Outcome(OutcomeKind.Ok, null) : new Outcome(OutcomeKind.Crash, caught);
    }

    public static Summary Fuzz(FuzzTarget target, List<byte[]> corpus, int iterations, int seed, int maxMs, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var random = new Random(seed);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int crashes = 0, hangs = 0, saved = 0;
        var clock = Stopwatch.StartNew();
        for (int iteration = 1; iteration <= iterations; iteration++)
        {
            var input = Mutator.Mutate(random, corpus);
            var outcome = RunOne(target, input, maxMs);
            if (outcome.Kind == OutcomeKind.Ok) { continue; }
            if (outcome.Kind == OutcomeKind.Crash) { crashes++; } else { hangs++; }
            if (!seen.Add(outcome.Key)) { continue; }
            saved++;
            string stem = Path.Combine(outDir, $"{target.Name}-{saved}");
            File.WriteAllBytes(stem + ".bin", input);
            File.WriteAllText(stem + ".txt",
                $"target: {target.Name}{Environment.NewLine}seed: {seed}{Environment.NewLine}iteration: {iteration}{Environment.NewLine}"
                + $"outcome: {outcome.Describe()}{Environment.NewLine}{Environment.NewLine}{outcome.Exception}{Environment.NewLine}");
        }

        clock.Stop();
        double perSecond = iterations / Math.Max(clock.Elapsed.TotalSeconds, 0.001);
        return new Summary(target.Name, iterations, perSecond, crashes, hangs, saved);
    }
}

static class Mutator
{
    static readonly byte[] Interesting = [0x00, 0xFF, 0x7F, 0x80];

    public static byte[] Mutate(Random random, List<byte[]> corpus)
    {
        var data = new List<byte>(corpus[random.Next(corpus.Count)]);
        int count = 1 + random.Next(3);
        for (int m = 0; m < count; m++)
        {
            switch (random.Next(7))
            {
                case 0: // bit flip
                    if (data.Count > 0) { int i = random.Next(data.Count); data[i] ^= (byte)(1 << random.Next(8)); }
                    break;
                case 1: // interesting byte
                    if (data.Count > 0) { data[random.Next(data.Count)] = Interesting[random.Next(Interesting.Length)]; }
                    break;
                case 2: // length field inflate or deflate: one byte, or a big-endian two-byte field
                    if (data.Count > 1)
                    {
                        int i = random.Next(data.Count - 1);
                        int delta = random.Next(2) == 0 ? 1 + random.Next(16) : -(1 + random.Next(16));
                        if (random.Next(2) == 0) { data[i] = (byte)(data[i] + delta); }
                        else
                        {
                            int value = ((data[i] << 8) | data[i + 1]) + (delta * (1 + random.Next(64)));
                            data[i] = (byte)(value >> 8);
                            data[i + 1] = (byte)value;
                        }
                    }
                    break;
                case 3: // truncate
                    if (data.Count > 0) { int keep = random.Next(data.Count); data.RemoveRange(keep, data.Count - keep); }
                    break;
                case 4: // duplicate a slice
                    if (data.Count > 0)
                    {
                        int start = random.Next(data.Count);
                        int length = 1 + random.Next(Math.Min(32, data.Count - start));
                        data.InsertRange(random.Next(data.Count + 1), data.GetRange(start, length));
                    }
                    break;
                case 5: // splice two seeds
                    {
                        var other = corpus[random.Next(corpus.Count)];
                        int cut = random.Next(data.Count + 1);
                        int from = random.Next(other.Length + 1);
                        data.RemoveRange(cut, data.Count - cut);
                        data.AddRange(other.Skip(from));
                    }
                    break;
                default: // random byte
                    if (data.Count > 0) { data[random.Next(data.Count)] = (byte)random.Next(256); }
                    break;
            }
        }

        return [.. data];
    }
}

static class Targets
{
    public static FuzzTarget? Find(string? name) => name switch
    {
        "tls-handshake" => TlsHandshake,
        "cli" => Cli,
        "ssh" => Ssh,
        _ => null,
    };

    static readonly FuzzTarget Ssh = new("ssh", SshSeeds, RunSsh);

    public static SshSeedSet SshSeedsForTest() => new();

    static void RunSsh(byte[] input)
    {
        // The packet reader's connection completes synchronously over an array, and the
        // worker is already off the fuzzing thread, so blocking here costs nothing.
        _ = SshWireDecoders.CountWholePacketsAsync(input, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        _ = SshWireDecoders.TryInflatePayload(input);
        _ = SshWireDecoders.TryDecodeKexInit(input);
        _ = SshWireDecoders.TryDecodeSftpAttributes(input);
        _ = SshWireDecoders.TryDecodeHostKeySignature(input);
    }

    static IEnumerable<byte[]> SshSeeds()
    {
        var seeds = new SshSeedSet();
        yield return seeds.Packets;
        yield return seeds.CompressedPayload;
        yield return seeds.KexInit;
        yield return seeds.SftpAttributes;
        yield return seeds.Ed25519HostKeySignature;
    }

    /// <summary>One well-formed input for each SshWireDecoders method.</summary>
    public sealed class SshSeedSet
    {
        // Two unprotected packets: SSH_MSG_IGNORE with a string, then SSH_MSG_NEWKEYS.
        public byte[] Packets { get; } = [.. Packet([2, .. SshString("fuzz"u8.ToArray())]), .. Packet([21])];

        // A KEXINIT payload, zlib-compressed as the first packet after compression starts.
        // CompressionLevel.Optimal gives the same bytes every run.
        public byte[] CompressedPayload { get; } = Compress(KexInitPayload());

        public byte[] KexInit { get; } = KexInitPayload();

        // Flags size, uid/gid, permissions, times and one extended pair, with their fields.
        public byte[] SftpAttributes { get; } =
        [
            .. UInt32(0x8000000F), .. UInt32(0), .. UInt32(1234), .. UInt32(1000), .. UInt32(1000), .. UInt32(0x81A4),
            .. UInt32(1700000000), .. UInt32(1700000001), .. UInt32(1), .. SshString("a@b"u8.ToArray()), .. SshString("c"u8.ToArray()),
        ];

        // RFC 8032 section 7.1 test 1: the public key and its signature over the empty
        // message, which stands for the exchange hash.
        public byte[] Ed25519HostKeySignature { get; } =
        [
            .. SshString("ssh-ed25519"u8.ToArray()),
            .. SshString([.. SshString("ssh-ed25519"u8.ToArray()), .. SshString(Convert.FromHexString("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a"))]),
            .. SshString([.. SshString("ssh-ed25519"u8.ToArray()), .. SshString(Convert.FromHexString(
                "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b"))]),
        ];

        static byte[] KexInitPayload()
        {
            string[] lists =
            [
                "curve25519-sha256,ecdh-sha2-nistp256", "ssh-ed25519,rsa-sha2-256", "aes128-ctr,chacha20-poly1305@openssh.com",
                "aes128-ctr,chacha20-poly1305@openssh.com", "hmac-sha2-256", "hmac-sha2-256", "none,zlib@openssh.com",
                "none,zlib@openssh.com", "", "",
            ];
            return [20, .. Enumerable.Range(1, 16).Select(i => (byte)i), .. lists.SelectMany(l => SshString(Encoding.ASCII.GetBytes(l))), 0, .. UInt32(0)];
        }

        static byte[] Compress(byte[] payload)
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(payload);
                zlib.Flush();
            }

            return output.ToArray();
        }

        // RFC 4253 section 6: length, padding length, payload, at least 4 bytes of padding,
        // length field included in a multiple of 8.
        static byte[] Packet(byte[] payload)
        {
            int padding = 8 - ((4 + 1 + payload.Length) % 8);
            if (padding < 4) { padding += 8; }
            return [.. UInt32((uint)(1 + payload.Length + padding)), (byte)padding, .. payload, .. new byte[padding]];
        }

        static byte[] SshString(byte[] value) => [.. UInt32((uint)value.Length), .. value];

        static byte[] UInt32(uint value)
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
            return bytes;
        }
    }

    static readonly FuzzTarget TlsHandshake = new("tls-handshake", TlsSeeds, RunTls);

    public static IEnumerable<byte[]> TlsSeedsForTest() => TlsSeeds();

    static readonly FuzzTarget Cli = new("cli", CliSeeds, RunCli);

    static void RunTls(byte[] input)
    {
        _ = EchConfigList.Decode(input);
        var read = HandshakeMessageReader.Read(input);
        if (read.Status != HandshakeMessageReadStatus.Complete || read.Message is null) { return; }
        byte[] body = read.Message.Body;
        _ = read.Message.Type switch
        {
            HandshakeType.ServerHello => (object)ServerHello.Decode(body),
            HandshakeType.NewSessionTicket => NewSessionTicket.Decode(body),
            HandshakeType.EncryptedExtensions => EncryptedExtensions.Decode(body),
            HandshakeType.Certificate => CertificateMessage.Decode(body),
            HandshakeType.CertificateRequest => CertificateRequest.Decode(body),
            HandshakeType.CertificateVerify => CertificateVerify.Decode(body),
            HandshakeType.Finished => Finished.Decode(body),
            _ => null!,
        };
    }

    static byte[] Message(HandshakeType type, byte[] body) =>
        [(byte)type, (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length, .. body];

    static IEnumerable<byte[]> TlsSeeds()
    {
        byte[] random = [.. Enumerable.Range(1, 32).Select(i => (byte)i)];
        // ServerHello: TLS 1.2 legacy version, random, empty session id, TLS_AES_128_GCM_SHA256,
        // null compression, supported_versions = TLS 1.3.
        yield return Message(HandshakeType.ServerHello, [0x03, 0x03, .. random, 0x00, 0x13, 0x01, 0x00, 0x00, 0x06, 0x00, 0x2B, 0x00, 0x02, 0x03, 0x04]);
        yield return Message(HandshakeType.EncryptedExtensions, [0x00, 0x04, 0x00, 0x10, 0x00, 0x00]);
        yield return Message(HandshakeType.Certificate, [0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x04, 0x30, 0x02, 0x05, 0x00, 0x00, 0x00]);
        yield return Message(HandshakeType.CertificateRequest, [0x00, 0x00, 0x08, 0x00, 0x0D, 0x00, 0x04, 0x00, 0x02, 0x08, 0x04]);
        yield return Message(HandshakeType.CertificateVerify, [0x08, 0x04, 0x00, 0x04, 0xDE, 0xAD, 0xBE, 0xEF]);
        yield return Message(HandshakeType.Finished, random);
        yield return Message(HandshakeType.NewSessionTicket, [0x00, 0x00, 0x1C, 0x20, 0x01, 0x02, 0x03, 0x04, 0x01, 0x00, 0x00, 0x04, 0xAA, 0xBB, 0xCC, 0xDD, 0x00, 0x00]);
        // An ECHConfigList with one draft-13 config (50 bytes of contents): config id 1, kem
        // X25519 (0x0020), a 32-byte public key, one cipher suite (HKDF-SHA256, AES-128-GCM),
        // maximum name length 64, public name "a.b", no extensions.
        yield return [0x00, 0x36, 0xFE, 0x0D, 0x00, 0x32, 0x01, 0x00, 0x20, 0x00, 0x20, .. random,
            0x00, 0x04, 0x00, 0x01, 0x00, 0x01, 0x40, 0x03, (byte)'a', (byte)'.', (byte)'b', 0x00, 0x00];
    }

    static void RunCli(byte[] input)
    {
        var arguments = Encoding.UTF8.GetString(input).Split('\0');
        _ = CommandLineParser.Parse(arguments, static _ => false, new AnswerPrompt(), new NoFileReader());
    }

    static IEnumerable<byte[]> CliSeeds()
    {
        static byte[] Join(params string[] a) => Encoding.UTF8.GetBytes(string.Join('\0', a));
        yield return Join("https://example.test/");
        yield return Join("-X", "POST", "-d", "a=1&b=2", "-H", "Accept: */*", "https://example.test/x");
        yield return Join("--max-time", "5", "--retry", "3", "-o", "out.txt", "http://example.test/");
        yield return Join("-u", "user:pass", "--proxy", "socks5h://proxy.test:1080", "https://example.test/");
        yield return Join("-w", "%{http_code} %{time_total}\\n", "-s", "-S", "-L", "--max-redirs", "10", "http://a.test/");
        yield return Join("-F", "file=@x.txt;type=text/plain", "--form-string", "a=b", "http://a.test/upload");
        yield return Join("-r", "0-99", "-C", "-", "--limit-rate", "10k", "ftp://ftp.test/file");
        yield return Join("-K", "config.txt", "--next", "-I", "http://b.test/");
        // Every option once, with a value where it takes one, so each name is in the corpus.
        foreach (var chunk in CommandLineOptionTable.Rows.Chunk(8))
        {
            var args = new List<string>();
            foreach (var option in chunk)
            {
                args.Add("--" + option.LongName);
                if (option.TakesValue) { args.Add("1"); }
            }

            args.Add("http://example.test/");
            yield return Join([.. args]);
        }
    }

    sealed class AnswerPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => "x";
    }

    sealed class NoFileReader : IDataFileReader
    {
        public bool TryReadFile(string path, out byte[] contents)
        {
            contents = [];
            return false;
        }

        public byte[] ReadStandardInput() => [];

        public bool TryReadModificationTime(string path, out DateTimeOffset modificationTime, out string? failureReason)
        {
            modificationTime = default;
            failureReason = "fuzzing: no file";
            return false;
        }
    }
}

static class SelfTest
{
    public static int Run()
    {
        int failed = 0;
        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}: {detail}");
            if (!ok) { failed++; }
        }

        string dir = Path.Combine(Path.GetTempPath(), "fuzz-selftest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var thrower = new FuzzTarget("fake-throw", () => [[0x00, 0x01, 0x02, 0x03]],
                input => { if (input.Length > 0 && input[0] == 0xFF) { throw new IndexOutOfRangeException("fake"); } });
            var summary = Runner.Fuzz(thrower, thrower.Seeds().ToList(), 5000, 1, 1000, dir);
            var bin = Directory.GetFiles(dir, "fake-throw-*.bin").FirstOrDefault();
            var txt = bin is null ? null : Path.ChangeExtension(bin, ".txt");
            Check("a throwing input is saved and starts 0xFF", bin is not null && File.ReadAllBytes(bin) is [0xFF, ..], $"{summary.Line()}; {bin ?? "no .bin"}");
            Check("its .txt names the exception", txt is not null && File.ReadAllText(txt).Contains("System.IndexOutOfRangeException"), txt ?? "no .txt");
            Check("one saved input per distinct exception and frame", summary.Saved == 1 && summary.Crashes >= 1, summary.Line());

            var sleeper = new FuzzTarget("fake-sleep", () => [[0x00]], _ => Thread.Sleep(2000));
            var hang = Runner.Fuzz(sleeper, sleeper.Seeds().ToList(), 1, 1, 1000, dir);
            Check("a 2 s input is a hang", hang.Hangs == 1 && File.Exists(Path.Combine(dir, "fake-sleep-1.bin")), hang.Line());

            var replay = Runner.RunOne(thrower, bin is null ? [] : File.ReadAllBytes(bin), 1000);
            Check("replaying the saved input crashes again", replay.Kind == OutcomeKind.Crash, replay.Describe());

            // The seeds must reach the decoders, or fuzzing them proves nothing.
            var tlsSeeds = Targets.TlsSeedsForTest().ToList();
            int complete = tlsSeeds.Count(s => HandshakeMessageReader.Read(s).Status == HandshakeMessageReadStatus.Complete);
            Check("every TLS handshake seed reads as a complete message", complete == tlsSeeds.Count - 1, $"{complete} of {tlsSeeds.Count - 1} (the last seed is an ECHConfigList)");
            Check("the ECHConfigList seed decodes", EchConfigList.Decode(tlsSeeds[^1]).Succeeded, "EchConfigList.Decode");
            Check("the ServerHello seed decodes", ServerHello.Decode(HandshakeMessageReader.Read(tlsSeeds[0]).Message!.Body).Succeeded, "ServerHello.Decode");

            var ssh = Targets.SshSeedsForTest();
            int packets = SshWireDecoders.CountWholePacketsAsync(ssh.Packets, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            Check("the SSH packet seed reads as two whole packets", packets == 2, $"{packets} packets");
            Check("the SSH compressed-payload seed inflates", SshWireDecoders.TryInflatePayload(ssh.CompressedPayload), "TryInflatePayload");
            Check("the SSH KEXINIT seed decodes", SshWireDecoders.TryDecodeKexInit(ssh.KexInit), "TryDecodeKexInit");
            Check("the SFTP attributes seed decodes", SshWireDecoders.TryDecodeSftpAttributes(ssh.SftpAttributes), "TryDecodeSftpAttributes");
            Check("the ssh-ed25519 host key and signature seed decodes", SshWireDecoders.TryDecodeHostKeySignature(ssh.Ed25519HostKeySignature), "TryDecodeHostKeySignature");
            byte[] cut = ssh.KexInit[..^5];
            Check("a cut-short KEXINIT is refused, not thrown", !SshWireDecoders.TryDecodeKexInit(cut), "TryDecodeKexInit on the seed minus 5 bytes");

            var again = Runner.Fuzz(thrower, thrower.Seeds().ToList(), 5000, 1, 1000, Path.Combine(dir, "again"));
            Check("the same seed gives the same counts", again.Crashes == summary.Crashes && again.Saved == summary.Saved, $"{again.Crashes}/{summary.Crashes}");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        return failed == 0 ? 0 : 1;
    }
}
