﻿using System.Collections.Generic;
using Scorpio.Compile.Exception;
using Scorpio.Compile.CodeDom;
using Scorpio.Compile.CodeDom.Temp;
using Scorpio.Instruction;
namespace Scorpio.Compile.Compiler {
    
    /// <summary> 编译脚本 </summary>
    public partial class ScriptParser {
        public List<double> ConstDouble { get; private set; } = new List<double>();
        public List<long> ConstLong { get; private set; } = new List<long>();
        public List<string> ConstString { get; private set; } = new List<string>();
        public List<ScriptFunctionData> Functions { get; private set; } = new List<ScriptFunctionData>();                   //定义的所有 function
        public List<ScriptClassData> Classes { get; private set; } = new List<ScriptClassData>();                           //定义的所有 class
        private Stack<List<ScriptInstructionCompiler>> m_Breaks = new Stack<List<ScriptInstructionCompiler>>();             //breaks
        private Stack<List<ScriptInstructionCompiler>> m_Continues = new Stack<List<ScriptInstructionCompiler>>();          //continues
        private Stack<List<ScriptInstructionCompiler>> m_Cases = new Stack<List<ScriptInstructionCompiler>>();               //cases
        private List<ScriptInstructionCompiler> m_Break = new List<ScriptInstructionCompiler>();                            //break
        private List<ScriptInstructionCompiler> m_Continue = new List<ScriptInstructionCompiler>();                         //continue
        private List<ScriptInstructionCompiler> m_Case = new List<ScriptInstructionCompiler>();                                 //case
        private List<ScriptExecutable> m_Executables = new List<ScriptExecutable>();                                        //指令栈
        private ScriptExecutable m_scriptExecutable;                                                                        //当前指令栈
        public int Index { get { return m_scriptExecutable.Count(); } }
        public bool SupportCase(ExecutableBlock block) { return block == ExecutableBlock.Switch; }
        public bool SupportBreak(ExecutableBlock block) { return block == ExecutableBlock.For || block == ExecutableBlock.Foreach || block == ExecutableBlock.While || block == ExecutableBlock.Switch; }
        public bool SupportContinue(ExecutableBlock block) { return block == ExecutableBlock.For || block == ExecutableBlock.Foreach || block == ExecutableBlock.While; }
        public bool BlockSupportBreak {
            get {
                foreach (var block in m_scriptExecutable.Blocks) {
                    if (SupportBreak(block)) { return true; }
                }
                return false;
            }
        }
        public bool BlockSupportContinue {
            get {
                foreach (var block in m_scriptExecutable.Blocks) {
                    if (SupportContinue(block)) { return true; }
                }
                return false;
            }
        }
        ScriptExecutable BeginExecutable(ExecutableBlock block) {
            if (block == ExecutableBlock.Context || block == ExecutableBlock.Function) {
                if (m_scriptExecutable != null) { m_Executables.Add(m_scriptExecutable); }
                m_scriptExecutable = new ScriptExecutable(block);
            } else {
                m_scriptExecutable.BeginStack(block);
                if (SupportBreak(block)) { m_Breaks.Push(new List<ScriptInstructionCompiler>()); }
                if (SupportContinue(block)) { m_Continues.Push(new List<ScriptInstructionCompiler>()); }
                if (SupportCase(block)) { m_Cases.Push(new List<ScriptInstructionCompiler>()); }
            }
            return m_scriptExecutable;
        }
        ScriptExecutable EndExecutable(ExecutableBlock block) {
            var executable = m_scriptExecutable;
            if (executable.Block == ExecutableBlock.Context || executable.Block == ExecutableBlock.Function) {
                m_scriptExecutable.Finished();
                if (m_Executables.Count > 0) {
                    m_scriptExecutable = m_Executables[m_Executables.Count - 1];
                    m_Executables.RemoveAt(m_Executables.Count - 1);
                } else {
                    m_scriptExecutable = null;
                }
            } else {
                if (SupportBreak(m_scriptExecutable.Block)) { m_Break = m_Breaks.Pop(); }
                if (SupportContinue(m_scriptExecutable.Block)) { m_Continue = m_Continues.Pop(); }
                if (SupportCase(m_scriptExecutable.Block)) { m_Case = m_Cases.Pop(); }
                if (block != ExecutableBlock.ForBegin && block != ExecutableBlock.ForEnd) {
                    m_scriptExecutable.EndStack();
                }
            }
            return executable;
        }
        //获取一个double常量的索引
        int GetConstDouble(double value) {
            var index = ConstDouble.IndexOf(value);
            if (index < 0) {
                index = ConstDouble.Count;
                ConstDouble.Add(value);
                return index;
            }
            return index;
        }
        //获取一个long常量的索引
        int GetConstLong(long value) {
            var index = ConstLong.IndexOf(value);
            if (index < 0) {
                index = ConstLong.Count;
                ConstLong.Add(value);
                return index;
            }
            return index;
        }
        //获取一个string常量的索引
        int GetConstString(string value) {
            var index = ConstString.IndexOf(value);
            if (index < 0) {
                index = ConstString.Count;
                ConstString.Add(value);
                return index;
            }
            return index;
        }
        ScriptInstructionCompiler AddScriptInstruction(Opcode opcode, int opvalue, int line = -1) {
            return m_scriptExecutable.AddScriptInstruction(opcode, opvalue, line == -1 ? PeekToken().SourceLine : line);
        }
        ScriptInstructionCompiler AddScriptInstructionWithoutValue(Opcode opcode, int line = -1) {
            return m_scriptExecutable.AddScriptInstruction(opcode, 0, line == -1 ? PeekToken().SourceLine : line);
        }
        //解析脚本
        public ScriptFunctionData Parse() {
            m_indexToken = 0;
            var executable = ParseStatementContext();
            return new ScriptFunctionData() {
                scriptInstructions = executable.ScriptInstructions,
                variableCount = executable.VariableCount,
                internalCount = executable.InternalCount,
                parameterCount = 0,
                param = false,
                internals = new int[0],
            };
        }
        //解析整个文件
        ScriptExecutable ParseStatementContext() { return ParseStatementBlock(ExecutableBlock.Context, false, true, TokenType.Finished); }
        //解析一个函数
        ScriptExecutable ParseStatementFunction() { return ParseStatementBlock(ExecutableBlock.Function, true, false, TokenType.RightBrace); }
        //解析一个代码块
        ScriptExecutable ParseStatementBlock() { return ParseStatementBlock(ExecutableBlock.Block, true, true, TokenType.RightBrace); }
        //解析一个代码块
        ScriptExecutable ParseStatementBlock(ExecutableBlock block) { return ParseStatementBlock(block, true, true, TokenType.RightBrace); }
        
