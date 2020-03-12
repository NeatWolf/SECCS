﻿using Moq;
using NUnit.Framework;
using SECCS.Formats;
using SECCS.Interfaces;
using SECCS.Tests.Classes;
using SECCS.Tests.Utils;

namespace SECCS.Tests.Formats
{
    public class SeccsWriteableFormatTest : BaseFormatTest<SeccsWriteableFormat<DummyBuffer>>
    {
        [Test]
        public void CanFormat_NonSeccsWriteable_False()
        {
            Assert.IsFalse(Format.CanFormat(typeof(TestClass1)));
        }

        [Test]
        public void CanFormat_SeccsWriteable_True()
        {
            Assert.IsTrue(Format.CanFormat(typeof(Writeable1)));
        }

        [Test]
        public void Write_Writeable_CallsWritableWrite()
        {
            var writeableMock = new Mock<ISeccsWriteable<DummyBuffer>>();
            writeableMock.Setup(o => o.Write(It.IsAny<DummyBuffer>())).Verifiable();

            Format.Write(writeableMock.Object, new WriteFormatContext<DummyBuffer>(Mock.Of<IBufferWriter<DummyBuffer>>(), new DummyBuffer(), ""));

            writeableMock.Verify();
        }

        private class Writeable1 : ISeccsWriteable<DummyBuffer>
        {
            public void Write(DummyBuffer writer) { }
        }
    }
}
