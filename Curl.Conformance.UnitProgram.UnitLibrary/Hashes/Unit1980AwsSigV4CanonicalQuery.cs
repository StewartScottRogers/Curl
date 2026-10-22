using System.Text;
using Curl.Authentication;

namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1980.c</c>: AWS SigV4's <c>canon_query</c>, here
/// <see cref="AwsSigV4UriEncoding.CanonicalQuery"/>.
/// </summary>
public sealed class Unit1980AwsSigV4CanonicalQuery : IUnitProgram
{
    private static readonly (string Name, string QueryPart, string CanonicalQuery)[] Rows =
    [
        ("no-value", "Param1=", "Param1="),
        ("test-439", "name=me&noval&aim=b%aad&weirdo=*.//-", "aim=b%AAd&name=me&noval=&weirdo=%2A.%2F%2F-"),
        ("blank-query-params", "hello=a&b&c=&d", "b=&c=&d=&hello=a"),
        ("get-vanilla-query-order-key-case", "Param2=value2&Param1=value1", "Param1=value1&Param2=value2"),
        ("get-vanilla-query-unreserved",
            "-._~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz=-._~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
            "-._~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz=-._~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"),
        ("get-vanilla-empty-query-key", "Param1=value1", "Param1=value1"),
        ("get-vanilla-query-order-encoded", "Param-3=Value3&Param=Value2&%E1%88%B4=Value1", "%E1%88%B4=Value1&Param=Value2&Param-3=Value3"),
        ("space-plus", "p3= &p1=+&p2=%20", "p1=%20&p2=%20&p3=%20"),
        ("2b-incoming", "p3=%2b&p1=+", "p1=%20&p3=%2B"),
    ];

    /// <inheritdoc />
    public string ProgramName => "unit1980";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((string name, string queryPart, string canonicalQuery) in Rows)
        {
            string? received = AwsSigV4UriEncoding.CanonicalQuery(queryPart, Encoding.UTF8);
            checks.FailUnless(received == canonicalQuery, $"{name}: Received \"{received}\" and should be \"{canonicalQuery}\"");
        }

        return Task.FromResult(checks.Result);
    }
}
