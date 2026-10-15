namespace Curl.Api.UnitTests.Options.Protocols;

/// <summary>Asserts that a <c>with</c> copy of an option record changes only the option it sets.</summary>
internal static class OptionRecordAssert
{
    /// <summary>
    /// Asserts <paramref name="changed"/> differs from <paramref name="defaults"/>, and that
    /// <paramref name="restore"/>, which puts back only the option that was set, makes it equal again.
    /// </summary>
    public static void ChangesOnly<T>(T defaults, T changed, Func<T, T> restore)
    {
        Assert.AreNotEqual(defaults, changed);
        Assert.AreEqual(defaults, restore(changed));
    }
}
