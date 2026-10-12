using NUnit.Framework;

namespace WardenZero.Tests
{
    public class MemoryProbeTests
    {
        [Test]
        public void Mem_ShowsTheHeap_OnlyAsItsOwnParameter()
        {
            Assert.IsTrue(MemoryProbe.ShowHeap("https://x.dev/index.html?mem"));
            Assert.IsTrue(MemoryProbe.ShowHeap("https://x.dev/?campaign=jungle&mem"));
            Assert.IsTrue(MemoryProbe.ShowHeap("https://x.dev/?mem&campaign=jungle"));
            Assert.IsFalse(MemoryProbe.ShowHeap("https://x.dev/?memprobe=12"));
            Assert.IsFalse(MemoryProbe.ShowHeap("https://x.dev/?memo"));
            Assert.IsFalse(MemoryProbe.ShowHeap(null));
        }

        [Test]
        public void MemProbe_ReadsMegabytes()
        {
            Assert.AreEqual(12, MemoryProbe.ProbeMegabytes("https://x.dev/?campaign=jungle&memprobe=12"));
            Assert.AreEqual(40, MemoryProbe.ProbeMegabytes("https://x.dev/?memprobe=40&mem"));
            Assert.AreEqual(0, MemoryProbe.ProbeMegabytes("https://x.dev/?mem"));
            Assert.AreEqual(0, MemoryProbe.ProbeMegabytes("https://x.dev/?memprobe=abc"));
            Assert.AreEqual(1024, MemoryProbe.ProbeMegabytes("https://x.dev/?memprobe=99999"));
            Assert.AreEqual(0, MemoryProbe.ProbeMegabytes(null));
        }
    }
}
