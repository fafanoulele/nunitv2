#region Copyright (c) 2002-2003, James W. Newkirk, Michael C. Two, Alexei A. Vorontsov, Charlie Poole, Philip A. Craig
/************************************************************************************
'
' Copyright � 2002-2003 James W. Newkirk, Michael C. Two, Alexei A. Vorontsov, Charlie Poole
' Copyright � 2000-2003 Philip A. Craig
'
' This software is provided 'as-is', without any express or implied warranty. In no 
' event will the authors be held liable for any damages arising from the use of this 
' software.
' 
' Permission is granted to anyone to use this software for any purpose, including 
' commercial applications, and to alter it and redistribute it freely, subject to the 
' following restrictions:
'
' 1. The origin of this software must not be misrepresented; you must not claim that 
' you wrote the original software. If you use this software in a product, an 
' acknowledgment (see the following) in the product documentation is required.
'
' Portions Copyright � 2003 James W. Newkirk, Michael C. Two, Alexei A. Vorontsov, Charlie Poole
' or Copyright � 2000-2003 Philip A. Craig
'
' 2. Altered source versions must be plainly marked as such, and must not be 
' misrepresented as being the original software.
'
' 3. This notice may not be removed or altered from any source distribution.
'
'***********************************************************************************/
#endregion

namespace NUnit.Console
{
	using System;
	using System.Collections;
	using System.Collections.Specialized;
	using System.IO;
	using System.Reflection;
	using System.Xml;
	using System.Xml.Xsl;
	using System.Xml.XPath;
	using System.Resources;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Diagnostics;
	using NUnit.Core;
	using NUnit.Util;
	

	/// <summary>
	/// Summary description for ConsoleUi.
	/// </summary>
	public class ConsoleUi
	{
		[STAThread]
		public static int Main(string[] args)
		{
			ConsoleOptions options = new ConsoleOptions(args);
			if(!options.nologo)
				WriteCopyright();

			if(options.help)
			{
				options.Help();
				return 0;
			}
			
			if(options.NoArgs) 
			{
				Console.Error.WriteLine("fatal error: no inputs specified");
				options.Help();
				return 0;
			}
			
			if(!options.Validate())
			{
				Console.Error.WriteLine("fatal error: invalid arguments");
				options.Help();
				return 2;
			}

			try
			{
				ConsoleUi consoleUi = new ConsoleUi();
				return consoleUi.Execute( options );
			}
			catch( FileNotFoundException ex )
			{
				Console.WriteLine( ex.Message );
				return 2;
			}
			catch( BadImageFormatException ex )
			{
				Console.WriteLine( ex.Message );
				return 2;
			}
			catch( Exception ex )
			{
				Console.WriteLine( "Unhandled Exception:\n{0}", ex.ToString() );
				return 2;
			}
			finally
			{
				if(options.wait)
				{
					Console.Out.WriteLine("\nHit <enter> key to continue");
					Console.ReadLine();
				}
			}
		}

		private static XmlTextReader GetTransformReader(ConsoleOptions parser)
		{
			XmlTextReader reader = null;
			if(!parser.IsTransform)
			{
				Assembly assembly = Assembly.GetAssembly(typeof(XmlResultVisitor));
				ResourceManager resourceManager = new ResourceManager("NUnit.Util.Transform",assembly);
				string xmlData = (string)resourceManager.GetObject("Summary.xslt");

				reader = new XmlTextReader(new StringReader(xmlData));
			}
			else
			{
				FileInfo xsltInfo = new FileInfo(parser.transform);
				if(!xsltInfo.Exists)
				{
					Console.Error.WriteLine("Transform file: {0} does not exist", xsltInfo.FullName);
					reader = null;
				}
				else
				{
					reader = new XmlTextReader(xsltInfo.FullName);
				}
			}

			return reader;
		}

		private static void WriteCopyright()
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			System.Version version = executingAssembly.GetName().Version;

