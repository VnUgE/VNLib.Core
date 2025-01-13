using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace VNLib.Utils.Tests
{
    [TestClass()]
    public class BitFieldTests
    {
        [TestMethod()]
        public void BasicFuntionalityTest()
        {
            BitField bf = new(0);

            Assert.AreEqual(bf.Value, 0ul);

            bf.Set(1);
            Assert.IsTrue(bf.IsSet(1));
            Assert.AreEqual(1ul, bf.Value);

            bf.Set(1 << 1);
            Assert.IsTrue(bf.IsSet(1 << 1));
            Assert.AreEqual(3ul, bf.Value);

            bf.Set(4);
            Assert.IsTrue(bf.IsSet(4));
            Assert.AreEqual(7ul, bf.Value);

            bf.Clear(0x02);
            Assert.AreEqual(5ul, bf.Value);

            bf.ClearAll();
            Assert.IsFalse(bf.IsSet(1));
            Assert.AreEqual(0ul, bf.Value);

            bf.Set(1u << 63);
            Assert.IsTrue(bf.IsSet(1u << 63));
            Assert.IsFalse(bf.IsSet(1u << 62));
            Assert.AreEqual(1u << 63, bf.Value);
        }
    }
}