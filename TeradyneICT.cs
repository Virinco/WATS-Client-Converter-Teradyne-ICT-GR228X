using Virinco.WATS.Interface;
using Virinco.WATS.Integration.TextConverter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace TeradyneConverter
{
    public class TeradyneICT : TextConverterBase
    {

        string currentPartNumber = "";
        string currentRevision = "1";
        string currentSequenceName = "";

        //Main events
        enum Event
        {
            Aborted,
            Pass,
            Fail,
            Error,
            SystemError,
            Cancelled,
            ReturnToDiagnose
        }
        Dictionary<char, Event> mainEvents = new Dictionary<char, Event>()
        {
            {'?', Event.Aborted},
            {'"', Event.Pass},
            {'/', Event.Fail},
            {'*', Event.Error},
            {'&', Event.SystemError},
            {'!', Event.Cancelled},
            {']', Event.ReturnToDiagnose}
        };

        public class PrefixUnit
        {
            public string Symbol { get; set; }
            public double UnitFactor { get; set; }
            public double GetValue(double value) { return value * UnitFactor; }
            public double GetValueInv(double value) { return value / UnitFactor; }
        }

        //PrefixUnit
        Dictionary<string, PrefixUnit> prefixUnit = new Dictionary<string, PrefixUnit>()
        {
            {"P",new PrefixUnit()   {Symbol="p",UnitFactor=0.000000000001}},  //Pico 
            {"N",new PrefixUnit()   {Symbol="n",UnitFactor=0.000000001}},  //Nano
            {"U",new PrefixUnit()   {Symbol="µ",UnitFactor=0.000001}},  //Micro 
            {"M",new PrefixUnit()   {Symbol="m",UnitFactor=0.001}},  //Milli 
            {"",new PrefixUnit()    {Symbol="" ,UnitFactor=1.0}},  //No unit prefix
            {"K",new PrefixUnit()   {Symbol="K",UnitFactor=1000}},  //Kilo
            {"MEG",new PrefixUnit() {Symbol="M",UnitFactor=1000000}} //Mega 
        };

        public double ConvertFromToUnit(double value, PrefixUnit from, PrefixUnit to)
        {
            double realValue = from.GetValue(value);
            return to.GetValueInv(realValue);
        }

        public class TestType
        {
            public string Description { get; set; }
            public string Unit { get; set; }
            //public TestType(string description, string unit) { Description = unit; Unit = unit; }
        }

        Dictionary<string, TestType> testTypes = new Dictionary<string, TestType>()
        {
            {"AC", new TestType(){Description="MEAS DVM ACV",Unit="V"}},
            {"AI", new TestType(){Description="MEAS ACI ACM",Unit=""}},
            {"AR", new TestType(){Description="TEST ARITH",Unit=""}},
            {"AV", new TestType(){Description="MEAS ACV ACM",Unit=""}},
            {"AY", new TestType(){Description="MEAS {DVM | {DMM {VOLTAGE|CURRENT}}}",Unit=""}},
            {"CS", new TestType(){Description="MEAS ACZ CS",Unit=""}},
            {"CP", new TestType(){Description="MEAS ACZ CP",Unit="F"}},
            {"DC", new TestType(){Description="MEAS DVM DCV",Unit="V"}},
            {"DD", new TestType(){Description="MEAS ACZ D",Unit=""}},
            {"EV", new TestType(){Description="FTM EVENT",Unit=""}},
            {"HZ", new TestType(){Description="FTM FREQ",Unit=""}},
            {"I", new TestType(){Description="MEAS DCM DCI",Unit=""}},
            {"IS", new TestType(){Description="TEST DCS DCI",Unit=""}},
            {"LP", new TestType(){Description="MEAS ACZ LP",Unit=""}},
            {"LS", new TestType(){Description="MEAS ACZ LS",Unit=""}},
            {"QQ", new TestType(){Description="MEAS ACZ Q",Unit=""}},
            {"R", new TestType(){Description="MEAS R",Unit="ohm"}},
            {"RA", new TestType(){Description="FTM RATIO",Unit=""}},
            {"RP", new TestType(){Description="MEAS ACZ RP",Unit=""}},
            {"RS", new TestType(){Description="MEAS ACZ RS",Unit=""}},
            {"S", new TestType(){Description="FTM PERIOD",Unit=""}},
            {"TI", new TestType(){Description="FTM INTERNAL",Unit=""}},
            {"V", new TestType(){Description="MEAS DCM DCV",Unit=""}},
            {"VS", new TestType(){Description="TEST DCS DCV",Unit=""}},
            {"XS", new TestType(){Description="MEAS ACZ XS",Unit=""}},
            {"XP", new TestType(){Description="MEAS ACZ XP",Unit=""}},
            {"ZM", new TestType(){Description="MEAS ACZ Z",Unit=""}},
        };

        Dictionary<string, string> genFailures = new Dictionary<string, string>()
        {
            {"(S","SHORTS test, failed"},
            {"(O","OPENS test, failed"},
            {"(B","BUSTEST, failure caused by bus"},
            {"(C","SCRATCHPROBING connection failure"},
            {"(F","CONTACT fixture failure"}
        };

        Dictionary<char, int> compRefGroupConters = null;

        void FixSequence(string compRef)
        {
            char compRefType = compRef[0]; //Use 1 char to group
            if (!compRefGroupConters.ContainsKey(compRefType)) //Make sure a counter exist
                compRefGroupConters.Add(compRefType, 0);
            if (currentSequence.Name[0] != compRefType)
            {
                compRefGroupConters[compRefType]++;
                currentSequence = currentUUT.GetRootSequenceCall().AddSequenceCall(String.Format("{0}-Group{1}", compRefType, compRefGroupConters[compRefType]));
            }
        }
        int reportCount = 0;
        protected override bool ProcessMatchedLine(TextConverterBase.SearchFields.SearchMatch match, ref TextConverterBase.ReportReadState readState)
        {
            if (match == null) return true;
            switch (match.matchField.fieldName)
            {
                case "StartProgram":
                    currentPartNumber = (string)match.GetSubField("PartNumber");
                    currentSequenceName = match.completeLine.Substring(0, match.completeLine.IndexOf('['));
                    break;
                case "StartTest":
                    compRefGroupConters = new Dictionary<char, int>();
                    currentUUT.PartNumber = currentPartNumber;
                    currentUUT.PartRevisionNumber = currentRevision;
                    currentUUT.SequenceName = currentSequenceName;
                    if (!String.IsNullOrEmpty(converterArguments["stationName"])) currentUUT.StationName = converterArguments["stationName"]; //Use StationName in converter.xml if specified
                    break;
                case "MainEvent":
                    Event ev = mainEvents[(char)match.GetSubField("Event")];
                    switch (ev)
                    {
                        case Event.Aborted:
                        case Event.Cancelled:
                            currentUUT.Status = UUTStatusType.Terminated;
                            break;
                        case Event.Pass:
                            currentUUT.Status = UUTStatusType.Passed;
                            break;
                        case Event.Fail:
                            currentUUT.Status = UUTStatusType.Failed;
                            currentUUT.GetRootSequenceCall().Status = StepStatusType.Failed;
                            break;
                        case Event.SystemError:
                        case Event.Error:
                            currentUUT.Status = UUTStatusType.Error;
                            break;
                        case Event.ReturnToDiagnose:
                            //Ignore
                            break;
                        default:
                            break;
                    }
                    reportCount++;
                    TimeSpan elapsed = (DateTime)match.GetSubField("DateTime") - currentUUT.StartDateTime;
                    currentUUT.ExecutionTime = elapsed.TotalMilliseconds / 1000.0;
                    logStream.WriteLine("{0}: Submitting UUT #{1} (SN={2})",
                        DateTime.Now.ToString("dd.MM.yy HH:mm:ss.ff"), reportCount, currentUUT.SerialNumber);
                    try
                    {
                        if (string.IsNullOrEmpty(currentUUT.PartNumber))
                        {
                            //If partnumber not specified in the file, try using file name up to .
                            Regex ex = new Regex(@"^(?<PartNumber>[^.]*)");
                            Match m = ex.Match(apiRef.ConversionSource.SourceFile.Name);
                            if (m.Groups.Count > 0)
                                currentUUT.PartNumber = m.Groups[0].Value;
                            else
                                currentUUT.PartNumber = apiRef.ConversionSource.SourceFile.Name; //Use whole partno
                        }

                        //Skip UUT if it doesn't have any steps and staus == terminated 
                        if( !(currentUUT.Status == UUTStatusType.Terminated && currentStep.StepOrderNumber <= 1) )
                            apiRef.Submit(SubmitMethod.Offline, currentUUT); //TODO: OffLine
                    }
                    catch (Exception ex)
                    {
                        ParseError(String.Format("Submitting UUT in ProcessMatchedLine: {0}", ex.Message + (ex.InnerException == null ? "" : "\r\nInner: " + ex.InnerException.Message)), currentUUT.SerialNumber);
                        throw;
                    }
                    CreateDefaultUUT();
                    if (!String.IsNullOrEmpty(converterArguments["stationName"])) currentUUT.StationName = converterArguments["stationName"]; //Use StationName in converter.xml if specified
                    break;
                case "FailuresGen":
                    currentStep = currentSequence.AddStringValueStep(genFailures[(string)match.GetSubField("Key")]);
                    currentStep.Status = StepStatusType.Failed;
                    currentStep.Parent.Status = StepStatusType.Failed;
                    currentStep.ReportText = (string)match.GetSubField("Info");
                    currentUUT.GetRootSequenceCall().Status = StepStatusType.Failed;
                    break;
                case "Measure":
                    string compRef = (string)match.GetSubField("CompRef");
                    FixSequence(compRef);
                    string result = (string)match.GetSubField("Result");
                    string meas = (string)match.GetSubField("meas");
                    PrefixUnit measUnit = prefixUnit[(string)match.GetSubField("measU")];
                    string lowLimit = (string)match.GetSubField("LowLim");
                    PrefixUnit lowLimitUnit = prefixUnit[(string)match.GetSubField("LowLimU")];
                    string highLimit = (string)match.GetSubField("HighLim");
                    PrefixUnit highLimitUnit = prefixUnit[(string)match.GetSubField("HighLimU")];
                    string testType = (string)match.GetSubField("Type");
                    string stepName = testType == "" ? compRef : string.Format("{0}({1})", compRef, testType);
                    if (meas != "")
                    {
                        currentStep = currentSequence.AddNumericLimitStep(stepName);
                        double measDouble = (double)ConvertStringToAny(meas, typeof(double), null, currentCulture);
                        double lowLimitDouble = ConvertFromToUnit((double)ConvertStringToAny(lowLimit, typeof(double), null, currentCulture), lowLimitUnit, measUnit);
                        double highLimitDouble = ConvertFromToUnit((double)ConvertStringToAny(highLimit, typeof(double), null, currentCulture), highLimitUnit, measUnit);
                        string units = String.Format("{0}({1})", measUnit.Symbol, testType);
                        if (lowLimit == "" && highLimit == "")
                            ((NumericLimitStep)currentStep).AddTest(measDouble, units);
                        else if (lowLimit != "" && highLimit == "")
                            currentNumLimTest = ((NumericLimitStep)currentStep).AddTest(measDouble, CompOperatorType.GE, lowLimitDouble, units);
                        else if (highLimit != "" && lowLimit == "")
                            currentNumLimTest = ((NumericLimitStep)currentStep).AddTest(measDouble, CompOperatorType.LE, highLimitDouble, units);
                        else
                            currentNumLimTest = ((NumericLimitStep)currentStep).AddTest(measDouble, CompOperatorType.GELE, lowLimitDouble, highLimitDouble, units);
                    }
                    else //No Measure
                    {
                        if (testType != "IS" && testType != "VS") //Filter these measures
                        {
                            currentStep = currentSequence.AddPassFailStep(stepName);
                            ((PassFailStep)currentStep).AddTest(result == "=");
                        }
                        else break;
                    }
                    if (result != "=" && result != "#")
                    {
                        currentStep.Status = StepStatusType.Failed;
                        currentStep.Parent.Status = StepStatusType.Failed;
                        currentUUT.GetRootSequenceCall().Status = StepStatusType.Failed;
                        if (!string.IsNullOrEmpty(testType)) currentStep.StepErrorMessage = testTypes[testType].Description;
                    }
                    if (result == "#")
                        currentStep.Status = StepStatusType.Done;
                    if ((string)match.GetSubField("Message") != "")
                        currentStep.ReportText = (string)match.GetSubField("Message");
                    break;
                default:
                    break;
            }
            return true;
        }

        public TeradyneICT() :
            base() { }

        public TeradyneICT(IDictionary<string, string> args)
            : base(args)
        {
            currentCulture = new CultureInfo("en-US"); //Use english culture info
            //F:\BOARDS\ABB_DA\43420_BIO2_PROGRAM\43420_BIO2.obc[31-JUL-12  14:17:35
            const string regStartProgram = @"^*.:.*\x5C(?<PartNumber>.*)_.*[.](?i:OBC)\x5B(?<DateTime>[1-9-A-Z]+ +[0-9:]+)";
            SearchFields.RegExpSearchField fmt = searchFields.AddRegExpField("StartProgram", ReportReadState.InHeader, regStartProgram, "", typeof(Match));
            fmt.AddSubField("PartNumber", typeof(string), null, UUTField.UserDefined); //TODO: Missing revision
            fmt.AddSubField("DateTime", typeof(DateTime), "dd-MMM-yy  HH:mm:ss"); //Ignore

            //@31-JUL-12  14:18:51 SN MP3774501MRS050643E
            const string regStartTest = @"^@(?<DateTime>[1-9-A-Z]+ +[0-9:]+) SN (?<SerialNumber>.+)";
            fmt = searchFields.AddRegExpField(UUTField.UseSubFields, ReportReadState.InHeader, regStartTest, "", typeof(Match), ReportReadState.InTest);
            fmt.fieldName = "StartTest";
            fmt.AddSubField("DateTime", typeof(DateTime), "dd-MMM-yy  HH:mm:ss", UUTField.StartDateTime);
            fmt.AddSubField("SerialNumber", typeof(string), null, UUTField.SerialNumber);

            //?31-JUL-12  14:18:59
            //"25-AUG-12  09:05:10
            ///25-AUG-12  09:12:01
            //&25-AUG-12  14:11:33
            //!25-AUG-12  08:56:29
            //]25-AUG-12  08:59:07
            const string regMainEvent = @"^(?<Event>[?""/&!\x5D])(?<DateTime>[1-9-A-Z]+ +[0-9:]+)";
            fmt = searchFields.AddRegExpField("MainEvent", ReportReadState.InTest, regMainEvent, "", typeof(Match), ReportReadState.InHeader);
            fmt.AddSubField("Event", typeof(char));
            fmt.AddSubField("DateTime", typeof(DateTime), "dd-MMM-yy  HH:mm:ss");

            //(S SHORTS test, failed
            //(O OPENS test, failed
            //(B BUSTEST, failure caused by bus
            //(C SCRATCHPROBING connection failure
            //(F CONTACT fixture failure
            const string regFailuresGen = @"^(?<Key>\x28S|\x28O|\x28B|\x28C|\x28F) *(?<Info>.*)";
            fmt = searchFields.AddRegExpField("FailuresGen", ReportReadState.InTest, regFailuresGen, "", typeof(Match));
            fmt.AddSubField("Key", typeof(string));
            fmt.AddSubField("Info", typeof(string));

            //K500_RLY1_NO=62.532611M(-500M,500M)V 
            //K500_CLEAR#(,)VS
            //V404=6.96336(0,20)RP
            const string regMeasure = @"^(?<CompRef>\w+?)(?<Result>[=<>#%])(?<meas>[0-9.E+-]*)(?<measU>(?:MEG*)|(?:[NPUMK]*))\x28*(?<LowLim>[0-9.E+-]*)(?<LowLimU>(?:MEG*)|(?:[NPUMK]*)),*(?<HighLim>[0-9.E+-]*)(?<HighLimU>(?:MEG*)|(?:[NPUMK]*))\x29*(?<Type>\w*) *=*(?<Message>.*)";
            fmt = searchFields.AddRegExpField("Measure", ReportReadState.InTest, regMeasure, null, typeof(Match));
            fmt.AddSubField("CompRef", typeof(string));
            fmt.AddSubField("Result", typeof(string));
            fmt.AddSubField("meas", typeof(string));
            fmt.AddSubField("measU", typeof(string));
            fmt.AddSubField("LowLim", typeof(string));
            fmt.AddSubField("LowLimU", typeof(string));
            fmt.AddSubField("HighLim", typeof(string));
            fmt.AddSubField("HighLimU", typeof(string));
            fmt.AddSubField("Type", typeof(string));
            fmt.AddSubField("Message", typeof(string));
        }


    }
}
