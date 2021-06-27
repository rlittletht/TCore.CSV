using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using TCore.Util;

namespace TCore.CSV
{
	public class CsvFile
	{
		private TextReader m_tr;
		private StreamReader m_stmr;

		private Csv m_csv;

		public CsvFile()
		{
		}

		public CsvFile(string sFilename)
		{
			m_stmr = new StreamReader(sFilename);
			m_tr = m_stmr;
			m_csv = new Csv();
		}

		public void Close()
		{
			m_stmr.Close();
			m_tr = null;
			m_stmr = null;
		}

		/*----------------------------------------------------------------------------
			%%Function: ReadHeadingLine
			%%Qualified: TCore.CSV.CsvFile.ReadHeadingLine
			%%Contact: rlittle
			
		    Read the first line from the file and treat that as headings. this just
		    assumes that the first row *is* a heading row.

		    FUTURE: Maybe write something that tries to determine if there's a heading
		    (maybe give some of the fields one woudl expect in the header?)

		    This will leave the file seeked to the line immediately after the
		    (presumed) header
		----------------------------------------------------------------------------*/
		public SR ReadHeadingLine()
		{
			m_stmr.BaseStream.Seek(0, SeekOrigin.Begin);
			m_stmr.DiscardBufferedData();

			String s = m_tr.ReadLine();
			return m_csv.ReadHeaderFromString(s);
		}

		private string m_sCurrentLine;
		private string[] m_rgsCurrentLine;

		/*----------------------------------------------------------------------------
			%%Function: ReadNextCsvLine
			%%Qualified: TCore.CSV.CsvFile.ReadNextCsvLine
			%%Contact: rlittle
			
		    Reads the next line from the csv file. If we're at the end of the buffer
		    then return false.

		    If we aren't at the end of the file, then the read in line becomes the
		    current line. (fetch values from the line by using GetValue with either
		    an ordinal or a field name
		----------------------------------------------------------------------------*/
		public bool ReadNextCsvLine()
		{
			m_sCurrentLine = m_tr.ReadLine();

			if (m_sCurrentLine == null)
				return false;

			m_rgsCurrentLine = Csv.LineToArray(m_sCurrentLine);

			return true;
		}

		/*----------------------------------------------------------------------------
			%%Function: GetValue
			%%Qualified: TCore.CSV.CsvFile.GetValue
			%%Contact: rlittle
			
		----------------------------------------------------------------------------*/
		public string GetValue(int iCol)
		{
			return m_rgsCurrentLine[iCol];
		}

		/*----------------------------------------------------------------------------
			%%Function: GetValue
			%%Qualified: TCore.CSV.CsvFile.GetValue
			%%Contact: rlittle
			
		----------------------------------------------------------------------------*/
		public string GetValue(string sHeading)
		{
			return m_csv.GetStringVal(m_rgsCurrentLine, sHeading);
		}

		/*----------------------------------------------------------------------------
			%%Function: LookupColumn
			%%Qualified: TCore.CSV.CsvFile.LookupColumn
		----------------------------------------------------------------------------*/
		public int LookupColumn(string sHeading)
		{
			return m_csv.LookupColumn(sHeading);
		}

		static string SetupTestFile(string[] rgsLines)
		{
			String sTestFile = Filename.SBuildTempFilename("__unittest", "");

			StreamWriter sw = new StreamWriter(sTestFile);
			foreach (String s in rgsLines)
			{
				sw.WriteLine(s);
			}

			sw.Flush();
			sw.Close();

			return sTestFile;
		}

		[TestCase(new string[] {"foo,bar", "foo1,bar1"}, "\"foo\",\"bar\"")]
		[TestCase(new string[] {"\"foo\",bar", "foo1,bar1"}, "\"foo\",\"bar\"")]
		[TestCase(new string[] {"foo,\"bar\"", "foo1,bar1"}, "\"foo\",\"bar\"")]
		[TestCase(new string[] {"\"foo\",\"bar\"", "foo1,bar1"}, "\"foo\",\"bar\"")]
		[TestCase(new string[] {"\"foo,bar\",baz", "foo1,bar1"}, "\"foo,bar\",\"baz\"")]
		[Test]
		public static void TestReadHeadingLine(string[] rgsTestFile, string sExpectedHeader)
		{
			string sTestFile = SetupTestFile(rgsTestFile);

			CsvFile csvf = new CsvFile(sTestFile);
			csvf.ReadHeadingLine();

			Assert.AreEqual(sExpectedHeader, csvf.m_csv.Header());
			csvf.Close();
			File.Delete(sTestFile);
		}

