using System;
using System.Reflection;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;

namespace NUnit.Core
{
	/// <summary>
	/// Static methods that implement aspects of the NUnit framework that cut 
	/// across individual test types, extensions, etc. Some of these use the 
	/// methods of the Reflect class to implement operations specific to the 
	/// NUnit Framework.
	/// </summary>
	public class NUnitFramework
	{
		private static Type assertType;
        //private static Hashtable frameworkByAssembly = new Hashtable();

        #region Constants

        #region Attribute Names
        // Attributes that apply to Assemblies, Classes and Methods
        public const string IgnoreAttribute = "NUnit.Framework.IgnoreAttribute";
        public const string PlatformAttribute = "NUnit.Framework.PlatformAttribute";
        public const string ExplicitAttribute = "NUnit.Framework.ExplicitAttribute";

        // Attributes that apply to Classes and Methods
        public static readonly string CategoryAttribute = "NUnit.Framework.CategoryAttribute";
        public static readonly string PropertyAttribute = "NUnit.Framework.PropertyAttribute";

        // Attributes that apply only to Classes
        public static readonly string TestFixtureAttribute = "NUnit.Framework.TestFixtureAttribute";
        public static readonly string SetUpFixtureAttribute = "NUnit.Framework.SetUpFixtureAttribute";

        // Attributes that apply only to Methods
        public static readonly string TestAttribute = "NUnit.Framework.TestAttribute";
        public static readonly string SetUpAttribute = "NUnit.Framework.SetUpAttribute";
        public static readonly string TearDownAttribute = "NUnit.Framework.TearDownAttribute";
        public static readonly string FixtureSetUpAttribute = "NUnit.Framework.TestFixtureSetUpAttribute";
        public static readonly string FixtureTearDownAttribute = "NUnit.Framework.TestFixtureTearDownAttribute";
        public static readonly string ExpectedExceptionAttribute = "NUnit.Framework.ExpectedExceptionAttribute";

        // Attributes that apply only to Properties
        public static readonly string SuiteAttribute = "NUnit.Framework.SuiteAttribute";
        #endregion

        #region Other Framework Types
        public static readonly string AssertException = "NUnit.Framework.AssertionException";
        public static readonly string IgnoreException = "NUnit.Framework.IgnoreException";
        public static readonly string AssertType = "NUnit.Framework.Assert";
        #endregion

        #region Core Types
        public static readonly string SuiteBuilderAttribute = typeof(SuiteBuilderAttribute).FullName;
        public static readonly string SuiteBuilderInterface = typeof(ISuiteBuilder).FullName;

        public static readonly string TestCaseBuilderAttributeName = typeof(TestCaseBuilderAttribute).FullName;
        public static readonly string TestCaseBuilderInterfaceName = typeof(ITestCaseBuilder).FullName;

        public static readonly string TestDecoratorAttributeName = typeof(TestDecoratorAttribute).FullName;
        public static readonly string TestDecoratorInterfaceName = typeof(ITestDecorator).FullName;
        #endregion

        #endregion

        #region Identify SetUp and TearDown Methods
        public static bool IsSetUpMethod(MethodInfo method)
        {
            return Reflect.HasAttribute(method, NUnitFramework.SetUpAttribute, false);
        }

        public static bool IsTearDownMethod(MethodInfo method)
        {
            return Reflect.HasAttribute(method, NUnitFramework.TearDownAttribute, false);
        }

        public static bool IsFixtureSetUpMethod(MethodInfo method)
        {
            return Reflect.HasAttribute(method, NUnitFramework.FixtureSetUpAttribute, false);
        }

        public static bool IsFixtureTearDownMethod(MethodInfo method)
        {
            return Reflect.HasAttribute(method, NUnitFramework.FixtureTearDownAttribute, false);
        }

        #endregion

        #region Locate SetUp and TearDown Methods
        public static MethodInfo GetSetUpMethod(Type fixtureType)
		{
			return Reflect.GetMethodWithAttribute(fixtureType, SetUpAttribute,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				true);
		}

        public static MethodInfo GetTearDownMethod(Type fixtureType)
		{
			return Reflect.GetMethodWithAttribute(fixtureType, TearDownAttribute,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				true);
		}

		public static MethodInfo GetFixtureSetUpMethod(Type fixtureType)
		{
			return Reflect.GetMethodWithAttribute(fixtureType, FixtureSetUpAttribute,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				true);
		}

