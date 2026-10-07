// The assertions every test in this project calls (BL-1467). Assert, CollectionAssert and
// StringAssert here sit in the tests' own namespace, so they take precedence over MSTest's
// imported ones without a change to any test: each writes, through the shared
// TestDiagnostics helper (Documentation/Wiki/Test-Diagnostics.md), the value it was given to
// expect as an ARRANGE line, the value the test produced as an ACT line, and an ASSERT or
// DIFF line comparing them, then hands the same arguments to MSTest's assertion unchanged.
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Curl.Testing;
using MSTestAssert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using MSTestCollectionAssert = Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using MSTestStringAssert = Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;

namespace Curl.Kerberos;

/// <summary>Writes the running test's expected value, actual value and comparison before each MSTest assertion.</summary>
internal static class DiagnosticAssertionLines
{
    private const int CollectionItemCap = 16;

    /// <summary>Writes the ARRANGE, ACT and ASSERT or DIFF lines for one comparison.</summary>
    /// <param name="assertion">The assertion's name, the ASSERT or DIFF line's label.</param>
    /// <param name="expected">What the test expects.</param>
    /// <param name="actual">What the test produced.</param>
    public static void WriteComparison(string assertion, object? expected, object? actual)
    {
        var diagnostics = Current();
        WriteValue(diagnostics, isExpected: true, expected);
        WriteValue(diagnostics, isExpected: false, actual);
        switch (expected, actual)
        {
            case (byte[] expectedBytes, byte[] actualBytes):
                diagnostics.Diff(assertion, expectedBytes, actualBytes);
                break;
            case (string expectedText, string actualText):
                diagnostics.Diff(assertion, expectedText, actualText);
                break;
            default:
                diagnostics.Assert(assertion, Describe(expected), Describe(actual));
                break;
        }
    }

    /// <summary>Writes the ARRANGE, ACT and ASSERT lines for an expected exception.</summary>
    /// <param name="assertion">The assertion's name.</param>
    /// <param name="expectedType">The exception type the test expects.</param>
    /// <param name="thrown">The exception the code threw, or <see langword="null" /> when it threw none.</param>
    public static void WriteException(string assertion, Type expectedType, Exception? thrown)
    {
        var diagnostics = Current();
        diagnostics.Arrange("expected exception", expectedType.FullName);
        diagnostics.Act("thrown", thrown is null ? "no exception" : $"{thrown.GetType().FullName}: {thrown.Message}");
        diagnostics.Assert(assertion, expectedType.FullName, thrown?.GetType().FullName ?? "no exception");
    }

    /// <summary>
    /// Writes one message a fake KDC or GSS acceptor exchanged, as an uncounted BYTES line
    /// labelled with what it is: message type, principals, realm, encryption types, key usage.
    /// Does nothing outside a test, so a fake used by a fake stays quiet.
    /// </summary>
    /// <param name="label">What the message is.</param>
    /// <param name="bytes">The message's bytes.</param>
    public static void WriteExchangedMessage(string label, ReadOnlySpan<byte> bytes)
    {
#pragma warning disable MSTESTEXP
        if (TestContext.Current is { } testContext)
#pragma warning restore MSTESTEXP
        {
            TestDiagnostics.For(testContext).Bytes(label, bytes);
        }
    }

    /// <summary>Describes a value on one line: strings quoted, bytes in hex, collections as their first items.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The description.</returns>
    public static string Describe(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        byte[] bytes => $"{bytes.Length} bytes",
        IEnumerable items => DescribeItems(items),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    // TestContext.Current is MSTest's ambient context for the running test; no test in this
    // project takes a TestContext of its own, so it is how an assertion finds its test.
#pragma warning disable MSTESTEXP
    private static TestDiagnostics Current() =>
        TestDiagnostics.For(TestContext.Current ?? throw new InvalidOperationException("An assertion ran outside a test."));
#pragma warning restore MSTESTEXP

    private static void WriteValue(TestDiagnostics diagnostics, bool isExpected, object? value)
    {
        var label = isExpected ? "expected" : "actual";
        if (isExpected)
        {
            diagnostics.Arrange(label, Describe(value));
        }
        else
        {
            diagnostics.Act(label, Describe(value));
        }

        if (value is byte[] bytes)
        {
            diagnostics.Bytes(label, bytes);
        }
    }

    private static string DescribeItems(IEnumerable items)
    {
        var text = new StringBuilder("[");
        var count = 0;
        foreach (var item in items)
        {
            if (count == CollectionItemCap)
            {
                text.Append(", ...");
                break;
            }

            text.Append(count == 0 ? string.Empty : ", ").Append(Describe(item));
            count++;
        }

        return text.Append(']').ToString();
    }
}

/// <summary>MSTest's <c>Assert</c>, writing each assertion's diagnostic lines first.</summary>
internal static class Assert
{
    public static void AreEqual<T>(T? expected, T? actual, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(AreEqual), expected, actual);
        MSTestAssert.AreEqual(expected, actual, message);
    }

