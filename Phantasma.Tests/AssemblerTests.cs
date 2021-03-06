using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Core.Utils;
using Phantasma.Core.Log;
using Phantasma.Cryptography;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Storage;
using Phantasma.Core;
using Phantasma.CodeGen;
using Phantasma.CodeGen.Assembler;
using Phantasma.Numerics;

namespace Phantasma.Tests
{
    [TestClass]
    public class AssemblerTests
    {


        #region RegisterOps

        [TestMethod]
        public void Move()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<int>>()
            {
                new List<int>() {1, 1},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                object r1 = argsLine[0];
                object target = argsLine[0];    //index 0 is not a typo, we want to copy the reference, not the contents

                scriptString = new string[]
                {
                    //put a DebugClass with x = {r1} on register 1
                    $@"load r1, {r1}",
                    $"push r1",
                    $"extcall \\\"PushDebugClass\\\"", 
                    $"pop r1",

                    //move it to r2, change its value on the stack and see if it changes on both registers
                    @"move r1, r2",
                    @"push r2",
                    $"push r1",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 2);

                var r1obj = vm.Stack.Pop().AsInterop<TestVM.DebugClass>();
                var r2obj = vm.Stack.Pop().AsInterop<TestVM.DebugClass>();

                Assert.IsTrue(ReferenceEquals(r1obj, r2obj));
            }
        }

        [TestMethod]
        public void Copy()
        {
            string[] scriptString;
            TestVM vm;

            scriptString = new string[]
            {
                //put a DebugClass with x = {r1} on register 1
                //$@"load r1, {value}",
                $"load r5, 1",
                $"push r5",
                $"extcall \\\"PushDebugStruct\\\"",
                $"pop r1",
                $"load r3, \\\"key\\\"",
                $"put r1, r2, r3",

                //move it to r2, change its value on the stack and see if it changes on both registers
                @"copy r1, r2",
                @"push r2",
                $"extcall \\\"IncrementDebugStruct\\\"",
                $"push r1",
                @"ret"
            };

            vm = ExecuteScript(scriptString);

            var r1struct = vm.Stack.Pop().AsInterop<TestVM.DebugStruct>();
            var r2struct = vm.Stack.Pop().AsInterop<TestVM.DebugStruct>();

            Assert.IsTrue(r1struct.x != r2struct.x);

        }

        [TestMethod]
        public void Load()
        {
            //TODO: test all VMTypes

            string[] scriptString;
            TestVM vm;

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                $"load r2, 123",
                $"load r3, true",
                //load struct
                //load bytes
                //load enum
                //load object

                $"push r3",
                $"push r2",
                $"push r1",
                $"ret"
            };

            vm = ExecuteScript(scriptString);

            Assert.IsTrue(vm.Stack.Count == 3);

            var str = vm.Stack.Pop().AsString();
            Assert.IsTrue(str.CompareTo("hello") == 0);

            var num = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(num == new BigInteger(123));

