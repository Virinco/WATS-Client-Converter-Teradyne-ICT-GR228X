using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Text;
using Virinco.WATS.Interface;
using System.IO;

namespace TeradyneConverter
{
    [TestClass]
    public class ConverterTests : TDM
    {
        [TestMethod]
        public void SetupClient()
        {
            SetupAPI(null, "", "Test", true);
            RegisterClient("your wats", "username", "password");
            InitializeAPI(true);
        }

        [TestMethod]
        public void TestTeradyneICT()
        {
            InitializeAPI(true);

            var fileInfo = new FileInfo(@"Examples\43420_BIO2_Extended.LOG");
            SetConversionSource(fileInfo, new Dictionary<string, string>(), new Dictionary<string, string>());

            var converter = new TeradyneICT(new Dictionary<string, string>());
            using (FileStream file = fileInfo.Open(FileMode.Open))
            {
                converter.ImportReport(this, file);
            }
        }
    }
}