        public static MethodInfo GetFixtureTearDownMethod(Type fixtureType)
		{
			return Reflect.GetMethodWithAttribute(fixtureType, FixtureTearDownAttribute,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				true);
		}
		#endregion

		#region GetCategories
		public static IList GetCategories( MemberInfo member )
		{
			System.Attribute[] attributes = 
                Reflect.GetAttributes( member, NUnitFramework.CategoryAttribute, false );
			IList categories = new ArrayList();

			foreach( Attribute categoryAttribute in attributes ) 
				categories.Add( 
					Reflect.GetPropertyValue( 
					categoryAttribute, 
					"Name", 
					BindingFlags.Public | BindingFlags.Instance ) );

			return categories;
		}
		#endregion

		#region ApplyCommonAttributes
        /// <summary>
        /// Modify a newly constructed test by applying any of NUnit's common
        /// attributes, specified for the type or method.
        /// </summary>
        /// <param name="member">The type or method from which the test was constructed</param>
        /// <param name="test">The test to which the attributes apply</param>
        public static void ApplyCommonAttributes(MemberInfo member, Test test)
        {
            ApplyCommonAttributes( Reflect.GetAttributes( member, false ), test );
        }

        /// <summary>
        /// Modify a newly constructed test by applying any of NUnit's common
        /// attributes, specified for an assembly.
        /// </summary>
        /// <param name="assembly">The assembly from which the test was constructed</param>
        /// <param name="test">The test to which the attributes apply</param>
        public static void ApplyCommonAttributes(Assembly assembly, Test test)
        {
            ApplyCommonAttributes( Reflect.GetAttributes( assembly, false ), test );
        }

        /// <summary>
        /// Modify a newly constructed test by applying any of NUnit's common
        /// attributes, based on an input array of attributes. This method checks
        /// for all attributes, relying on the fact that specific attributes can only
        /// occur on those constructs on which they are allowed.
        /// </summary>
        /// <param name="attributes">An array of attributes possibly including NUnit attributes
        /// <param name="test">The test to which the attributes apply</param>
        public static void ApplyCommonAttributes(Attribute[] attributes, Test test)
        {
            foreach (Attribute attribute in attributes)
            {
                switch (attribute.GetType().FullName)
                {
                    case ExplicitAttribute:
                        test.IsExplicit = true;
                        test.RunState = RunState.Explicit;
                        test.IgnoreReason = GetIgnoreReason(attribute);
                        break;
                    case IgnoreAttribute:
                        test.RunState = RunState.Ignored;
                        test.IgnoreReason = GetIgnoreReason(attribute);
                        break;
                    case PlatformAttribute:
                        PlatformHelper helper = new PlatformHelper();
                        if (!helper.IsPlatformSupported(attribute))
                        {
                            test.RunState = RunState.Skipped;
                            test.IgnoreReason = GetIgnoreReason(attribute);
                        }
                        break;
                    default:
                        break;
                }
            }
        }
		#endregion

		#region GetProperties
		public static IDictionary GetProperties( MemberInfo member )
		{
			ListDictionary properties = new ListDictionary();

			foreach( Attribute propertyAttribute in 
                Reflect.GetAttributes( member, NUnitFramework.PropertyAttribute, false ) ) 
			{
				string name = (string)Reflect.GetPropertyValue( propertyAttribute, "Name", BindingFlags.Public | BindingFlags.Instance );
				if ( name != null && name != string.Empty )
				{
					object val = Reflect.GetPropertyValue( propertyAttribute, "Value", BindingFlags.Public | BindingFlags.Instance );
					properties[name] = val;
				}
			}

			return properties;
		}
		#endregion

