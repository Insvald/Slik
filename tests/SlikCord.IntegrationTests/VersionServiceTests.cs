using Containerd.Services.Version.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class VersionServiceTests : ServiceTestsBase
    {
        [TestMethod]
        public async Task Version_ReturnsExpectedVersion()
        {
            string pattern = "*-linux-amd64.tar.gz";

            var dirInfo = new DirectoryInfo("..\\..\\..\\..\\..\\src\\SlikCord\\assets");
            var fileInfo = dirInfo.GetFiles(pattern).FirstOrDefault();
            if (fileInfo == null)
                throw new FileNotFoundException($"Containerd archive is missing from 'assets' folder");

            string expectedVersion = "";
            int start = fileInfo.FullName.LastIndexOf("containerd-");
            if (start > 0)
            {
                start += "containerd-".Length;
                int end = fileInfo.FullName.IndexOf("-", start + 1);
                if (end > 0)
                {
                    expectedVersion = $"v{fileInfo.FullName[start..end]}";
                }
            }

            if (string.IsNullOrEmpty(expectedVersion))
                Assert.Fail("Version part is not found in containerd archive");

            var client = new Version.VersionClient(Channel);
            var response = await client.VersionAsync(new Empty(), Headers);
            Assert.IsTrue(response.Version.Equals(expectedVersion));
        }
    }
}
