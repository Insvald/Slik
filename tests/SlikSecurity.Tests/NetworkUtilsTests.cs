using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Slik.Security.Tests
{
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class NetworkUtilsTests
    {
        [TestMethod]
        public void GetLocalMachineName_ReturnsNonEmptyName()
        {
            string machineName = NetworkUtils.GetLocalMachineName();
            Assert.IsFalse(string.IsNullOrWhiteSpace(machineName));
        }

        [TestMethod]
        public void GetLocalIPAddresses_ReturnsNonEmptyList()
        {
            var addresses = NetworkUtils.GetLocalIPAddresses();
            Assert.IsTrue(addresses.Any());
        }
    }
}
