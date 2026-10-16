using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options.Behaviour;

[TestClass]
public sealed class EasyOptionSettersBehaviourTests
{
    private static readonly Func<EasyBehaviourOptions, bool>[] Flags =
    [
        static b => b.Verbose,
        static b => b.IncludeHeader,
        static b => b.NoProgress,
        static b => b.NoSignal,
        static b => b.WildcardMatch,
        static b => b.FailOnError,
        static b => b.KeepSendingOnError,
        static b => b.QuickExit,
    ];

    private static readonly CurlOption[] FlagOptions =
    [
        CurlOption.Verbose, CurlOption.Header, CurlOption.Noprogress, CurlOption.Nosignal,
        CurlOption.Wildcardmatch, CurlOption.Failonerror, CurlOption.KeepSendingOnError, CurlOption.QuickExit,
    ];

    [TestMethod]
    public void SetOption_EachOnOffOption_SetsItsFieldOnForNonZeroAndOffForZero()
    {
        for (var i = 0; i < FlagOptions.Length; i++)
        {
            var options = new EasyOptions();

            Assert.AreEqual(CurlCode.Ok, Set(ref options, FlagOptions[i], 2L), FlagOptions[i].ToString());
            Assert.IsTrue(Flags[i](options.Behaviour), FlagOptions[i].ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, FlagOptions[i], 0), FlagOptions[i].ToString());
            Assert.IsFalse(Flags[i](options.Behaviour), FlagOptions[i].ToString());
        }
    }

    [TestMethod]
    public void SetOption_EachOnOffOptionGivenAString_ReturnsBadFunctionArgument()
    {
        foreach (var option in FlagOptions)
        {
            AssertBadFunctionArgument(option, "1");
        }
    }

    [TestMethod]
    public void SetOption_Verbose_LeavesTheOtherBehaviourFieldsAlone()
    {
        var options = new EasyOptions();

        Set(ref options, CurlOption.Verbose, 1L);

        Assert.AreEqual(new EasyBehaviourOptions { Verbose = true }, options.Behaviour);
    }

    [TestMethod]
    public void SetOption_Errorbuffer_SetsTheBufferAndNullClearsIt()
    {
        var options = new EasyOptions();
        var buffer = new StringBuilder();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Errorbuffer, buffer));
        Assert.AreSame(buffer, options.Behaviour.ErrorBuffer);
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Errorbuffer, null));
        Assert.IsNull(options.Behaviour.ErrorBuffer);
    }

    [TestMethod]
    public void SetOption_ErrorbufferGivenBytes_ReturnsBadFunctionArgument()
    {
        AssertBadFunctionArgument(CurlOption.Errorbuffer, new byte[256]);
    }

    [TestMethod]
    public void SetOption_Stderr_SetsTheStreamAndNullClearsIt()
    {
        var options = new EasyOptions();
        using var stream = new MemoryStream();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Stderr, stream));
        Assert.AreSame(stream, options.Behaviour.StandardError);
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Stderr, null));
        Assert.IsNull(options.Behaviour.StandardError);
    }

    [TestMethod]
    public void SetOption_StderrGivenAString_ReturnsBadFunctionArgument()
    {
        AssertBadFunctionArgument(CurlOption.Stderr, "stderr");
    }

    [TestMethod]
    public void SetOption_Share_SetsTheShareHandle()
    {
        var options = new EasyOptions();
        var share = new FakeShareHandle();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Share, share));
        Assert.AreSame(share, options.Behaviour.Share);
    }

    [TestMethod]
    public void SetOption_ShareGivenAnObjectThatIsNotAShareHandle_ReturnsBadFunctionArgument()
    {
        AssertBadFunctionArgument(CurlOption.Share, new object());
    }

    [TestMethod]
    public void SetOption_Private_KeepsAnyValueAndNull()
    {
        var options = new EasyOptions();
        var value = new object();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Private, value));
        Assert.AreSame(value, options.Behaviour.Private);
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Private, null));
        Assert.IsNull(options.Behaviour.Private);
    }

    [TestMethod]
    public void SetOption_PrivateGivenADelegate_ReturnsBadFunctionArgument()
    {
        AssertBadFunctionArgument(CurlOption.Private, new Func<int>(() => 1));
    }

    [TestMethod]
    [DataRow(CurlOption.NewFilePerms, 0x180L, DisplayName = "NEW_FILE_PERMS 0600")]
    [DataRow(CurlOption.NewFilePerms, 0L, DisplayName = "NEW_FILE_PERMS 0")]
    [DataRow(CurlOption.NewDirectoryPerms, 0x1FFL, DisplayName = "NEW_DIRECTORY_PERMS 0777")]
    public void SetOption_APermissionInRange_SetsItsField(CurlOption option, long mode)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, mode));

        var set = option == CurlOption.NewFilePerms
            ? options.Behaviour.NewFilePermissions
            : options.Behaviour.NewDirectoryPermissions;
        Assert.AreEqual((UnixFileMode)mode, set);
    }

    [TestMethod]
    [DataRow(CurlOption.NewFilePerms, -1L, DisplayName = "NEW_FILE_PERMS -1")]
    [DataRow(CurlOption.NewFilePerms, 0x200L, DisplayName = "NEW_FILE_PERMS 01000")]
    [DataRow(CurlOption.NewDirectoryPerms, -1L, DisplayName = "NEW_DIRECTORY_PERMS -1")]
    [DataRow(CurlOption.NewDirectoryPerms, 0x200L, DisplayName = "NEW_DIRECTORY_PERMS 01000")]
    [DataRow(CurlOption.NewFilePerms, "0644", DisplayName = "NEW_FILE_PERMS given a string")]
    public void SetOption_APermissionOutOfRangeOrOfTheWrongType_ReturnsBadFunctionArgument(CurlOption option, object value)
    {
        AssertBadFunctionArgument(option, value);
    }

    private static CurlCode Set(ref EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);

    private static void AssertBadFunctionArgument(CurlOption option, object? value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, value), option.ToString());
        Assert.AreSame(before, options, option.ToString());
    }

    private sealed class FakeShareHandle : IShareHandle
    {
        public CurlShareCode Share(CurlLockData data) => CurlShareCode.Ok;

        public CurlShareCode Unshare(CurlLockData data) => CurlShareCode.Ok;

        public CurlShareCode SetLockCallback(ShareLockCallback? callback) => CurlShareCode.Ok;

        public CurlShareCode SetUnlockCallback(ShareUnlockCallback? callback) => CurlShareCode.Ok;

        public CurlShareCode Cleanup() => CurlShareCode.Ok;
    }
}
