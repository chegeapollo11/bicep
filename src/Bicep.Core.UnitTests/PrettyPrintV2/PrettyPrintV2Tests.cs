// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.PrettyPrintV2;
using Bicep.Core.UnitTests.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bicep.Core.UnitTests.PrettyPrintV2
{
    [TestClass]
    public class PrettyPrintV2Tests
    {
        private readonly static string ProgramText = """
            var foo = {
            prop1: true
            prop2: false
            prop3: {
            nestedProp1: 1
            nestedProp2: 2
            }
            }

            var bar = [
            1
            2
            {
            prop1: true
            prop2: false
            }
            ]
            """.ReplaceLineEndings("\n");

        [DataTestMethod]
        [DataRow(IndentKind.Space, NewlineKind.CRLF, 2, true, "var foo = {\r\n  prop1: true\r\n  prop2: false\r\n  prop3: {\r\n    nestedProp1: 1\r\n    nestedProp2: 2\r\n  }\r\n}\r\n\r\nvar bar = [\r\n  1\r\n  2\r\n  {\r\n    prop1: true\r\n    prop2: false\r\n  }\r\n]\r\n")]
        [DataRow(IndentKind.Space, NewlineKind.Auto, 4, false, "var foo = {\n    prop1: true\n    prop2: false\n    prop3: {\n        nestedProp1: 1\n        nestedProp2: 2\n    }\n}\n\nvar bar = [\n    1\n    2\n    {\n        prop1: true\n        prop2: false\n    }\n]")]
        [DataRow(IndentKind.Tab, NewlineKind.LF, 0, false, "var foo = {\n\tprop1: true\n\tprop2: false\n\tprop3: {\n\t\tnestedProp1: 1\n\t\tnestedProp2: 2\n\t}\n}\n\nvar bar = [\n\t1\n\t2\n\t{\n\t\tprop1: true\n\t\tprop2: false\n\t}\n]")]
        [DataRow(IndentKind.Tab, NewlineKind.CR, 2, true, "var foo = {\r\tprop1: true\r\tprop2: false\r\tprop3: {\r\t\tnestedProp1: 1\r\t\tnestedProp2: 2\r\t}\r}\r\rvar bar = [\r\t1\r\t2\r\t{\r\t\tprop1: true\r\t\tprop2: false\r\t}\r]\r")]
        public void Print_VariousOptions_PrintsAccordingly(IndentKind indentKind, NewlineKind newlineKind, int indentSize, bool insertFinalNewline, string expectedOutput)
        {
            var options = PrettyPrinterV2Options.Default with
            {
                IndentKind = indentKind,
                NewlineKind = newlineKind,
                IndentSize = indentSize,
                InsertFinalNewline = insertFinalNewline,
            };

            var program = ParserHelper.Parse(ProgramText, out var lexingErrorLookup, out var parsingErrorLookup);
            var context = PrettyPrinterV2Context.Create(program, options, lexingErrorLookup, parsingErrorLookup);
            var writer = new StringWriter();

            PrettyPrinterV2.PrintTo(writer, context);

            writer.ToString().Should().Be(expectedOutput);
        }
    }
}