        int IsParentVariable(string str) {
            int index = m_scriptExecutable.GetInternalIndex(str);
            if (index >= 0) { return index; }
            for (int i = m_Executables.Count - 1; i >= 0; --i) {
                var executable = m_Executables[i];
                index = executable.GetInternalIndex(str);
                if (index < 0) {
                    index = executable.GetParentInternalIndex(str);
                }
                if (index >= 0) {
                    for (int j = i + 1; j < m_Executables.Count; ++j) {
                        index = m_Executables[j].AddInternalIndex(str, index);
                    }
                    return m_scriptExecutable.AddInternalIndex(str, index);
                }
            }
            return -1;
        }
        /// <summary>
        /// 解析代码块
        /// </summary>
        /// <param name="block">类型</param>
        /// <param name="readLeftBrace">是否需要 { </param>
        /// <param name="finished">结尾 token 类型</param>
        /// <returns></returns>
        ScriptExecutable ParseStatementBlock(ExecutableBlock block, bool readLeftBrace, bool beginExcutable, TokenType finished) {
            if (beginExcutable) { BeginExecutable(block); }
            if (readLeftBrace) { ReadLeftBrace(); }
            TokenType tokenType;
            while (HasMoreTokens()) {
                tokenType = ReadToken().Type;
                if (tokenType == finished) { break; }
                UndoToken();
                ParseStatement();
            }
            return EndExecutable(block);
        }
        //解析单据代码内容
        void ParseStatement() {
            var token = ReadToken();
            switch (token.Type) {
                case TokenType.Var:
                    ParseVar();
                    return;
                case TokenType.Identifier:
                case TokenType.String:
                case TokenType.Boolean: 
                case TokenType.Number:
                    ParseExpression();
                    return;
                case TokenType.LeftBrace:
                    ParseBlock();
                    return;
                case TokenType.If:
                    ParseIf();
                    return;
                case TokenType.For:
                    ParseFor();
                    return;
                case TokenType.Foreach:
                    ParseForeach();
                    return;
                case TokenType.While:
                    ParseWhile();
                    return;
                case TokenType.Class:
                    ParseClass();
                    return;
                case TokenType.Function:
                case TokenType.Sharp:
                    ParseFunction();
                    return;
                case TokenType.Return:
                    ParseReturn();
                    return;
                case TokenType.Break:
                    if (BlockSupportBreak) {
                        m_Breaks.Peek().Add(AddScriptInstructionWithoutValue(Opcode.Jump, token.SourceLine));
                    } else {
                        throw new ParserException(this, "当前代码块不支持 break 操作", token);
                    }
                    return;
                case TokenType.Continue:
                    if (BlockSupportContinue) {
                        m_Continues.Peek().Add(AddScriptInstructionWithoutValue(Opcode.Jump, token.SourceLine));
                    } else {
                        throw new ParserException(this, "当前代码块不支持 continue 操作", token);
                    }
                    return;
                case TokenType.Switch:
                    ParseSwtich();
                    return;
                case TokenType.Case:
                    ParseCase();
                    return;
                case TokenType.Default:
                    ParseDefault();
                    break;
                //    case TokenType.Try:
                //        ParseTry();
                //        break;
                //    case TokenType.Throw:
                //        ParseThrow();
                //        break;
                case TokenType.SemiColon: return;
                default: throw new ParserException(this, "不支持的语法 ", token);
            }
        }
        //解析Var关键字
        void ParseVar() {
            m_scriptExecutable.AddIndex(ReadIdentifier());
            while (true) {
                switch (PeekToken().Type) {
                    case TokenType.Comma: {
                        ReadToken();
                        m_scriptExecutable.AddIndex(ReadIdentifier());
                        break;
                    }
                    case TokenType.Assign: {
                        UndoToken();
                        return;
                    }
                    default: return;
                }
            }
        }
        //解析区域块{}
        void ParseBlock() {
            UndoToken();
            ParseStatementBlock();
        }
        //解析if(判断语句)
        void ParseIf() {
            var gotos = new List<ScriptInstructionCompiler>();
            ParseCondition(true, gotos);
            for (; ; ) {
                var token = ReadToken();
                if (token.Type == TokenType.ElseIf) {
                    ParseCondition(true, gotos);
                } else if (token.Type == TokenType.Else && PeekToken().Type == TokenType.If) {
                    ReadToken();
                    ParseCondition(true, gotos);
                } else {
                    UndoToken();
                    break;
                }
            }
            if (PeekToken().Type == TokenType.Else) {
                ReadToken();
                ParseCondition(false, gotos);
            }
            gotos.SetValue(Index);
        }
        //解析 if 单个判断模块
        void ParseCondition(bool condition, List<ScriptInstructionCompiler> gotos) {
            ScriptInstructionCompiler allow = null;
            if (condition) {
                ReadLeftParenthesis();
                PushObject(GetObject());
                ReadRightParenthesis();
                allow = AddScriptInstructionWithoutValue(Opcode.FalseTo, PeekToken().SourceLine);
            }
            ParseStatementBlock(ExecutableBlock.If);
            gotos.Add(AddScriptInstructionWithoutValue(Opcode.Jump, PeekToken().SourceLine));
            if (allow != null) {
                allow.SetValue(Index);
            }
        }
        //解析for语句
        void ParseFor() {
            var startIndex = m_indexToken;
            ReadLeftParenthesis();
            if (PeekToken().Type == TokenType.Var) ReadToken();
            if (PeekToken().Type == TokenType.Identifier) {
                var identifier = ReadIdentifier();
                if (ReadToken().Type == TokenType.Assign) {
                    var obj = GetObject();
                    if (ReadToken().Type == TokenType.Comma) {
                        ParseForSimple(identifier, obj);
                        return;
                    }
                }
            }
            m_indexToken = startIndex;
            ParseForNormal();
        }
        void ParseForSimple(string identifier, CodeObject obj) {
            var line = GetSourceLine();
            m_scriptExecutable.BeginStack();
            var index = m_scriptExecutable.AddIndex(identifier);
            PushObject(obj);
            AddScriptInstruction(Opcode.StoreLocal, index, line);
            PushObject(GetObject());
            if (ReadToken().Type == TokenType.Comma) {
                PushObject(GetObject());
            } else {
                UndoToken();
                AddScriptInstruction(Opcode.LoadConstDouble, GetConstDouble(1), line);
            }
            var startIndex = AddScriptInstruction(Opcode.CopyStackTopIndex, 1, line);
            AddScriptInstruction(Opcode.LoadLocal, index, line);
            AddScriptInstructionWithoutValue(Opcode.GreaterOrEqual, line);
            var falseTo = AddScriptInstruction(Opcode.FalseTo, 0, line);
            ReadRightParenthesis();
            ParseStatementBlock(ExecutableBlock.For);
            AddScriptInstructionWithoutValue(Opcode.CopyStackTop, line);
            AddScriptInstruction(Opcode.LoadLocal, index, line);
            AddScriptInstructionWithoutValue(Opcode.Plus, line);
            AddScriptInstruction(Opcode.StoreLocal, index, line);
            AddScriptInstruction(Opcode.Jump, startIndex.index, line);
            m_scriptExecutable.EndStack();
            var endIndex = Index;
            AddScriptInstruction(Opcode.PopNumber, 2, line);     //弹出栈顶的 end 和 step
            falseTo.SetValue(endIndex);
            m_Continue.SetValue(startIndex);
            m_Break.SetValue(endIndex);
        }
        //正常for循环  for(;;)
        void ParseForNormal() {
            ReadLeftParenthesis();
            var token = ReadToken();
            var endStackCount = 0;
            //首先解析第一部分
            if (token.Type != TokenType.SemiColon) {
                UndoToken();
                ParseStatementBlock(ExecutableBlock.ForBegin, false, true, TokenType.SemiColon);
                endStackCount += 1;
            }
            //第二部分开始索引
            var startIndex = Index;
            //如果是false就跳出循环
            ScriptInstructionCompiler falseTo = null;
            {
                token = ReadToken();
                //开始解析第二部分
                if (token.Type != TokenType.SemiColon) {
                    UndoToken();
                    PushObject(GetObject());
                    ReadSemiColon();
                    falseTo = AddScriptInstructionWithoutValue(Opcode.FalseTo, token.SourceLine);     //如果是false则跳转到循环结尾
                }
            }
            //判断完直接跳到for循环主逻辑
            var block = AddScriptInstructionWithoutValue(Opcode.Jump, token.SourceLine);
            //第三部分开始索引
            var forIndex = Index;
            {
                token = ReadToken();
                if (token.Type != TokenType.RightPar) {
                    UndoToken();
                    ParseStatementBlock(ExecutableBlock.ForEnd, false, true, TokenType.RightPar);
                    endStackCount += 1;
                }
                AddScriptInstruction(Opcode.Jump, startIndex, token.SourceLine);      //执行完第三部分跳转到第二部分的判断
            }
            block.SetValue(Index);
            ParseStatementBlock(ExecutableBlock.For);
            AddScriptInstruction(Opcode.Jump, forIndex, token.SourceLine);        //执行完主逻辑跳转到第三部分逻辑
            //for结束索引
            var endIndex = Index;
            if (falseTo != null) {
                falseTo.SetValue(endIndex);
            }
            m_Continue.SetValue(forIndex);
            m_Break.SetValue(endIndex);
            for (var i = 0; i < endStackCount; ++i) {
                m_scriptExecutable.EndStack();
            }
        }
        //解析while（循环语句）
        void ParseWhile() {
            var startIndex = Index;
            ReadLeftParenthesis();
            PushObject(GetObject());
            ReadRightParenthesis();
            var allow = AddScriptInstructionWithoutValue(Opcode.FalseTo, PeekToken().SourceLine);
            ParseStatementBlock(ExecutableBlock.While);
            AddScriptInstruction(Opcode.Jump, startIndex, PeekToken().SourceLine);
            m_Continue.SetValue(startIndex);
            var endIndex = Index;
            allow.SetValue(endIndex);
            m_Break.SetValue(endIndex);
        }
        //解析swtich语句
        void ParseSwtich() {
            ReadLeftParenthesis();
            PushObject(GetObject());
            PushObject(new CodeNativeObject(false, PeekToken().SourceLine));
            ReadRightParenthesis();
            ParseStatementBlock(ExecutableBlock.Switch);
            var endIndex = Index;
            AddScriptInstruction(Opcode.PopNumber, 2);       //弹出switch值 和 false 值
            m_Break.SetValue(endIndex);
            m_Case.SetValue(endIndex);
            //foreach (var instruction in m_Case) {
            //    instruction.SetValue(endIndex);
            //}
        }
        //解析case
        void ParseCase() {
            foreach (var instruction in m_Cases.Peek()) {
                instruction.SetValue(Index);
            }
            m_Cases.Peek().Clear();
            var leftIndex = AddScriptInstructionWithoutValue(Opcode.TrueLoadTrue);
            AddScriptInstructionWithoutValue(Opcode.CopyStackTop);
            PushObject(GetObject());
            ReadColon();
            AddScriptInstructionWithoutValue(Opcode.Equal);
            leftIndex.SetValue(Index);
            m_Cases.Peek().Add(AddScriptInstructionWithoutValue(Opcode.FalseLoadFalse));
            PushObject(new CodeNativeObject(true, PeekToken().SourceLine));
        }
        void ParseDefault() {
            foreach (var instruction in m_Cases.Peek()) {
                instruction.SetValue(Index);
            }
            m_Cases.Peek().Clear();
            ReadColon();
        }
        //解析foreach语句
        void ParseForeach() {
            ReadLeftParenthesis();
            var line = PeekToken().SourceLine;
            if (PeekToken().Type == TokenType.Var) ReadToken();
            m_scriptExecutable.BeginStack();
            var varIndex = m_scriptExecutable.AddIndex(ReadIdentifier());
            ReadIn();
            PushObject(GetObject());
            ReadRightParenthesis();
            AddScriptInstructionWithoutValue(Opcode.CopyStackTop, line);
            AddScriptInstructionWithoutValue(Opcode.CopyStackTop, line);
            AddScriptInstruction(Opcode.StoreLocal, varIndex, line);
            AddScriptInstruction(Opcode.LoadValueString, GetConstString(ScriptConst.IteratorNext), line);
            var beginIndex = AddScriptInstruction(Opcode.CallEach, 0, line);
            var falseTo = AddScriptInstruction(Opcode.FalseTo, 0, line);
            ParseStatementBlock(ExecutableBlock.Foreach);
            AddScriptInstruction(Opcode.Jump, beginIndex.index, line);
            falseTo.SetValue(Index);                                //如果是循环完毕跳出的话要先弹出栈顶的null值           
            var endIndex = Index;
            AddScriptInstructionWithoutValue(Opcode.Pop, line);     //弹出栈顶的foreach 函数
            AddScriptInstructionWithoutValue(Opcode.Pop, line);     //弹出栈顶的foreach map
            m_Continue.SetValue(beginIndex);
            m_Break.SetValue(endIndex);
            m_scriptExecutable.EndStack();
        }
        //解析Class
        void ParseClass() {
            var className = "";
            var index = ParseClassContent(ref className);
            var sourceLine = PeekToken().SourceLine;
            AddScriptInstruction(Opcode.NewType, index, sourceLine);
            if (m_scriptExecutable.Block == ExecutableBlock.Context) {
                AddScriptInstruction(Opcode.StoreGlobalString, GetConstString(className), sourceLine);
            } else {
                AddScriptInstruction(Opcode.StoreLocal, m_scriptExecutable.AddIndex(className), sourceLine);
            }
        }
        //解析函数（全局函数或类函数）
        void ParseFunction() {
            UndoToken();
            var functionName = "";
            var index = ParseFunctionContent(true, ref functionName);
            var sourceLine = PeekToken().SourceLine;
            AddScriptInstruction(Opcode.NewFunction, index, sourceLine);
            if (m_scriptExecutable.Block == ExecutableBlock.Context) {
                AddScriptInstruction(Opcode.StoreGlobalString, GetConstString(functionName), sourceLine);
            } else {
                AddScriptInstruction(Opcode.StoreLocal, m_scriptExecutable.AddIndex(functionName), sourceLine);
            }
        }
        //解析return
        void ParseReturn() {
            var peek = PeekToken();
            if (peek.Type == TokenType.RightBrace || peek.Type == TokenType.SemiColon || peek.Type == TokenType.Finished) {
                AddScriptInstructionWithoutValue(Opcode.RetNone, peek.SourceLine);
            } else {
                PushObject(GetObject());
                AddScriptInstructionWithoutValue(Opcode.Ret, peek.SourceLine);
            }
        }
        //压入一个值
        void PushObject(CodeObject obj) {
            switch (obj) {
                case CodeNativeObject native: {
                    var value = native.obj;
                    if (value == null) {
                        AddScriptInstructionWithoutValue(Opcode.LoadConstNull, obj.Line);
                    } else if (value is bool) {
                        AddScriptInstructionWithoutValue((bool)value ? Opcode.LoadConstTrue : Opcode.LoadConstFalse, obj.Line);
                    } else if (value is double) {
                        AddScriptInstruction(Opcode.LoadConstDouble, GetConstDouble((double)value), obj.Line);
                    } else if (value is long) {
                        AddScriptInstruction(Opcode.LoadConstLong, GetConstLong((long)value), obj.Line);
                    } else if (value is string) {
                        AddScriptInstruction(Opcode.LoadConstString, GetConstString((string)value), obj.Line);
                    }
                    break;
                }
                case CodeMember member: {
                    if (member.Parent == null) {
                        if (obj is CodeMemberIndex) {
                            AddScriptInstruction(Opcode.LoadLocal, member.index, obj.Line);
                        } else if (obj is CodeMemberInternal) {
                            AddScriptInstruction(Opcode.LoadInternal, member.index, obj.Line);
                        } else if (obj is CodeMemberString) {
                            AddScriptInstruction(Opcode.LoadGlobalString, GetConstString(member.key), obj.Line);
                        }
                    } else {
                        PushObject(member.Parent);
                        var nullTo = member.nullTo ? AddScriptInstruction(Opcode.NullTo, 0) : null;
                        if (obj is CodeMemberIndex) {
                            AddScriptInstruction(Opcode.LoadValue, member.index, obj.Line);
                        } else if (obj is CodeMemberString) {
                            AddScriptInstruction(Opcode.LoadValueString, GetConstString(member.key), obj.Line);
                        } else if (obj is CodeMemberObject) {
                            PushObject(member.codeKey);
                            AddScriptInstructionWithoutValue(Opcode.LoadValueObject, obj.Line);
                        }
                        nullTo?.SetValue(Index);
                    }
                    break;
                }
                case CodeOperator oper: {
                    if (oper.TokenType == TokenType.And) {
                        PushObject(oper.Left);
                        var leftIndex = AddScriptInstructionWithoutValue(Opcode.FalseLoadFalse, obj.Line);
                        PushObject(oper.Right);
                        leftIndex.SetValue(Index);
                    } else if (oper.TokenType == TokenType.Or) {
                        PushObject(oper.Left);
                        var leftIndex = AddScriptInstructionWithoutValue(Opcode.TrueLoadTrue, obj.Line);
                        PushObject(oper.Right);
                        leftIndex.SetValue(Index);
                    } else {
                        PushObject(oper.Left);
                        PushObject(oper.Right);
                        AddScriptInstructionWithoutValue(TempOperator.GetOpcode(oper.TokenType), obj.Line);
                    }
                    break;
                }
                case CodeRegion region: {
                    PushObject(region.Context);
                    break;
                }
                case CodeFunction func: {
                    if (func.lambada) {
                        AddScriptInstruction(Opcode.NewLambadaFunction, func.func, obj.Line);
                    } else {
                        AddScriptInstruction(Opcode.NewFunction, func.func, obj.Line);
                    }
                    break;
                }
                case CodeCallFunction func: {
                    var unfold = 0L;
                    for (var i = 0; i < func.Parameters.Count; ++i) {
                        var parameter = func.Parameters[i];
                        PushObject(parameter.obj);
                        if (parameter.unfold) {
                            unfold |= 1L << i;
                        }
                    }
                    PushObject(func.Member);
					var nullTo = func.nullTo ? AddScriptInstruction(Opcode.NullTo, 0) : null;
                    if (unfold == 0L) {
                        AddScriptInstruction(IsVariableFunction(func.Member) != null ? Opcode.CallVi : Opcode.Call, func.Parameters.Count, obj.Line);
                    } else {
                        var index = System.Convert.ToInt64(func.Parameters.Count) << 8 | unfold;
                        AddScriptInstruction(IsVariableFunction(func.Member) != null ? Opcode.CallViUnfold : Opcode.CallUnfold, GetConstLong(index), obj.Line);
                    }
                    if (func.Variables != null) {
                        foreach (var variable in func.Variables.Variables) {
                            if (variable.key is string) {
                                AddScriptInstructionWithoutValue(Opcode.CopyStackTop, obj.line);
                                PushObject(variable.value);
                                AddScriptInstruction(Opcode.StoreValueString, GetConstString(variable.key.ToString()), obj.Line);
                            }
                        }
                    }
                    nullTo?.SetValue(Index);
                    break;
                }
                case CodeClass cl: {
                    AddScriptInstruction(Opcode.NewType, cl.index, obj.Line);
                    break;
                }
                case CodeArray array: {
                    foreach (var ele in array.Elements) {
                        PushObject(ele);
                    }
                    AddScriptInstruction(Opcode.NewArray, array.Elements.Count, obj.Line);
                    break;
                }
                case CodeMap map: {
                    foreach (var ele in map.Variables) {
                        if (ele.value == null) {
                            AddScriptInstructionWithoutValue(Opcode.LoadConstNull, obj.Line);
                        } else {
                            PushObject(ele.value);
                        }
                    }
                    var hadObjectKey = false;
                    foreach (var ele in map.Variables) {
                        if (ele.key is string) {
                            AddScriptInstruction(Opcode.LoadConstString, GetConstString(ele.key.ToString()), obj.Line);
                        } else if (ele.key is double) {
                            hadObjectKey = true;
                            AddScriptInstruction(Opcode.LoadConstDouble, GetConstDouble((double)ele.key), obj.Line);
                        } else if (ele.key is long) {
                            hadObjectKey = true;
                            AddScriptInstruction(Opcode.LoadConstLong, GetConstLong((long)ele.key), obj.Line);
                        } else if (ele.key is bool) {
                            hadObjectKey = true;
                            AddScriptInstructionWithoutValue(((bool)ele.key) ? Opcode.LoadConstTrue : Opcode.LoadConstFalse, obj.Line);
                        } else {
                            throw new ParserException(this, "未知的map key 类型 : " + ele.key.GetType());
                        }
                    }
                    AddScriptInstruction(hadObjectKey ? Opcode.NewMapObject : Opcode.NewMap, map.Variables.Count, obj.Line);
                    break;
                }
                case CodeTernary ternary: {
                    PushObject(ternary.Allow);
                    var falseTo = AddScriptInstructionWithoutValue(Opcode.FalseTo, obj.Line);
                    PushObject(ternary.True);
                    var jump = AddScriptInstructionWithoutValue(Opcode.Jump, obj.Line);
                    falseTo.SetValue(Index);
                    PushObject(ternary.False);
                    jump.SetValue(Index);
                    break;
                }
                case CodeEmptyRet emptyRet: {
                    PushObject(emptyRet.Emtpy);
                    var emptyTo = AddScriptInstructionWithoutValue(Opcode.NotNullTo, obj.Line);
                    PushObject(emptyRet.Ret);
                    emptyTo.SetValue(Index);
                    break;
                }
                case CodeAssign assign: {
                    PushAssign(assign);
                    break;
                }
                default: throw new ParserException(this, "不支持的语法 : " + obj);
            }
            if (obj.Not) {
                AddScriptInstructionWithoutValue(Opcode.FlagNot, obj.Line);
            } else if (obj.Minus) {
                AddScriptInstructionWithoutValue(Opcode.FlagMinus, obj.Line);
            } else if (obj.Negative) {
                AddScriptInstructionWithoutValue(Opcode.FlagNegative, obj.Line);
            }
        }
        CodeObject IsVariableFunction(CodeObject member) {
            while (true) {
                if (member is CodeMember) {
                    return (member as CodeMember).Parent;
                } else if (member is CodeCallFunction) {
                    member = (member as CodeCallFunction).Member;
                } else {
                    return null;
                }
            }
        }
        //压入一个无返回值的赋值公式
        void PushAssign(CodeAssign assign) {
            var member = assign.member;
            var value = assign.value;
            var index = member.index;
            var line = member.line;
            // = 操作
            if (assign.AssignType == TokenType.Assign) {
                if (member.Parent == null) {
                    PushObject(value);
                    if (member is CodeMemberIndex) {
                        AddScriptInstruction(Opcode.StoreLocalAssign, index, line);
                    } else if (member is CodeMemberInternal) {
                        AddScriptInstruction(Opcode.StoreInternalAssign, index, line);
                    } else if (member is CodeMemberString) {
                        AddScriptInstruction(Opcode.StoreGlobalStringAssign, GetConstString(member.key), line);
                    }
                } else {
                    PushObject(member.Parent);
                    if (member is CodeMemberIndex) {
                        PushObject(value);
                        AddScriptInstruction(Opcode.StoreValueAssign, index, line);
                    } else if (member is CodeMemberString) {
                        PushObject(value);
                        AddScriptInstruction(Opcode.StoreValueStringAssign, GetConstString(member.key), line);
                    } else {
                        PushObject(member.codeKey);
                        PushObject(value);
                        AddScriptInstructionWithoutValue(Opcode.StoreValueObjectAssign, line);
                    }
                }
            // += -= 等计算赋值操作
            } else {
                var opcode = TempOperator.GetOpcode(assign.AssignType);
                if (member.Parent == null) {
                    if (member is CodeMemberIndex) {
                        AddScriptInstruction(Opcode.LoadLocal, index, line);
                        PushObject(value);
                        AddScriptInstructionWithoutValue(opcode, line);
                        AddScriptInstruction(Opcode.StoreLocalAssign, index, line);
                    } else if (member is CodeMemberInternal) {
                        AddScriptInstruction(Opcode.LoadInternal, index, line);
                        PushObject(value);
                        AddScriptInstructionWithoutValue(opcode, line);
                        AddScriptInstruction(Opcode.StoreInternalAssign, index, line);
                    } else if (member is CodeMemberString) {
                        AddScriptInstruction(Opcode.LoadGlobalString, GetConstString(member.key), line);
                        PushObject(value);
                        AddScriptInstructionWithoutValue(opcode, line);
                        AddScriptInstruction(Opcode.StoreGlobalStringAssign, GetConstString(member.key), line);
                    }
                } else {
                    PushObject(member.Parent);
                    if (member is CodeMemberIndex) {
                        AddScriptInstructionWithoutValue(Opcode.CopyStackTop, line);
                        AddScriptInstruction(Opcode.LoadValue, index, line);
                        PushObject(value);
                        AddScriptInstructionWithoutValue(opcode, line);
                        AddScriptInstruction(Opcode.StoreValueAssign, index, line);
                    } else if (member is CodeMemberString) {
                        AddScriptInstructionWithoutValue(Opcode.CopyStackTop, line);
                        AddScriptInstruction(Opcode.LoadValueString, GetConstString(member.key), line);
                        PushObject(value);
                        AddScriptInstructionWithoutValue(opcode, line);
                        AddScriptInstruction(Opcode.StoreValueStringAssign, GetConstString(member.key), line);
                    } else {
                        PushObject(member.codeKey);
                        AddScriptInstructionWithoutValue(Opcode.LoadValueObjectDup, line);
                        PushObject(value);
                        AddScriptInstructionWithoutValue(opcode, line);
                        AddScriptInstructionWithoutValue(Opcode.StoreValueObjectAssign, line);
                    }
                }
            }
        }
        //解析表达式
        void ParseExpression() {
            UndoToken();
            var member = GetObject();
            if (member is CodeAssign) {
                PushAssign(member as CodeAssign);
                AddScriptInstructionWithoutValue(Opcode.Pop);                   //弹出赋值的返回值
            } else if (member is CodeCallFunction) {
                PushObject(member);
                AddScriptInstructionWithoutValue(Opcode.Pop, member.line);      //弹出call的返回值
            } else {
                throw new ParserException(this, "语法错误 " + member.GetType(), PeekToken());
            }
        }
        //获取一个Object
        CodeObject GetObject() {
            Stack<TempOperator> operateStack = new Stack<TempOperator>();
            Stack<CodeObject> objectStack = new Stack<CodeObject>();
            int line = GetSourceLine();
            while (true) {
                objectStack.Push(GetOneObject());
                if (!P_Operator(operateStack, objectStack))
                    break;
            }
            while (operateStack.Count > 0) {
                objectStack.Push(new CodeOperator(objectStack.Pop(), objectStack.Pop(), operateStack.Pop().Operator, line));
            }
            CodeObject ret = objectStack.Pop();
            if (ret is CodeMember) {
                CodeMember member = ret as CodeMember;
                Token token = ReadToken();
                switch (token.Type) {
                    case TokenType.Assign:
                    case TokenType.PlusAssign:
                    case TokenType.MinusAssign:
                    case TokenType.MultiplyAssign:
                    case TokenType.DivideAssign:
                    case TokenType.ModuloAssign:
                    case TokenType.CombineAssign:
                    case TokenType.InclusiveOrAssign:
                    case TokenType.XORAssign:
                    case TokenType.ShrAssign:
                    case TokenType.ShiAssign:
                        ret = new CodeAssign(member, GetObject(), token.Type, token.SourceLine);
                        break;
                    default:
                        UndoToken();
                        break;
                }
            }
            switch (PeekToken().Type) {
                case TokenType.QuestionMark: {
                    ReadToken();
                    var ternary = new CodeTernary(GetSourceLine());
                    ternary.Allow = ret;
                    ternary.True = GetObject();
                    ReadColon();
                    ternary.False = GetObject();
                    return ternary;
                }
                case TokenType.EmptyRet: {
                    ReadToken();
                    var emptyRet = new CodeEmptyRet(GetSourceLine());
                    emptyRet.Emtpy = ret;
                    emptyRet.Ret = GetObject();
                    return emptyRet;
                }
                default: return ret;
            }
        }
        //解析操作符
        bool P_Operator(Stack<TempOperator> operateStack, Stack<CodeObject> objectStack) {
            TempOperator curr = TempOperator.GetOperator(PeekToken().Type);
            if (curr == null) return false;
            ReadToken();
            while (operateStack.Count > 0) {
                if (operateStack.Peek().Level >= curr.Level) {
                    objectStack.Push(new CodeOperator(objectStack.Pop(), objectStack.Pop(), operateStack.Pop().Operator, GetSourceLine()));
                } else {
                    break;
                }
            }
            operateStack.Push(curr);
            return true;
        }
        //获得单一变量
        CodeObject GetOneObject() {
            Token token = ReadToken();
            byte flag = 0;
            if (token.Type == TokenType.Not) {
                flag = 1;
                token = ReadToken();
            } else if (token.Type == TokenType.Minus) {
                flag = 2;
                token = ReadToken();
            } else if (token.Type == TokenType.Negative) {
                flag = 3;
                token = ReadToken();
            }
            CodeObject ret;
            switch (token.Type) {
                case TokenType.Identifier: {
                    var key = token.Lexeme as string;
                    if (m_scriptExecutable.HasIndex(key)) {
                        ret = new CodeMemberIndex(m_scriptExecutable.GetIndex(key), token.SourceLine);
                    } else {
                        int index = IsParentVariable(key);
                        if (index >= 0) {
                            ret = new CodeMemberInternal(index, token.SourceLine);
                        } else {
                            ret = new CodeMemberString(key, token.SourceLine);
                        }
                    }
                    break;
                }
                case TokenType.Function:
                case TokenType.Sharp: {
                    UndoToken();
                    var functionName = "";
                    ret = new CodeFunction(ParseFunctionContent(true, ref functionName), token.SourceLine);
                    break;
                }
                case TokenType.Class: {
                    var className = "";
                    ret = new CodeClass(ParseClassContent(ref className), token.SourceLine);
                    break;
                }
                case TokenType.LeftPar: {
                    ret = GetRegionOrFunction();
                    break;
                }
                case TokenType.Null:
                case TokenType.Boolean:
                case TokenType.Number:
                case TokenType.String: {
                    ret = new CodeNativeObject(token.Lexeme, token.SourceLine);
                    break;
                }
                case TokenType.LeftBracket: {
                    UndoToken();
                    ret = GetArray();
                    break;
                }
                case TokenType.LeftBrace: {
                    UndoToken();
                    ret = GetMap();
                    break;
                }
                default: throw new ParserException(this, "Object起始关键字错误 ", token);
            }
            ret.line = token.SourceLine;
            ret = GetVariable(ret);
            if ((ret is CodeNativeObject || (ret is CodeRegion && (ret as CodeRegion).Context is CodeNativeObject)) && flag != 0) {
                var scriptObject = ret is CodeNativeObject ? (ret as CodeNativeObject) : (ret as CodeRegion).Context as CodeNativeObject;
                var obj = scriptObject.obj;
                if (obj is bool) {
                    if (flag == 1) {
                        scriptObject.obj = !((bool)obj);
                    } else {
                        throw new ParserException(this, "bool 不支持 [-][~] 符号", token);
                    }
                } else if (obj is double) {
                    if (flag == 2) {
                        scriptObject.obj = -(double)obj;
                    } else {
                        throw new ParserException(this, "double 不支持 [!][~] 符号", token);
                    }
                } else if (obj is long) {
                    if (flag == 2) {
                        scriptObject.obj = -(long)obj;
                    } else if (flag == 3) {
                        scriptObject.obj = ~(long)obj;
                    } else {
                        throw new ParserException(this, "long 不支持 [!] 符号", token);
                    }
                } else {
                    throw new ParserException(this, "数据类型不支持 [!][-][~] 符号", token);
                }
                ret = scriptObject;
            } else {
                ret.Not = flag == 1;
                ret.Minus = flag == 2;
                ret.Negative = flag == 3;
            }
            return ret;
        }
        //返回变量数据
        CodeObject GetVariable(CodeObject parent) {
            CodeObject ret = parent;
            for (; ; ) {
                Token token = ReadToken();
                if (token.Type == TokenType.Period) {
                    ret = ReadMember(TokenType.Period, ret, token.SourceLine, false);
                } else if (token.Type == TokenType.LeftBracket) {
                    ret = ReadMember(TokenType.LeftBracket, ret, token.SourceLine, false);
                } else if (token.Type == TokenType.LeftPar) {
                    UndoToken();
                    ret = ReadCallFunction(ret, false);
                } else if (token.Type == TokenType.QuestionMarkDot) {
                    var next = PeekToken();
                    if (next.Type == TokenType.LeftBracket) {
                        ReadToken();
                        ret = ReadMember(TokenType.LeftBracket, ret, token.SourceLine, true);
                    } else if (next.Type == TokenType.LeftPar) {
                        ret = ReadCallFunction(ret, true);
                    } else {
                        ret = ReadMember(TokenType.Period, ret, token.SourceLine, true);
                    }
                } else {
                    UndoToken();
                    break;
                }
                ret.line = token.SourceLine;
            }
            return ret;
        }
        CodeMember ReadMember(TokenType type, CodeObject parent, int line, bool nullTo) {
            if (type == TokenType.Period) {
                return new CodeMemberString(ReadIdentifier(), parent, line) { nullTo = nullTo };
            } else if (type == TokenType.LeftBracket) {
                var member = GetObject();
                ReadRightBracket();
                var stringKey = (member as CodeNativeObject)?.obj as string;
                if (stringKey != null) {
                    return new CodeMemberString(stringKey, parent, line) { nullTo = nullTo };
                } else {
                    return new CodeMemberObject(member, parent, line) { nullTo = nullTo };
                }
            }
            return null;
        }
        CodeCallFunction ReadCallFunction(CodeObject parent, bool nullTo) {
            var ret = GetCallFunction(parent);
            ret.nullTo = nullTo;
            if (PeekToken().Type == TokenType.LeftBrace) {
                ret.Variables = GetMap();
            }
            return ret;
        }
        //返回一个调用函数 Object
        CodeCallFunction GetCallFunction(CodeObject member) {
            ReadLeftParenthesis();
            var paramters = new List<CodeFunctionParameter>();
            var token = PeekToken();
            while (token.Type != TokenType.RightPar) {
                var obj = GetObject();
                token = PeekToken();
                var spread = false;
                if (token.Type == TokenType.Params) {
                    spread = true;
                    ReadToken();
                    token = PeekToken();
                }
                paramters.Add(new CodeFunctionParameter() { unfold = spread, obj = obj } );
                if (token.Type == TokenType.Comma)
                    ReadComma();
                else if (token.Type == TokenType.RightPar)
                    break;
                else
                    throw new ParserException(this, "Comma ',' or right parenthesis ')' expected in function declararion.", token);
            }
            ReadRightParenthesis();
            return new CodeCallFunction(member, paramters, token.SourceLine);
        }
        CodeObject GetRegionOrFunction() {
            UndoToken();
            var index = m_indexToken;
            ReadLeftParenthesis();
            var token = ReadToken();
            if (token.Type != TokenType.RightPar) {
                while (true) {
                    token = ReadToken();
                    if (token.Type == TokenType.RightPar)
                        break;
                }
            }
            if (PeekToken().Type == TokenType.Lambda) {
                m_indexToken = index;
                var functionName = "";
                return new CodeFunction(ParseFunctionContent(false, ref functionName), true, token.SourceLine);
            } else {
                m_indexToken = index;
                ReadLeftParenthesis();
                var ret = new CodeRegion(GetObject(), token.SourceLine);
                ReadRightParenthesis();
                return ret;
            }
        }
        //返回数组
        CodeArray GetArray() {
            ReadLeftBracket();
            var token = PeekToken();
            var ret = new CodeArray(token.SourceLine);
            while (token.Type != TokenType.RightBracket) {
                if (PeekToken().Type == TokenType.RightBracket)
                    break;
                ret.Elements.Add(GetObject());
                token = PeekToken();
                if (token.Type == TokenType.Comma || token.Type == TokenType.SemiColon) {
                    ReadToken();
                } else if (token.Type == TokenType.RightBracket) {
                    break;
                } else
                    throw new ParserException(this, "Comma ',' or SemiColon ';' or right parenthesis ']' expected in array object.", token);
            }
            ReadRightBracket();
            return ret;
        }
        //返回map数据
        CodeMap GetMap() {
            var ret = new CodeMap(GetSourceLine());
            ReadLeftBrace();
            while (PeekToken().Type != TokenType.RightBrace) {
                var token = ReadToken();
                switch (token.Type) {
                    case TokenType.Comma:
                    case TokenType.SemiColon:
                        continue;
                    case TokenType.Identifier:
                    case TokenType.String:
                    case TokenType.Number:
                    case TokenType.Boolean: {
                        var next = ReadToken();
                        if (next.Type == TokenType.Assign || next.Type == TokenType.Colon) {
                            ret.Variables.Add(new CodeMap.MapVariable(token.Lexeme, GetObject()));
                        } else if (next.Type == TokenType.Comma || next.Type == TokenType.SemiColon) {
                            ret.Variables.Add(new CodeMap.MapVariable(token.Lexeme, null));
                        } else if ((token.Type == TokenType.Identifier || token.Type == TokenType.String) && next.Type == TokenType.LeftPar) {
                            UndoToken();
                            UndoToken();
                            string functionName = "";
                            var index = ParseFunctionContent(false, ref functionName);
                            ret.Variables.Add(new CodeMap.MapVariable(functionName, new CodeFunction(index, token.SourceLine)));
                        } else {
                            throw new ParserException(this, "Map变量赋值符号为[=]或者[:]", token);
                        }
                        continue;
                    }
                    case TokenType.Function:
                    case TokenType.Sharp: {
                        UndoToken();
                        string functionName = "";
                        var index = ParseFunctionContent(true, ref functionName);
                        ret.Variables.Add(new CodeMap.MapVariable(functionName, new CodeFunction(index, token.SourceLine)));
                        break;
                    }
                    default: throw new ParserException(this, "Map开始关键字必须为[变量名称]或者[function]关键字", token);
                }
            }
            ReadRightBrace();
            return ret;
        }
        /// <summary>
        /// 解析一个函数
        /// </summary>
        /// <param name="needKeyword">是否需要function,#关键字</param>
        /// <param name="functionName">返回函数的名字</param>
        /// <returns></returns>
        int ParseFunctionContent(bool needKeyword, ref string functionName) {
            var token = ReadToken();
            if (token.Type != TokenType.Function && token.Type != TokenType.Sharp) {
                if (needKeyword) {
                    throw new ParserException(this, "Function declaration must start with the 'function' or '#' keyword.", token);
                } else {
                    UndoToken();
                }
            }
            if (PeekToken().Type == TokenType.Identifier || PeekToken().Type == TokenType.String) {
                functionName = ReadToken().Lexeme.ToString();        //函数名
            } else {
                functionName = $"{Breviary}:{PeekToken().SourceLine}";
            }
            var listParameters = new List<string>();    //参数列表(如果是变长参数，包含变长参数名字)
            var bParams = false;                        //是否是变长参数
            var peek = ReadToken();
            if (peek.Type == TokenType.LeftPar) {
                if (PeekToken().Type != TokenType.RightPar) {
                    while (true) {
                        token = ReadToken();
                        if (token.Type == TokenType.Params) {
                            token = ReadToken();
                            bParams = true;
                        }
                        if (token.Type != TokenType.Identifier) {
                            throw new ParserException(this, "Unexpected token in function declaration.", token);
                        }
                        //参数名字
                        var parameterName = token.Lexeme.ToString();
                        listParameters.Add(parameterName);
                        token = PeekToken();
                        if (token.Type == TokenType.Comma && !bParams)
                            ReadComma();
                        else if (token.Type == TokenType.RightPar)
                            break;
                        else
                            throw new ParserException(this, "Comma ',' or right parenthesis ')' expected in function declararion.", token);
                    }
                }
                ReadRightParenthesis();
                peek = ReadToken();
            }
            if (peek.Type == TokenType.LeftBrace) {
                UndoToken();
            }
            BeginExecutable(ExecutableBlock.Function);
            foreach (var par in listParameters) {
                AddScriptInstruction(Opcode.StoreLocal, m_scriptExecutable.AddIndex(par), token.SourceLine)  ;
            }
            var executable = ParseStatementFunction();
            var index = Functions.Count;
            Functions.Add(new ScriptFunctionData() {
                scriptInstructions = executable.ScriptInstructions,
                parameterCount = listParameters.Count,
                param = bParams,
                variableCount = executable.VariableCount,
                internalCount = executable.InternalCount,
                internals = executable.ScriptInternals,
            });
            return index;
        }
        int ParseClassContent(ref string className) {
            if (PeekToken().Type == TokenType.Identifier || PeekToken().Type == TokenType.String) {
                className = ReadToken().Lexeme.ToString();  //类名
            } else {
                className = $"{Breviary}:{PeekToken().SourceLine}";
            }
            var parent = "";
            if (PeekToken().Type == TokenType.Colon) {
                ReadToken();
                parent = ReadIdentifier();
            }
            var functions = new List<long>();           //所有的函数
            ReadLeftBrace();
            while (PeekToken().Type != TokenType.RightBrace) {
                var token = ReadToken();
                if (token.Type == TokenType.SemiColon) {
                    continue;
                }
                long nameIndex, funcIndex;
                var functionName = "";
                if (token.Type == TokenType.Identifier || token.Type == TokenType.String) {
                    var next = ReadToken();
                    if (next.Type == TokenType.LeftPar || next.Type == TokenType.LeftBrace) {
                        UndoToken();
                        UndoToken();
                        nameIndex = GetConstString(token.Lexeme.ToString());
                        funcIndex = ParseFunctionContent(false, ref functionName);
                    } else {
                        throw new ParserException(this, "Class 开始关键字必须为[变量名称]或者[function]关键字", token);
                    }
                } else if (token.Type == TokenType.Function || token.Type == TokenType.Sharp) {
                    UndoToken();
                    funcIndex = ParseFunctionContent(true, ref functionName);
                    nameIndex = GetConstString(functionName);
                } else {
                    throw new ParserException(this, "Class 开始关键字必须为[变量名称]或者[function]关键字", token);
                }
                functions.Add(nameIndex << 32 | funcIndex);
            }
            ReadRightBrace();
            var index = Classes.Count;
            Classes.Add(new ScriptClassData() {
                name = GetConstString(className),
                parent = parent.Length == 0 ? -1 : GetConstString(parent),
                functions = functions.ToArray(),
            });
            return index;
        }
        ////解析case
        //private void ParseCase(List<CodeObject> allow) {
        //    allow.Add(GetObject());
        //    ReadColon();
        //    if (ReadToken().Type == TokenType.Case) {
        //        ParseCase(allow);
        //    } else {
        //        UndoToken();
        //    }
        //}
        ////解析try catch
        //private void ParseTry() {
        //    CodeTry ret = new CodeTry();
        //    ret.TryExecutable = ParseStatementBlock(Executable_Block.Context);
        //    ReadCatch();
        //    ReadLeftParenthesis();
        //    ret.Identifier = ReadIdentifier();
        //    ReadRightParenthesis();
        //    ret.CatchExecutable = ParseStatementBlock(Executable_Block.Context);
        //    if (PeekToken().Type == TokenType.Finally) {
        //        ReadToken();
        //        ret.FinallyExecutable = ParseStatementBlock(Executable_Block.Context);
        //    }
        //    m_scriptExecutable.AddScriptInstruction(new ScriptInstruction(Opcode.CALL_TRY, ret));
        //}
        ////解析throw
        //private void ParseThrow() {
        //    CodeThrow ret = new CodeThrow();
        //    ret.obj = GetObject();
        //    m_scriptExecutable.AddScriptInstruction(new ScriptInstruction(Opcode.THROW, ret));
        //}
    }
}
