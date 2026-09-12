using System.Net;
using billpg.SpartanHttpClient;

namespace billpg.SpartanHttpClientTests;

/// <summary>SpartanResponse's public With... methods exist so test doubles - a fake
/// SpartanRequestRunner especially - can build one by hand, per the pattern documented
/// in the README's "Testing code that uses SpartanRequest" section.</summary>
[TestClass]
public class SpartanResponseTests
{
    [TestMethod]
    public void DefaultResponse_HasNoStatusOrBody()
    {
        var response = new SpartanResponse();
        Assert.AreEqual(0, response.StatusCode);
        Assert.AreEqual("", response.Body);
        Assert.IsEmpty(response.Headers);
        Assert.IsNull(response.RemoteAddress);
        Assert.IsNull(response.RemoteCertificateHash);
    }

    [TestMethod]
    public void WithStatusCode_SetsStatusCode()
    {
        var response = new SpartanResponse().WithStatusCode(200);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public void WithBody_SetsBody()
    {
        var response = new SpartanResponse().WithBody("Rutabaga Farms Inc");
        Assert.AreEqual("Rutabaga Farms Inc", response.Body);
    }

    [TestMethod]
    public void WithHeader_SetsHeaderValue()
    {
        var response = new SpartanResponse().WithHeader("X-Farm", "Rutabaga Farms Inc");
        Assert.AreEqual("Rutabaga Farms Inc", response.Headers["X-Farm"]);
    }

    [TestMethod]
    public void WithHeader_NullValue_SkipsHeader()
    {
        var response = new SpartanResponse().WithHeader("X-Vegetable", null);
        Assert.IsEmpty(response.Headers);
    }

    [TestMethod]
    public void WithRemoteAddress_SetsRemoteAddress()
    {
        var response = new SpartanResponse().WithRemoteAddress(IPAddress.Loopback);
        Assert.AreEqual(IPAddress.Loopback, response.RemoteAddress);
    }

    [TestMethod]
    public void WithRemoteCertificateHash_SetsHash()
    {
        var response = new SpartanResponse().WithRemoteCertificateHash("Rutabaga-hash");
        Assert.AreEqual("Rutabaga-hash", response.RemoteCertificateHash);
    }

    [TestMethod]
    public void With_ReturnsNewInstance_OriginalUnchanged()
    {
        var original = new SpartanResponse();
        var withStatus = original.WithStatusCode(200);

        Assert.AreEqual(0, original.StatusCode);
        Assert.AreEqual(200, withStatus.StatusCode);
    }
}