    public static void AreSame<T>(T? expected, T? actual, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(AreSame), expected, actual);
        MSTestAssert.AreSame(expected, actual, message);
    }

    public static void IsTrue([DoesNotReturnIf(false)] bool? condition, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(IsTrue), true, condition);
        MSTestAssert.IsTrue(condition, message);
    }

    public static void IsFalse([DoesNotReturnIf(true)] bool? condition, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(IsFalse), false, condition);
        MSTestAssert.IsFalse(condition, message);
    }

    public static void IsNull(object? value, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(IsNull), null, value);
        MSTestAssert.IsNull(value, message);
    }

    public static void IsNotNull([NotNull] object? value, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(IsNotNull), "not null", value);
        MSTestAssert.IsNotNull(value, message);
    }

    public static void IsEmpty(IEnumerable collection, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(IsEmpty), "[]", collection);
        MSTestAssert.IsEmpty(collection, message);
    }

    public static void HasCount(int expected, IEnumerable collection, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(HasCount), expected, collection.Cast<object?>().Count());
        MSTestAssert.HasCount(expected, collection, message);
    }

    public static void IsGreaterThan<T>(T lowerBound, T value, string message = "")
        where T : IComparable<T>
    {
        DiagnosticAssertionLines.WriteComparison(nameof(IsGreaterThan), $"greater than {DiagnosticAssertionLines.Describe(lowerBound)}", value);
        MSTestAssert.IsGreaterThan(lowerBound, value, message);
    }

    public static void IsInstanceOfType<T>([NotNull] object? value, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(IsInstanceOfType), typeof(T).FullName, value?.GetType().FullName);
        MSTestAssert.IsInstanceOfType<T>(value, message);
    }

    public static TException ThrowsExactly<TException>(Action action, string message = "")
        where TException : Exception
    {
        Exception? thrown = null;
        try
        {
            return MSTestAssert.ThrowsExactly<TException>(() => Record(action, ref thrown), message);
        }
        finally
        {
            DiagnosticAssertionLines.WriteException(nameof(ThrowsExactly), typeof(TException), thrown);
        }
    }

    public static TException ThrowsExactly<TException>(Func<object?> action, string message = "")
        where TException : Exception =>
        ThrowsExactly<TException>(() => { action(); }, message);

    public static async Task<TException> ThrowsExactlyAsync<TException>(Func<Task> action, string message = "")
        where TException : Exception
    {
        Exception? thrown = null;
        try
        {
            return await MSTestAssert.ThrowsExactlyAsync<TException>(
                async () =>
                {
                    try
                    {
                        await action();
                    }
                    catch (Exception exception)
                    {
                        thrown = exception;
                        throw;
                    }
                },
                message);
        }
        finally
        {
            DiagnosticAssertionLines.WriteException(nameof(ThrowsExactlyAsync), typeof(TException), thrown);
        }
    }

    private static void Record(Action action, ref Exception? thrown)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            thrown = exception;
            throw;
        }
    }
}

/// <summary>MSTest's <c>CollectionAssert</c>, writing each assertion's diagnostic lines first.</summary>
internal static class CollectionAssert
{
    public static void AreEqual(ICollection? expected, ICollection? actual, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(AreEqual), expected, actual);
        MSTestCollectionAssert.AreEqual(expected, actual, message);
    }

    public static void AreNotEqual(ICollection? notExpected, ICollection? actual, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(AreNotEqual), notExpected, actual);
        MSTestCollectionAssert.AreNotEqual(notExpected, actual, message);
    }
}

/// <summary>MSTest's <c>StringAssert</c>, writing each assertion's diagnostic lines first.</summary>
internal static class StringAssert
{
    public static void Contains(string? value, string? substring, string message = "")
    {
        DiagnosticAssertionLines.WriteComparison(nameof(Contains), $"contains {DiagnosticAssertionLines.Describe(substring)}", value);
        MSTestStringAssert.Contains(value, substring, message);
    }
}