			object[] objectAttrs = executingAssembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false);
			AssemblyProductAttribute productAttr = (AssemblyProductAttribute)objectAttrs[0];

			objectAttrs = executingAssembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
			AssemblyCopyrightAttribute copyrightAttr = (AssemblyCopyrightAttribute)objectAttrs[0];

			Console.WriteLine(String.Format("{0} version {1}", productAttr.Product, version.ToString(3)));
			Console.WriteLine(copyrightAttr.Copyright);
			Console.WriteLine();
		}

		private static Test MakeTestFromCommandLine(TestDomain testDomain, ConsoleOptions parser)
		{
			NUnitProject project;

			if ( parser.IsTestProject )
			{
				project = NUnitProject.LoadProject( (string)parser.Parameters[0] );
				string configName = (string) parser.config;
				if ( configName != null )
					project.SetActiveConfig( configName );
			}
			else
				project = NUnitProject.FromAssemblies( (string[])parser.Parameters.ToArray( typeof( string ) ) );

			return testDomain.Load( project, parser.fixture );
		}

		public ConsoleUi()
		{
		}

		public int Execute( ConsoleOptions options )
		{
			XmlTextReader transformReader = GetTransformReader(options);
			if(transformReader == null) return 3;

			ConsoleWriter outStream = options.isOut
				? new ConsoleWriter( new StreamWriter( options.output ) )
				: new ConsoleWriter(Console.Out);

			ConsoleWriter errorStream = options.isErr
				? new ConsoleWriter( new StreamWriter( options.err ) )
				: new ConsoleWriter(Console.Error);

			TestDomain testDomain = new TestDomain(outStream, errorStream);

			Test test = MakeTestFromCommandLine(testDomain, options);

			if(test == null)
			{
				Console.Error.WriteLine("Unable to locate fixture {0}", options.fixture);
				return 2;
			}

			Directory.SetCurrentDirectory(new FileInfo((string)options.Parameters[0]).DirectoryName);
		
			EventListener collector = new EventCollector( options, outStream );

			string savedDirectory = Environment.CurrentDirectory;
			TestResult result = null;
			if (options.IsCategories) 
			{
				CategoryFilter filter = new CategoryFilter();
				foreach (string category in options.CategoryArray)
				{
					filter.AddCategory(category);
				}
				result = testDomain.Run(collector, filter);
			} 
			else 
			{
				result = testDomain.Run( collector );
			}
			Directory.SetCurrentDirectory( savedDirectory );
			
			Console.WriteLine();

			StringBuilder builder = new StringBuilder();
			XmlResultVisitor resultVisitor = new XmlResultVisitor(new StringWriter( builder ), result);
			result.Accept(resultVisitor);
			resultVisitor.Write();

			string xmlOutput = builder.ToString();

			if (options.xmlConsole)
				Console.WriteLine(xmlOutput);
			else
				CreateSummaryDocument(xmlOutput, transformReader);

			// Write xml output here
			string xmlResult = options.IsXml ? options.xml : "TestResult.xml";

			using (StreamWriter writer = new StreamWriter(xmlResult)) 
			{
				writer.Write(xmlOutput);
			}

			int resultCode = 0;
			
			if(result.IsFailure)
				resultCode = 1;

			return resultCode;
		}

		private void CreateSummaryDocument(string xmlOutput, XmlTextReader transformReader)
		{
			XPathDocument originalXPathDocument = new XPathDocument(new StringReader(xmlOutput));
			XslTransform summaryXslTransform = new XslTransform();
			
			// Using obsolete form for now, remove warning suppression from project after changing
			summaryXslTransform.Load(transformReader);
			
			// Using obsolete form for now, remove warning suppression from project after changing
			summaryXslTransform.Transform(originalXPathDocument,null,Console.Out);
		}

		#region Nested Class to Handle Events

		private class EventCollector : LongLivingMarshalByRefObject, EventListener
		{
			private int testRunCount;
			private int testIgnoreCount;
			private int failureCount;
			private int level;

			private ConsoleOptions options;
			private ConsoleWriter writer;

			StringCollection messages;
		
			private bool debugger = false;
			private string currentTestName;

			public EventCollector( ConsoleOptions options, ConsoleWriter writer )
			{
				debugger = Debugger.IsAttached;
				level = 0;
				this.options = options;
				this.writer = writer;
				this.currentTestName = string.Empty;
			}

			public void RunStarted(Test[] tests)
			{
			}

			public void RunFinished(TestResult[] results)
			{
			}

			public void RunFinished(Exception exception)
			{
			}

			public void TestFinished(TestCaseResult testResult)
			{
				if ( !options.xmlConsole )
				{
					if(testResult.Executed)
					{
						testRunCount++;
						
						if(testResult.IsFailure)
						{	
							failureCount++;
							Console.Write("F");
							if ( debugger )
								messages.Add( ParseTestCaseResult( testResult ) );
						}
					}
					else
					{
						testIgnoreCount++;
						Console.Write("N");
					}
				}

				currentTestName = string.Empty;
			}

			public void TestStarted(TestCase testCase)
			{
				currentTestName = testCase.FullName;

				if ( !options.xmlConsole )
					Console.Write(".");

				if ( options.labels )
					writer.WriteLine("[{0}]", testCase.FullName );
			}

			public void SuiteStarted(TestSuite suite) 
			{
				if ( debugger && level++ == 0 )
				{
					messages = new StringCollection();
					testRunCount = 0;
					testIgnoreCount = 0;
					failureCount = 0;
					Trace.WriteLine( "################################ UNIT TESTS ################################" );
					Trace.WriteLine( "Running tests in '" + suite.FullName + "'..." );
				}
			}

			public void SuiteFinished(TestSuiteResult suiteResult) 
			{
				if ( debugger && --level == 0) 
				{
					Trace.WriteLine( "############################################################################" );

					if (messages.Count == 0) 
					{
						Trace.WriteLine( "##############                 S U C C E S S               #################" );
					}
					else 
					{
						Trace.WriteLine( "##############                F A I L U R E S              #################" );
						
						foreach ( string s in messages ) 
						{
							Trace.WriteLine(s);
						}
					}

					Trace.WriteLine( "############################################################################" );
					Trace.WriteLine( "Executed tests : " + testRunCount );
					Trace.WriteLine( "Ignored tests  : " + testIgnoreCount );
					Trace.WriteLine( "Failed tests   : " + failureCount );
					Trace.WriteLine( "Total time     : " + suiteResult.Time + " seconds" );
					Trace.WriteLine( "############################################################################");
				}
			}

			public void UnhandledException( Exception exception )
			{
				string msg = string.Format( "##### Unhandled Exception while running {0}", currentTestName );

				// If we do labels, we already have a newline
				if ( !options.labels ) writer.WriteLine();
				writer.WriteLine( msg );
				writer.WriteLine( exception.ToString() );

				if ( debugger )
				{
					Trace.WriteLine( msg );
					Trace.WriteLine( exception.ToString() );
				}
			}

			private string ParseTestCaseResult( TestCaseResult result ) 
			{
				string[] trace = result.StackTrace.Split( System.Environment.NewLine.ToCharArray() );
			
				foreach (string s in trace) 
				{
					if ( s.IndexOf( result.Test.FullName ) >= 0 ) 
					{
						string link = Regex.Replace( s.Trim(), @"at " + result.Test.FullName + @"\(\) in (.*):line (.*)", "$1($2)");

						string message = string.Format("{1}: {0}", 
							result.Message.Replace(System.Environment.NewLine, "; "), 
							result.Test.FullName).Trim(' ', ':');
					
						return string.Format("{0}: {1}", link, message);
					}
				}

				return result.Message;
			}
		}

		#endregion
	}
}