		[TestCase(new string[] {"\"foo,bar\",baz", "foo1,bar1"}, "\"foo,bar\",\"baz\"")]
		[Test]
		public static void TestReadHeadingLineSeekBack(string[] rgsTestFile, string sExpectedHeader)
		{
			string sTestFile = SetupTestFile(rgsTestFile);

			CsvFile csvf = new CsvFile(sTestFile);
			string sTemp = csvf.m_tr.ReadLine();

			csvf.ReadHeadingLine();

			Assert.AreEqual(sExpectedHeader, csvf.m_csv.Header());
			csvf.Close();
			File.Delete(sTestFile);
		}

		[TestCase(new string[] {"\"foo,bar\",baz", "foo1,bar1"}, new string[] {"\"foo1\",\"bar1\""})]
		[TestCase(new string[] {"\"foo,bar\",baz", "foo1,bar1", "foo2,bar2"},
			new string[] {"\"foo1\",\"bar1\"", "\"foo2\",\"bar2\""})]
		[TestCase(new string[] {"\"foo,bar\",baz", "foo1,bar1", "\"foo2\",bar2"},
			new string[] {"\"foo1\",\"bar1\"", "\"foo2\",\"bar2\""})]
		[TestCase(new string[] {"\"foo,bar\",baz", "foo1,bar1", "\"foo2\",\"bar2\""},
			new string[] {"\"foo1\",\"bar1\"", "\"foo2\",\"bar2\""})]
		[TestCase(new string[] {"\"foo,bar\",baz", "foo1,bar1", "\"foo2,bar2\",baz1"},
			new string[] {"\"foo1\",\"bar1\"", "\"foo2,bar2\",\"baz1\""})]
		[Test]
		public static void TestReadCsvLines(string[] rgsTestFile, string[] rgcsvExpectedLines)
		{
			string sTestFile = SetupTestFile(rgsTestFile);

			CsvFile csvf = new CsvFile(sTestFile);

			csvf.ReadHeadingLine();
			foreach (string sCsvExpected in rgcsvExpectedLines)
			{
				Assert.AreEqual(true, csvf.ReadNextCsvLine());

				string sCsvActual = Csv.CsvFromRgs(csvf.m_rgsCurrentLine);

				Assert.AreEqual(sCsvExpected, sCsvActual);
			}

			Assert.AreEqual(false, csvf.ReadNextCsvLine());

			csvf.Close();
			File.Delete(sTestFile);
		}

	}

	public class Csv
	{
		protected static string CsvHeader;
		protected string[] m_rgsStaticHeader;


		protected List<string> m_plsHeadings = null;
		protected List<string> m_plsHeadingsMatch = null; // a version with each string all in caps (for matching)

		public Csv()
		{
			m_plsHeadings = new List<string>();
			m_plsHeadingsMatch = new List<string>();
		}

		public void SetStaticHeader(string[] rgsStaticHeader)
		{
			m_rgsStaticHeader = rgsStaticHeader;

			foreach (string s in m_rgsStaticHeader)
			{
				m_plsHeadings.Add(s);
				m_plsHeadingsMatch.Add(s.ToUpper());
			}
		}

		public static string[] LineToArray(string line)
		{
			String pattern = ",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))";
			Regex r = new Regex(pattern);

			string[] rgs = r.Split(line);

			for (int i = 0; i < rgs.Length; i++)
			{
				if (rgs[i].Length > 0 && rgs[i][0] == '"')
					rgs[i] = rgs[i].Substring(1, rgs[i].Length - 2);
			}

			return rgs;
		}

		[Test]
		[TestCase("1,2", new[] {"1", "2"})]
		[TestCase("1,\"2,3\"", new[] {"1", "2,3"})]
		[TestCase("1,\"2,3\",4", new[] {"1", "2,3", "4"})]
		[TestCase("\"1,2,3,4\"", new[] {"1,2,3,4"})]
		public static void TestLineToArray(string sLine, string[] rgsExpected)
		{
			string[] rgsActual = LineToArray(sLine);
			Assert.AreEqual(rgsExpected, rgsActual);
		}

		protected Dictionary<int, string> m_mpColHeader;
		protected Dictionary<string, int> m_mpHeaderCol;

