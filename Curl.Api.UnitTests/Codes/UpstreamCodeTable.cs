using System.Globalization;

namespace Curl.Api.UnitTests.Codes;

/// <summary>Checks an enum against a table of upstream curl's members and numbers.</summary>
internal static class UpstreamCodeTable
{
    /// <summary>
    /// Asserts each member in <paramref name="table"/> has its upstream number, and that the enum
    /// has exactly the table's members.
    /// </summary>
    public static void AssertMatches<TCode>((TCode Code, int Number)[] table)
        where TCode : struct, Enum
    {
        foreach (var (code, number) in table)
        {
            Assert.AreEqual(number, Convert.ToInt32(code, CultureInfo.InvariantCulture), code.ToString());
        }

        CollectionAssert.AreEquivalent(table.Select(entry => entry.Code).ToArray(), Enum.GetValues<TCode>());
    }
}
