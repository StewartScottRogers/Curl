namespace Curl.Extension.Grpc.Client;

/// <summary>
/// A temporary directory holding <c>greeter.proto</c>, which imports <c>reply.proto</c>: service
/// <c>helloworld.Greeter</c> with <c>SayHello(HelloRequest) returns (HelloReply)</c>. Deleted on dispose.
/// </summary>
internal sealed class ProtoFixture : IDisposable
{
    private const string Greeter = """
        syntax = "proto3";
        package helloworld;
        import "reply.proto";
        service Greeter {
          rpc SayHello (HelloRequest) returns (HelloReply);
        }
        message HelloRequest {
          string name = 1;
        }
        """;

    private const string Reply = """
        syntax = "proto3";
        package helloworld;
        message HelloReply {
          string message = 1;
        }
        """;

    public ProtoFixture()
    {
        Directory = Path.Combine(Path.GetTempPath(), "grpc-" + Guid.NewGuid().ToString("N"));
        _ = System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(Path.Combine(Directory, "greeter.proto"), Greeter);
        File.WriteAllText(Path.Combine(Directory, "reply.proto"), Reply);
    }

    /// <summary>Gets the directory.</summary>
    public string Directory { get; }

    /// <summary>Gets the path of <c>greeter.proto</c>.</summary>
    public string GreeterPath => Path.Combine(Directory, "greeter.proto");

    /// <summary>Writes another file into the directory and gives its path.</summary>
    public string Write(string name, string text)
    {
        string path = Path.Combine(Directory, name);
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
}
