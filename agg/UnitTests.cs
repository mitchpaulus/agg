using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace agg
{
    [TestFixture]
    public class UnitTests
    {
        [Test]
        public void MissingDataTest()
        {
            List<(DateTime time, double value)> values = new List<(DateTime time, double value)>
            {
                (new DateTime(2019,1,1), 1),
                (new DateTime(2019,1,3), 1),
            };


        }

    }
}
