# Curl.Config

The `curl-config` executable (ADR-0712): assembly `curl-config`, native AOT, shipped on every
platform beside `curl`. It is a naming exception, like `Curl.Console` and `Curl.Mcp`, because it is
not a library. Namespace `Curl.Config`.

It holds only `Program.cs`, which forwards the arguments, standard output, the platform and the
executable's folder to `CurlConfigProgram.RunAsync` in `Curl.Config.UnitLibrary`. Every answer lives
there; a branch added here is a sign it belongs there. Its tests live in `Curl.Config.UnitTests`,
which tests both projects.
