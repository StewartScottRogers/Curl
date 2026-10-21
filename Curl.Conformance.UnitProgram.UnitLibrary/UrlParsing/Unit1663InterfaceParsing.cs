using Curl.Cli;
using Curl.Conformance.UnitProgram.Hashes;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1663.c</c>: <c>Curl_parse_interface</c>, here
/// <see cref="InterfaceBinding.Parse"/>. <c>CURLE_BAD_FUNCTION_ARGUMENT</c> is
/// <see cref="InterfaceBinding.IsMalformed"/>; <c>dev</c>, <c>iface</c> and <c>host</c> are
/// <see cref="InterfaceBinding.InterfaceOrHostName"/>, <see cref="InterfaceBinding.InterfaceName"/> and
/// <see cref="InterfaceBinding.HostName"/>. Each of the twelve rows makes the C program's checks in its order:
/// the result, whether each part is set, and, while no check has failed yet, each part's text.
/// </summary>
/// <param name="parse">Splits a <c>--interface</c> value; <see cref="InterfaceBinding.Parse"/> unless a test injects another.</param>
public sealed class Unit1663InterfaceParsing(Func<string, InterfaceBinding> parse) : IUnitProgram
{
    private static readonly (string Input, string? Device, string? Interface, string? Host, bool Refused)[] Rows =
    [
        ("dev", "dev", null, null, false),
        ("if!eth0", null, "eth0", null, false),
        ("host!myname", null, null, "myname", false),
        ("ifhost!eth0!myname", null, "eth0", "myname", false),
        ("", null, null, null, true),
        ("!", "!", null, null, false),
        ("if!", null, null, null, true),
        ("if!eth0!blubb", null, "eth0!blubb", null, false),
        ("host!", null, null, null, true),
        ("ifhost!", null, null, null, true),
        ("ifhost!eth0", null, null, null, true),
        ("ifhost!eth0!", null, null, null, true),
    ];

    /// <summary>Creates the port over <see cref="InterfaceBinding.Parse"/>.</summary>
    public Unit1663InterfaceParsing()
        : this(InterfaceBinding.Parse)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1663";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((string input, string? device, string? interfaceName, string? host, bool refused) in Rows)
        {
            InterfaceBinding binding = parse(input);
            checks.FailUnless(binding.IsMalformed == refused, "Curl_parse_interface() failed");
            checks.FailUnless(device is null == binding.InterfaceOrHostName is null, "dev expectation failed.");
            checks.FailUnless(interfaceName is null == binding.InterfaceName is null, "iface expectation failed");
            checks.FailUnless(host is null == binding.HostName is null, "host expectation failed");

            // With every presence check passed, the C program's "!x || !exp || !strcmp(x, exp)" is plain equality.
            if (checks.Result.FailureCount == 0)
            {
                checks.FailUnless(binding.InterfaceOrHostName == device, "dev should be equal to exp_dev");
                checks.FailUnless(binding.InterfaceName == interfaceName, "iface should be equal to exp_iface");
                checks.FailUnless(binding.HostName == host, "host should be equal to exp_host");
            }
        }

        return Task.FromResult(checks.Result);
    }
}
