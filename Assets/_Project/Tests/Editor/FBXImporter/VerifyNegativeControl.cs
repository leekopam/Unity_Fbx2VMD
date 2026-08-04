// TEMP negative-control test — deliberately fails to prove merge blocking. Delete after verification.
using NUnit.Framework;

public class VerifyNegativeControl
{
    [Test]
    public void Deliberate_Failure_For_Control_Test()
    {
        Assert.Fail("deliberate failure for branch protection negative control");
    }
}
