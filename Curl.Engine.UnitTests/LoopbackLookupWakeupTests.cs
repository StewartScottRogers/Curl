namespace Curl.Engine;

[TestClass]
public sealed class LoopbackLookupWakeupTests
{
    [TestMethod]
    public void Signal_AfterDispose_DoesNothingAndThrowsNothing()
    {
        LoopbackLookupWakeup wakeup = LoopbackLookupWakeup.Open();
        wakeup.Dispose();

        wakeup.Signal();
        wakeup.Dispose();
    }
}
