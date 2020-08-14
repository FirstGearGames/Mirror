using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mirror.Tests.Generators
{
    public class AttributeTestGenerator : GeneratorBase
    {
        [MenuItem("Tools/Generate Test files/Attributes")]
        public static void GenerateTestFiles()
        {
            List<string> classes = new List<string>();
            foreach (string baseClass in baseClassArray)
            {
                List<string> functions = new List<string>();
                List<string> tests = new List<string>();
                foreach (string attribute in attributeArray)
                {
                    foreach (string returnType in returnTypeArray)
                    {
                        functions.Add(AttributeFunction(attribute, returnType));
                        tests.Add(TestFunction(attribute, baseClass, returnType));

                        functions.Add(AttributeOutFunction(attribute, returnType));
                        tests.Add(TestOutFunction(attribute, baseClass, returnType));
                    }
                }

                classes.Add(Classes(baseClass, functions, tests));
            }
            string main = Main(classes);
            Save(main, "AttritubeTest");
        }

        static string[] returnTypeArray = new string[]
        {
           "float",
           "double",
           "bool",
           "char",
           "byte",
           "int",
           "long",
           "ulong",
           nameof(Vector3),
           nameof(ClassWithNoConstructor),
           nameof(ClassWithConstructor),
        };

        static string ReturnTypeToFullName(string returnType)
        {
            switch (returnType)
            {
                case "float":
                    return typeof(float).FullName;
                case "double":
                    return typeof(double).FullName;
                case "bool":
                    return typeof(bool).FullName;
                case "char":
                    return typeof(char).FullName;
                case "byte":
                    return typeof(byte).FullName;
                case "int":
                    return typeof(int).FullName;
                case "long":
                    return typeof(long).FullName;
                case "ulong":
                    return typeof(ulong).FullName;
                case nameof(Vector3):
                    return typeof(Vector3).FullName;
                case nameof(ClassWithNoConstructor):
                    return typeof(ClassWithNoConstructor).FullName;
                case nameof(ClassWithConstructor):
                    return typeof(ClassWithConstructor).FullName;
                default:
                    return returnType;
            }
        }

        static string[] attributeArray = new string[]
        {
            "Client",
            "Server",
            "ClientCallback",
            "ServerCallback"
        };

        static string[] baseClassArray = new string[]
        {
            "NetworkBehaviour",
            //"MonoBehaviour",
            //"ClassWithNoConstructor" // non unity class
        };

        const string NameSpace = BaseNameSpace + ".Attributes";

        static string Main(IEnumerable<string> classes)
        {
            string mergedClasses = Merge(classes);
            return $@"// Generated by {nameof(AttributeTestGenerator)}.cs
using Mirror.Tests.Generators;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace {NameSpace}
{{
    {mergedClasses}
}}";
        }

        static string Classes(string baseClass, IEnumerable<string> functions, IEnumerable<string> tests)
        {
            string mergedFunctions = Merge(functions);
            string mergedTests = Merge(tests);

            return $@"
    public class {AttributeBehaviourName(baseClass)} : {baseClass}
    {{
        public static readonly float Expected_float = 2020f;
        public static readonly double Expected_double = 2.54;
        public static readonly bool Expected_bool = true;
        public static readonly char Expected_char = 'a';
        public static readonly byte Expected_byte = 224;
        public static readonly int Expected_int = 103;
        public static readonly long Expected_long = -123456789L;
        public static readonly ulong Expected_ulong = 123456789UL;
        public static readonly Vector3 Expected_Vector3 = new Vector3(29, 1, 10);
        public static readonly ClassWithNoConstructor Expected_ClassWithNoConstructor = new ClassWithNoConstructor {{ a = 10 }};
        public static readonly ClassWithConstructor Expected_ClassWithConstructor = new ClassWithConstructor(29);

        {mergedFunctions}
    }}


    public class AttributeTest_{baseClass} 
    {{
        AttributeBehaviour_{baseClass} behaviour;
        GameObject go;

        [OneTimeSetUp]
        public void SetUp()
        {{
            go = new GameObject();
            behaviour = go.AddComponent<{AttributeBehaviourName(baseClass)}>();
        }}

        [OneTimeTearDown]
        public void TearDown()
        {{
            UnityEngine.Object.DestroyImmediate(go);
            NetworkClient.connectState = ConnectState.None;
            NetworkServer.active = false;
        }}

        {mergedTests}
    }}";
        }

        static string AttributeBehaviourName(string baseClass)
        {
            return $"AttributeBehaviour_{baseClass}";
        }
        static string AttributeFunctionName(string attribute, string returnType)
        {
            return $"{attribute}_{returnType}_Function";
        }
        static string AttributeOutFunctionName(string attribute, string returnType)
        {
            return $"{attribute}_{returnType}_out_Function";
        }

        static string AttributeFunction(string attribute, string returnType)
        {
            return $@"
        [{attribute}]
        public {returnType} {AttributeFunctionName(attribute, returnType)}()
        {{
            return Expected_{returnType};
        }}";
        }

        static string TestFunction(string attribute, string baseClass, string returnType)
        {
            string activeLine = ActivateServerClient(attribute);
            string logLine = attribute.Contains("Callback")
                ? ""
                : ExpectedLog(attribute, baseClass, returnType);

            return TestFunctionBody(attribute, baseClass, returnType, activeLine, logLine);
        }

        static string TestFunctionBody(string attribute, string baseClass, string returnType, string activeLine, string logLine)
        {
            return $@"
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void {attribute}_{returnType}_returnsValue(bool active)
        {{
            {activeLine}

            {returnType} expected = active ? {AttributeBehaviourName(baseClass)}.Expected_{returnType} : default;
            {logLine}
            {returnType} actual = behaviour.{AttributeFunctionName(attribute, returnType)}();

            Assert.AreEqual(expected, actual); 
        }}";
        }

        static string ActivateServerClient(string attribute)
        {
            if (attribute.Contains("Server"))
            {
                return "NetworkServer.active = active;";
            }
            else if (attribute.Contains("Client"))
            {
                return "NetworkClient.connectState = active ? ConnectState.Connected : ConnectState.None;";
            }
            else
            {
                throw new System.ArgumentException("Attribute must include Server or Client");
            }
        }

        static string ExpectedLog(string attribute, string baseClass, string returnType)
        {
            string functionFullName = $"{NameSpace}.{AttributeBehaviourName(baseClass)}::{AttributeFunctionName(attribute, returnType)}()";
            string returnTypeFullName = ReturnTypeToFullName(returnType);
            return $@"
            if (!active)
            {{
                LogAssert.Expect(LogType.Warning, ""[{attribute}] function '{returnTypeFullName} {functionFullName}' called when {attribute.ToLower()} was not active"");
            }}";
        }


        static string AttributeOutFunction(string attribute, string returnType)
        {
            return $@"
        [{attribute}]
        public void {AttributeOutFunctionName(attribute, returnType)}(out {returnType} value)
        {{
            value = Expected_{returnType}; 
        }}";
        }

        static string TestOutFunction(string attribute, string baseClass, string returnType)
        {
            string activeLine = ActivateServerClient(attribute);
            string logLine = attribute.Contains("Callback")
                ? ""
                : ExpectedOutLog(attribute, baseClass, returnType);

            return TestOutFunctionBody(attribute, baseClass, returnType, activeLine, logLine);
        }

        static string TestOutFunctionBody(string attribute, string baseClass, string returnType, string activeLine, string logLine)
        {
            return $@"
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void {attribute}_{returnType}_setsOutValue(bool active)
        {{
            {activeLine}

            {returnType} expected = active ? {AttributeBehaviourName(baseClass)}.Expected_{returnType} : default;
            {logLine}
            behaviour.{AttributeOutFunctionName(attribute, returnType)}(out {returnType} actual);

            Assert.AreEqual(expected, actual); 
        }}";
        }

        static string ExpectedOutLog(string attribute, string baseClass, string returnType)
        {
            string returnTypeFullName = ReturnTypeToFullName(returnType);
            string functionFullName = $"{NameSpace}.{AttributeBehaviourName(baseClass)}::{AttributeOutFunctionName(attribute, returnType)}({returnTypeFullName}&)";
            return $@"
            if (!active)
            {{
                LogAssert.Expect(LogType.Warning, ""[{attribute}] function 'System.Void {functionFullName}' called when {attribute.ToLower()} was not active"");
            }}";
        }
    }

    public class ClassWithNoConstructor
    {
        public int a;
    }

    public class ClassWithConstructor
    {
        public int a;

        public ClassWithConstructor(int a)
        {
            this.a = a;
        }
    }
}
