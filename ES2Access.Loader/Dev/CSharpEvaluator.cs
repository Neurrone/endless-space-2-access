using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Mono.CSharp;

namespace ES2Access.Loader.Dev
{
    /// <summary>
    /// A C# REPL over the running game, behind POST /eval. One evaluator lives for the whole
    /// session, so variables and usings declared by one request are still there for the next, and
    /// a hot reload only adds the new mod assembly to what it can see.
    ///
    /// Compiling is Mono.CSharp (vendor\mcs\mcs.dll, the sinai-dev/mcs-unity net35 build that
    /// UnityExplorer uses). Its diagnostics do not come back from the call: they are printed to a
    /// <see cref="ReportPrinter"/>, so this holds its own printer over a StringWriter and reads
    /// the text back out after each attempt.
    ///
    /// Main thread only - evaluated code runs inline, and reaching game state is the whole point.
    /// </summary>
    internal sealed class CSharpEvaluator
    {
        internal sealed class Result
        {
            public bool Ok;

            /// <summary>ToString of what the source evaluated to; null when it was a statement or
            /// a void call, which produce no value.</summary>
            public string Value;

            public string Error;

            public static Result Failed(string error)
            {
                return new Result { Error = error };
            }
        }

        // What evaluated code can name. Everything here is already in the process; the REPL is
        // for driving the live game, not for compiling against things it has never loaded.
        private static readonly string[] ReferencedAssemblies =
        {
            "mscorlib",
            "System",
            "System.Core",
            "UnityEngine",
            "UnityEngine.UI",
            "Assembly-CSharp",
            "Assembly-CSharp-firstpass",
            "Amplitude",
            "Newtonsoft.Json",
        };

        private static readonly string[] InitialUsings =
        {
            "using System;",
            "using System.Collections.Generic;",
            "using System.Linq;",
            "using UnityEngine;",
        };

        private readonly StringWriter _messages = new StringWriter(CultureInfo.InvariantCulture);
        private readonly StreamReportPrinter _printer;
        private readonly Evaluator _evaluator;

        public CSharpEvaluator()
        {
            _printer = new StreamReportPrinter(_messages);
            CompilerSettings settings = new CompilerSettings
            {
                Version = LanguageVersion.Experimental,
                Target = Target.Library,
                TargetExt = ".dll",
                GenerateDebugInfo = false,
                WarningLevel = 0,
                EnhancedWarnings = false,
            };
            _evaluator = new Evaluator(new CompilerContext(settings, _printer));

            foreach (Assembly assembly in Loaded())
            {
                Reference(assembly);
            }

            Reference(typeof(CSharpEvaluator).Assembly);

            // The first compile is also what loads the compiler's own default references, so any
            // complaint about those would otherwise come back as the first request's error text.
            foreach (string directive in InitialUsings)
            {
                _evaluator.Run(directive);
            }

            Clear();
        }

        public void Reference(Assembly assembly)
        {
            try
            {
                _evaluator.ReferenceAssembly(assembly);
            }
            catch (Exception e)
            {
                LoaderLog.Warn(
                    "eval: could not reference " + assembly.GetName().Name + ": " + e.Message
                );
            }
        }

        public Result Evaluate(string source)
        {
            Clear();

            object value;
            bool valueSet;
            string incomplete;
            try
            {
                incomplete = _evaluator.Evaluate(source, out value, out valueSet);
            }
            catch (Exception e)
            {
                return Result.Failed(e.ToString());
            }

            if (incomplete != null)
            {
                return Result.Failed(
                    "incomplete input: this is not a whole statement or expression"
                );
            }

            if (_printer.ErrorsCount > 0)
            {
                return Result.Failed(Messages("the source did not compile"));
            }

            return new Result { Ok = true, Value = valueSet ? Describe(value) : null };
        }

        private static List<Assembly> Loaded()
        {
            List<Assembly> found = new List<Assembly>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (Array.IndexOf(ReferencedAssemblies, assembly.GetName().Name) >= 0)
                {
                    found.Add(assembly);
                }
            }

            return found;
        }

        private static string Describe(object value)
        {
            if (value == null)
            {
                return "null";
            }

            try
            {
                return value.ToString();
            }
            catch (Exception e)
            {
                return value.GetType().FullName + " (ToString threw: " + e.Message + ")";
            }
        }

        private string Messages(string fallback)
        {
            string text = _messages.ToString().Trim();
            return text.Length == 0 ? fallback : text;
        }

        private void Clear()
        {
            _messages.GetStringBuilder().Length = 0;
            _printer.Reset();
        }
    }
}
