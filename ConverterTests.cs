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
            //SetupAPI(null, "", "Test", true);
            InitializeAPI(true);
        }

        [TestMethod]
        public void TestTeradyneICT()
        {
            InitializeAPI(true);

            var fileInfo = new FileInfo(@"Examples\ExampleWithPanel.LOG");
            var converter = new TeradyneICT(new TeradyneICT().ConverterParameters);            
            using (FileStream file = fileInfo.Open(FileMode.Open))
            {
                SetConversionSource(fileInfo, new Dictionary<string, string>(), converter.ConverterParameters);
                converter.ImportReport(this, file);
            }
        }
    }
}