		#region ApplyExpectedExceptionAttribute
		// TODO: Handle this with a separate ExceptionProcessor object
		public static void ApplyExpectedExceptionAttribute(MethodInfo method, TestMethod testMethod)
		{
			Type expectedException = null;
			string expectedExceptionName = null;
			string expectedMessage = null;
			string matchType = null;

			Attribute attribute = Reflect.GetAttribute(
                method, NUnitFramework.ExpectedExceptionAttribute, false );

			if (attribute != null)
			{
				expectedException = Reflect.GetPropertyValue(
					attribute, "ExceptionType",
					BindingFlags.Public | BindingFlags.Instance) as Type;
				expectedExceptionName = (string)Reflect.GetPropertyValue(
					attribute, "ExceptionName",
					BindingFlags.Public | BindingFlags.Instance) as String;
				expectedMessage = (string)Reflect.GetPropertyValue(
					attribute, "ExpectedMessage",
					BindingFlags.Public | BindingFlags.Instance) as String;
				object matchEnum = Reflect.GetPropertyValue(
					attribute, "MatchType",
					BindingFlags.Public | BindingFlags.Instance);
				if (matchEnum != null)
					matchType = matchEnum.ToString();
			}

			if ( expectedException != null )
				testMethod.ExpectedException = expectedException;
			else if ( expectedExceptionName != null )
				testMethod.ExpectedExceptionName = expectedExceptionName;

			testMethod.ExpectedMessage = expectedMessage;
			testMethod.MatchType = matchType;
		}
		#endregion

		#region GetAssertCount
		public static int GetAssertCount()
		{
			if ( assertType == null )
				foreach( Assembly assembly in AppDomain.CurrentDomain.GetAssemblies() )
					if ( assembly.GetName().Name == "nunit.framework" )
					{
						assertType = assembly.GetType( AssertType );
						break;
					}

			if ( assertType == null )
				return 0;

			PropertyInfo property = Reflect.GetNamedProperty( 
				assertType,
				"Counter", 
				BindingFlags.Public | BindingFlags.Static );

			if ( property == null )
				return 0;
		
			return (int)property.GetValue( null, new object[0] );
		}
		#endregion

		#region IsSuiteBuilder
		public static bool IsSuiteBuilder( Type type )
		{
			return Reflect.HasAttribute( type, SuiteBuilderAttribute, false )
				&& Reflect.HasInterface( type, SuiteBuilderInterface );
		}
		#endregion

		#region IsTestCaseBuilder
		public static bool IsTestCaseBuilder( Type type )
		{
			return Reflect.HasAttribute( type, TestCaseBuilderAttributeName, false )
				&& Reflect.HasInterface( type, TestCaseBuilderInterfaceName );
		}
		#endregion

		#region IsTestDecorator
		public static bool IsTestDecorator( Type type )
		{
			return Reflect.HasAttribute( type, TestDecoratorAttributeName, false )
				&& Reflect.HasInterface( type, TestDecoratorInterfaceName );
		}
		#endregion

		#region GetIgnoreReason
		public static string GetIgnoreReason( System.Attribute attribute )
		{
			return (string)Reflect.GetPropertyValue(
				attribute,
				"Reason",
				BindingFlags.Public | BindingFlags.Instance);
		}
        #endregion

        #region GetDescription
        /// <summary>
        /// Method to return the description for a fixture
        /// </summary>
        /// <param name="fixtureType">The fixture to check</param>
        /// <returns>The description, if any, or null</returns>
        public static string GetDescription(Type type)
        {
            Attribute fixtureAttribute = Reflect.GetAttribute(type, TestFixtureAttribute, true);

            if (fixtureAttribute != null)
                return NUnitFramework.GetDescription(fixtureAttribute);

            return null;
        }

        /// <summary>
        /// Method to return the description for a method
        /// </summary>
        /// <param name="method">The method to check</param>
        /// <returns>The description, if any, or null</returns>
        public static string GetDescription(MethodInfo method)
        {
            Attribute testAttribute = Reflect.GetAttribute(method, TestAttribute, true);

            if (testAttribute != null)
                return GetDescription(testAttribute);

            return null;
        }

        /// <summary>
        /// Method to return the description from an attribute
        /// </summary>
        /// <param name="attribute">The attribute to check</param>
        /// <returns>The description, if any, or null</returns>
        public static string GetDescription(System.Attribute attribute)
		{
			return (string)Reflect.GetPropertyValue(
				attribute,
				"Description",
				BindingFlags.Public | BindingFlags.Instance);
		}
		#endregion

		#region AllowOldStyleTests
		public static bool AllowOldStyleTests
		{
			get
			{
				try
				{
					NameValueCollection settings = (NameValueCollection)
						ConfigurationSettings.GetConfig("NUnit/TestCaseBuilder");
					if (settings != null)
					{
						string oldStyle = settings["OldStyleTestCases"];
						if (oldStyle != null)
							return Boolean.Parse(oldStyle);
					}
				}
				catch( Exception e )
				{
					Debug.WriteLine( e );
				}

				return false;
			}
		}
		#endregion
	}
}