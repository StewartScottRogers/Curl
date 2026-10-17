using Curl.Api.EngineContracts;
using Curl.Conformance.LibTest;

namespace Curl.Conformance;

/// <summary>
/// What <see cref="UpstreamCaseRunner"/> needs to run a case whose <c>&lt;client&gt;&lt;tool&gt;</c> line
/// names a libtest program (<c>lib%TESTNUMBER</c> or <c>libNNN</c>): the ported programs, and how to make
/// the <c>Libcurl</c> engine a program's <c>curl_global_init</c> takes, reaching the case's servers (BL-2556).
/// </summary>
/// <param name="Programs">The ported programs; a lib case whose program is not among them is skipped as not ported.</param>
/// <param name="CreateEngine">
/// Makes the engine for one run from the invocation the runner built: its connectors reach the case's
/// in-memory servers and its environment is the case's <c>&lt;setenv&gt;</c>. The program owns the engine.
/// </param>
public sealed record UpstreamLibTests(LibTestPrograms Programs, Func<UpstreamCurlInvocation, ICurlEngine> CreateEngine);