		/* R E A D  H E A D E R  F R O M  S T R I N G */
		/*----------------------------------------------------------------------------
			%%Function: ReadHeaderFromString
			%%Qualified: tw.twsvc:TwUser:Csv.ReadHeaderFromString
			%%Contact: rlittle
			 
			m_plsHeadings has the current set of headings that the database supports
			 
			read in the heading line from the CSV file and validate what it wants
			to upload
		----------------------------------------------------------------------------*/
		public SR ReadHeaderFromString(string sLine)
		{
			bool fDynamicHeader = false;

			if (m_plsHeadings == null || m_plsHeadings.Count == 0)
			{
				// we're going to build this on the fly
				m_plsHeadings = new List<string>();
				m_plsHeadingsMatch = new List<string>();
				fDynamicHeader = true;
			}

			string[] rgs = LineToArray(sLine);
			m_mpColHeader = new Dictionary<int, string>();
			m_mpHeaderCol = new Dictionary<string, int>();
			int i = 0;

			foreach (string s in rgs)
			{
				if (fDynamicHeader)
				{
					m_plsHeadings.Add(s);
					m_plsHeadingsMatch.Add(s.ToUpper());
				}
				else
				{
					if (!m_plsHeadings.Contains(s))
						return SR.Failed($"header {s} not in database");
				}

				m_mpColHeader.Add(i, s.ToUpper());
				m_mpHeaderCol.Add(s.ToUpper(), i);
				i++;
			}

			return SR.Success();
		}

		/* G E T  S T R I N G  V A L */
		/*----------------------------------------------------------------------------
			%%Function: GetStringVal
			%%Qualified: tw.twsvc:TwUser.GetStringVal
			%%Contact: rlittle

		----------------------------------------------------------------------------*/
		protected string GetStringVal(string[] rgs, string s, string sCurValue, List<string> plsDiff)
		{
			string sNew;

			if (m_mpHeaderCol.ContainsKey(s))
				sNew = rgs[m_mpHeaderCol[s]];
			else
				sNew = "";

			if (sNew != sCurValue)
				plsDiff.Add($"{s}('{sCurValue}' != '{sNew}')");

			return sNew;
		}

		/* G E T  S T R I N G  V A L */
		/*----------------------------------------------------------------------------
			%%Function: GetStringVal
			%%Qualified: tw.twsvc:TwUser.GetStringVal
			%%Contact: rlittle

		----------------------------------------------------------------------------*/
		public string GetStringVal(string[] rgs, string s)
		{
			string sNew;

			if (m_mpHeaderCol.ContainsKey(s))
				sNew = rgs[m_mpHeaderCol[s]];
			else
				sNew = "";

			return sNew;
		}

		/*----------------------------------------------------------------------------
			%%Function:LookupColumn
			%%Qualified:TCore.CSV.Csv.LookupColumn
		----------------------------------------------------------------------------*/
		public int LookupColumn(string s)
		{
			s = s.ToUpper();
			if (m_mpHeaderCol.ContainsKey(s))
				return m_mpHeaderCol[s];

			return -1;
		}

		protected List<string> TupleMake(Dictionary<string, string> mpColData)
		{
			List<string> tpl = new List<string>();

			foreach (string sHeading in m_plsHeadings)
			{
				string s;

				if (mpColData.ContainsKey(sHeading))
					s = mpColData[sHeading];
				else
					s = "";

				tpl.Add(s);
			}

			return tpl;
		}

		protected static string CsvFromTuple(IEnumerable<string> pls)
		{
			string sCsv = null;
			string sTemplateFirst = "\"{0}\"";
			string sTemplateNext = ",\"{0}\"";

			foreach (string s in pls)
			{
				if (sCsv == null)
					sCsv = String.Format(sTemplateFirst, s);
				else
					sCsv += String.Format(sTemplateNext, s);
			}

			return sCsv;
		}

		public static string CsvFromRgs(string[] rgs)
		{
			return CsvFromTuple(rgs);
		}

		protected string CsvMake(Dictionary<string, string> mpColData)
		{
			string sCsv = null;
			string sTemplateFirst = "\"{0}\"";
			string sTemplateNext = ",\"{0}\"";

			foreach (string sHeading in m_plsHeadings)
			{
				string s;

				if (mpColData.ContainsKey(sHeading))
					s = mpColData[sHeading];
				else
					s = "";

				if (sCsv == null)
					sCsv = String.Format(sTemplateFirst, s);
				else
					sCsv += String.Format(sTemplateNext, s);
			}

			return sCsv;
		}

		public List<string> TupleHeader()
		{
			Dictionary<string, string> mpColData = new Dictionary<string, string>();

			foreach (string s in m_plsHeadings)
				mpColData.Add(s, s);

			return TupleMake(mpColData);
		}

		public string Header()
		{
			return CsvFromTuple(TupleHeader());
		}

	}
}