            var bo = vm.Stack.Pop().AsBool();
            Assert.IsTrue(bo);
        }

        [TestMethod]
        public void Push()
        {
            Load(); //it is effectively the same test
        }

        [TestMethod]
        public void Pop()
        {
            //TODO: test all VMTypes

            string[] scriptString;
            TestVM vm;

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                $"load r2, 123",
                $"load r3, true",
                //load struct
                //load bytes
                //load enum
                //load object

                $"push r3",
                $"push r2",
                $"push r1",

                $"pop r11",
                $"pop r12",
                $"pop r13",

                $"push r13",
                $"push r12",
                $"push r11",
                $"ret"
            };

            vm = ExecuteScript(scriptString);

            Assert.IsTrue(vm.Stack.Count == 3);

            var str = vm.Stack.Pop().AsString();
            Assert.IsTrue(str.CompareTo("hello") == 0);

            var num = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(num == new BigInteger(123));

            var bo = vm.Stack.Pop().AsBool();
            Assert.IsTrue(bo);
        }

        [TestMethod]
        public void Swap()
        {
            string[] scriptString;
            TestVM vm;

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                $"load r2, 123",
                $"swap r1, r2",
                $"push r1",
                $"push r2",
                $"ret"
            };

            vm = ExecuteScript(scriptString);

            Assert.IsTrue(vm.Stack.Count == 2);

            var str = vm.Stack.Pop().AsString();
            Assert.IsTrue(str.CompareTo("hello") == 0);

            var num = vm.Stack.Pop().AsNumber();
            Assert.IsTrue(num == new BigInteger(123));
        }

        #endregion

        #region FlowOps

        [TestMethod]
        public void Call()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<int>>()
            {
                new List<int>() {1, 2},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"call @label",
                    @"push r1",
                    @"ret",
                    $"@label: inc r1",
                    $"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == target);
            }
        }

        [TestMethod]
        public void ExtCall()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"abc", "ABC"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, \\\"{r1}\\\"",
                    $"push r1",
                    $"extcall \\\"Upper\\\"",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }
        }

        [TestMethod]
        public void Jmp()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<int>>()
            {
                new List<int>() {1, 1},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, 1",
                    $"jmp @label",
                    $"inc r1",
                    $"@label: push r1",
                    $"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == target);
            }
        }

        [TestMethod]
        public void JmpConditional()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<int>>()
            {
                new List<int>() {1, 1},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, true",
                    $"load r2, false",
                    $"load r3, {r1}",
                    $"load r4, {r1}",
                    $"jmpif r1, @label",
                    $"inc r3",
                    $"@label: jmpnot r2, @label2",
                    $"inc r4",
                    $"@label2: push r3",
                    $"push r4",
                    $"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 2);

                var result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == target, "Opcode JmpNot isn't working correctly");

                result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == target, "Opcode JmpIf isn't working correctly");
            }
        }

        [TestMethod]
        public void Throw()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<bool>>()
            {
                new List<bool>() {true, true},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, {r1}",
                    $"push r1",
                    $"throw",
                    $"not r1, r1",
                    $"pop r2",
                    $"push r1",
                    $"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsBool();
                Assert.IsTrue(result == target, "Opcode JmpNot isn't working correctly");

            }
        }


        #endregion

        #region LogicalOps
        [TestMethod]
        public void Not()
        {
            var scriptString = new string[]
            {
                $@"load r1, true",
                @"not r1, r2",
                @"push r2",
                @"ret"
            };

            var vm = ExecuteScript(scriptString);

            Assert.IsTrue(vm.Stack.Count == 1);

            var result = vm.Stack.Pop().AsString();
            Assert.IsTrue(result == "false");

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                @"not r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to NOT a non-bool variable.");
        }

        [TestMethod]
        public void And()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"true", "true", "true"},
                new List<string>() {"true", "false", "false"},
                new List<string>() {"false", "true", "false"},
                new List<string>() {"false", "false", "false"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"and r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, false",
                @"and r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Or()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"true", "true", "true"},
                new List<string>() {"true", "false", "true"},
                new List<string>() {"false", "true", "true"},
                new List<string>() {"false", "false", "false"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"or r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, false",
                @"or r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to OR a non-bool variable.");
        }

        [TestMethod]
        public void Xor()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"true", "true", "false"},
                new List<string>() {"true", "false", "true"},
                new List<string>() {"false", "true", "true"},
                new List<string>() {"false", "false", "false"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"xor r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, false",
                @"xor r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to XOR a non-bool variable.");
        }

        [TestMethod]
        public void Equals()
        {
            string[] scriptString;
            TestVM vm;
            string result;

            var args = new List<List<string>>()
            {
                new List<string>() {"true", "true", "true"},
                new List<string>() {"true", "false", "false"},
                new List<string>() {"1", "1", "true"},
                new List<string>() {"1", "2", "false"},
                new List<string>() { "\\\"hello\\\"", "\\\"hello\\\"", "true"},
                new List<string>() { "\\\"hello\\\"", "\\\"world\\\"", "false"},
                
                //TODO: add lines for bytes, structs, enums and structs
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"equal r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);


                result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }
        }

        [TestMethod]
        public void LessThan()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "false"},
                new List<string>() {"1", "1", "false"},
                new List<string>() {"1", "2", "true"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"lt r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"lt r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void GreaterThan()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "true"},
                new List<string>() {"1", "1", "false"},
                new List<string>() {"1", "2", "false"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"gt r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"gt r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void LesserThanOrEquals()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "false"},
                new List<string>() {"1", "1", "true"},
                new List<string>() {"1", "2", "true"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"lte r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"lte r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void GreaterThanOrEquals()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "true"},
                new List<string>() {"1", "1", "true"},
                new List<string>() {"1", "2", "false"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"gte r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"gte r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }
        #endregion

        #region NumericOps
        [TestMethod]
        public void Increment()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "2"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"inc r1",
                    @"push r1",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                @"inc r1",
                @"push r1",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void Decrement()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"2", "1"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"dec r1",
                    @"push r1",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"hello\\\"",
                @"dec r1",
                @"push r1",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void Sign()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"-1123124", "-1"},
                new List<string>() {"0", "0"},
                new List<string>() {"14564535", "1"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"sign r1, r2",
                    @"push r2",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                @"sign r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Negate()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"-1123124", "1123124"},
                new List<string>() {"0", "0"},
                new List<string>() {"14564535", "-14564535" }
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"negate r1, r2",
                    @"push r2",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                @"negate r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Abs()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"-1123124", "1123124"},
                new List<string>() {"0", "0"},
                new List<string>() {"14564535", "14564535" }
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = argsLine[1];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    @"abs r1, r2",
                    @"push r2",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                @"abs r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Add()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "246196246099661965807160469919750427681847698407517884715668182"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"add r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"add r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Sub()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "0"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"sub r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"sub r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Mul()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "15153147898391329927834760664056143940222558862285292671240041298552647375412113910342337827528430805055673715428680681796281"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"mul r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"mul r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Div()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "1"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"div r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"div r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void Mod()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "123098123049830982903580234959875213840923849203758942357834091", "0"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"mod r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"mod r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void ShiftLeft()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "100", "156045409571086686325343677668972466714151959338084738385422346983957734263469303184507273216"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"shl r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"shl r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }

        [TestMethod]
        public void ShiftRight()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"123098123049830982903580234959875213840923849203758942357834091", "100", "97107296780097167688396095959314" }
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"shr r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $@"load r1, true",
                $"load r2, \\\"stuff\\\"",
                @"shr r1, r2, r3",
                @"push r3",
                @"ret"
            };


            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to AND a non-bool variable.");
        }


        [TestMethod]
        public void Min()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "0"},
                new List<string>() {"1", "1", "1"},
                new List<string>() {"1", "2", "1"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"min r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"min r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }

        [TestMethod]
        public void Max()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<string>>()
            {
                new List<string>() {"1", "0", "1"},
                new List<string>() {"1", "1", "1"},
                new List<string>() {"1", "2", "2"},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string r2 = argsLine[1];
                string target = argsLine[2];

                scriptString = new string[]
                {
                    $@"load r1, {r1}",
                    $@"load r2, {r2}",
                    @"max r1, r2, r3",
                    @"push r3",
                    @"ret"
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == target);
            }

            scriptString = new string[]
            {
                $"load r1, \\\"abc\\\"",
                $@"load r2, 2",
                @"max r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                vm = ExecuteScript(scriptString);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("Didn't throw an exception after trying to compare non-integer variables.");
        }
        #endregion

        #region ContextOps

        [TestMethod]
        public void ContextSwitching()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<int[]>()
            {
                new int[] {1, 2},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    $"load r1, \\\"test\\\"",
                    $"load r3, 1",
                    $"push r3",
                    $"ctx r1, r2",
                    $"switch r2",
                    $"load r5, 42",
                    $"push r5",
                    @"ret",
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 2);

                var result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == 42);

                result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == 2);
            }
        }

        #endregion

        #region Array

        [TestMethod]
        public void PutGet()
        {
            string[] scriptString;
            TestVM vm;

            var args = new List<List<int>>()
            {
                new List<int>() {1, 1},
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                var r1 = argsLine[0];
                var target = argsLine[1];

                scriptString = new string[]
                {
                    //$"switch \\\"Test\\\"",
                    $"load r1 {r1}",
                    $"load r2 \\\"key\\\"",
                    $"put r1 r3 r2",
                    $"get r3 r4 r2",
                    $"push r4",
                    @"ret",
                };

                vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsNumber();
                Assert.IsTrue(result == target);
            }
        }

        #endregion

        #region Data
        [TestMethod]
        public void Cat()
        {
            var args = new List<List<string>>()
            {
                new List<string>() {"Hello", null},
                new List<string>() {null, " world"},
                new List<string>() {"", ""},
                new List<string>() {"Hello ", "world"}
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0] == null ? null : $"\\\"{argsLine[0]}\\\"";
                string r2 = argsLine[1] == null ? null : $"\\\"{argsLine[1]}\\\"";

                var scriptString = new string[1];

                switch (i)
                {
                    case 0:
                        scriptString = new string[]
                        {
                            $@"load r1, {r1}",
                            @"cat r1, r2, r3",
                            @"push r3",
                            @"ret"
                        };
                        break;
                    case 1:
                        scriptString = new string[]
                        {
                            $@"load r2, {r2}",
                            @"cat r1, r2, r3",
                            @"push r3",
                            @"ret"
                        };
                        break;
                    case 2:
                        scriptString = new string[]
                        {
                            $@"load r1, {r1}",
                            $@"load r2, {r2}",
                            @"cat r1, r2, r3",
                            @"push r3",
                            @"ret"
                        };
                        break;
                    case 3:
                        scriptString = new string[]
                        {
                            $@"load r1, {r1}",
                            $@"load r2, {r2}",
                            @"cat r1, r2, r3",
                            @"push r3",
                            @"ret"
                        };
                        break;
                }

                var vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();
                Assert.IsTrue(result == String.Concat(argsLine[0], argsLine[1]));
            }

            var scriptString2 = new string[]
            {
                $"load r1, \\\"Hello\\\"",
                $@"load r2, 1",
                @"cat r1, r2, r3",
                @"push r3",
                @"ret"
            };

            try
            {
                var vm2 = ExecuteScript(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("VM did not throw exception when trying to cat a string and a non-string object, and it should");
        }

        [TestMethod]
        public void Left()
        {
            var args = new List<List<string>>()
            {
                new List<string>() {"Hello world", "5", "Hello"},
                //TODO: missing tests with byte data
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string len = argsLine[1];
                string target = argsLine[2];

                var scriptString = new string[1];

                scriptString = new string[]
                {
                    $"load r1, \\\"{r1}\\\"",
                    $"left r1, r2, {len}",
                    @"push r2",
                    @"ret"
                };
        

                var vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var resultBytes = vm.Stack.Pop().AsByteArray();
                var result = Encoding.UTF8.GetString(resultBytes);
                
                Assert.IsTrue(result == target);
            }

            var scriptString2 = new string[]
            {
                $"load r1, 100",
                @"left r1, r2, 1",
                @"push r2",
                @"ret"
            };

            try
            {
                var vm2 = ExecuteScript(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("VM did not throw exception when trying to cat a string and a non-string object, and it should");
        }

        [TestMethod]
        public void Right()
        {
            var args = new List<List<string>>()
            {
                new List<string>() {"Hello world", "5", "world"},
                //TODO: missing tests with byte data
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string len = argsLine[1];
                string target = argsLine[2];

                var scriptString = new string[1];

                scriptString = new string[]
                {
                    $"load r1, \\\"{r1}\\\"",
                    $"right r1, r2, {len}",
                    @"push r2",
                    @"ret"
                };


                var vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var resultBytes = vm.Stack.Pop().AsByteArray();
                var result = Encoding.UTF8.GetString(resultBytes);

                Assert.IsTrue(result == target);
            }

            var scriptString2 = new string[]
            {
                $"load r1, 100",
                @"right r1, r2, 1",
                @"push r2",
                @"ret"
            };

            try
            {
                var vm2 = ExecuteScript(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("VM did not throw exception when trying to cat a string and a non-string object, and it should");
        }

        [TestMethod]
        public void Size()
        {
            var args = new List<List<string>>()
            {
                new List<string>() {"Hello world"},
                //TODO: missing tests with byte data
            };

            for (int i = 0; i < args.Count; i++)
            {
                var argsLine = args[i];
                string r1 = argsLine[0];
                string target = Encoding.UTF8.GetBytes(argsLine[0]).Length.ToString();

                var scriptString = new string[1];

                scriptString = new string[]
                {
                    $"load r1, \\\"{r1}\\\"",
                    $"size r1, r2",
                    @"push r2",
                    @"ret"
                };


                var vm = ExecuteScript(scriptString);

                Assert.IsTrue(vm.Stack.Count == 1);

                var result = vm.Stack.Pop().AsString();

                Assert.IsTrue(result == target);
            }

            var scriptString2 = new string[]
            {
                $"load r1, 100",
                @"size r1, r2",
                @"push r2",
                @"ret"
            };

            try
            {
                var vm2 = ExecuteScript(scriptString2);
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message == "Invalid cast");
                return;
            }

            throw new Exception("VM did not throw exception when trying to cat a string and a non-string object, and it should");
        }
        #endregion

        #region AuxFunctions
        private TestVM ExecuteScript(string[] scriptString)
        {
            var script = BuildScript(scriptString);

            var keys = KeyPair.Generate();
            var nexus = new Nexus("vmnet", keys.Address, new ConsoleLogger());
            var tx = new Transaction(nexus.Name, nexus.RootChain.Name, script, 0);

            var vm = new TestVM(tx.Script);
            vm.ThrowOnFault = true;
            vm.Execute();

            return vm;
        }


        private byte[] BuildScript(string[] lines)
        {
            IEnumerable<Semanteme> semantemes = null;
            try
            {
                semantemes = Semanteme.ProcessLines(lines);
            }
            catch (Exception e)
            {
                throw new InternalTestFailureException("Error parsing the script");
            }

            var sb = new ScriptBuilder();
            byte[] script = null;

            try
            {
                foreach (var entry in semantemes)
                {
                    Trace.WriteLine($"{entry}");
                    entry.Process(sb);
                }
                script = sb.ToScript();
            }
            catch (Exception e)
            {
                throw new InternalTestFailureException("Error assembling the script");
            }

            return script;
        }
        #endregion

    }
}
