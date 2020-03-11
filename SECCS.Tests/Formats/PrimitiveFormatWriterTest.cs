﻿using Moq;
using NUnit.Framework;
using SECCS.Formats.Write;
using SECCS.Tests.Classes;
using SECCS.Tests.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SECCS.Tests.Formats
{
    public class PrimitiveFormatWriterTest : BaseFormatTest<PrimitiveFormatWriter<DummyBuffer>>
    {
        [Test]
        public void CanFormat_NonPrimitive_False()
        {
            Assert.IsFalse(Format.CanFormat(typeof(TestClass1)));
        }

        [Test]
        public void Write_Int32_WritesToDummy()
        {
            const int data = 123;

            var dummyBufferMock = new Mock<DummyBuffer>();
            dummyBufferMock.Setup(o => o.Write(data)).Verifiable();

            Format.Write(data, new WriteFormatContext<DummyBuffer>(Mock.Of<IBufferWriter<DummyBuffer>>(), dummyBufferMock.Object, ""));

            dummyBufferMock.Verify();
        }
    }
}
