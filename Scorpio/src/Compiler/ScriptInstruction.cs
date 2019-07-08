﻿using System.Text;
namespace Scorpio.Compiler {
    //指令集大类型
    public enum OpcodeType : byte {
        None,       //无效类型
        Load,       //压栈
        New,        //new
        Store,      //取栈
        Compute,    //运算
        Compare,    //比较
        Jump,       //跳转
    }
    //指令类型
    public enum Opcode : byte {
        None,

        //压栈操作
        LoadBegin,
        LoadConstNull,          //push null
        LoadConstDouble,        //push double
        LoadConstLong,          //push long
        LoadConstString,        //push string
        LoadConstFalse,         //push false
        LoadConstTrue,          //push true
        LoadLocal,              //push a local value by index
        LoadInternal,           //push a internal value
        LoadGlobal,             //push a global value by index
        LoadGlobalString,       //push a global value by string
        LoadFunction,           //push a function
        LoadValue,              //push a value by index
        LoadValueString,        //push a value by string
        LoadValueObject,        //push a value by object
        LoadValueObjectDup,     //push a value by object
        CopyStackTop,           //复制栈顶的数据
        CopyStackTopIndex,      //复制栈顶的数据
        LoadEnd,


        //New操作
        NewBegin,
        NewFunction,            //load a new function
        NewLambadaFunction,     //load a new lambada function
        NewArray,               //new array
        NewMap,                 //new map
        NewMapObject,           //new map with key contain object
        NewType,                //new class
        NewTypeParent,          //new class with parent
        NewEnd,

        //取栈操作
        StoreBegin,
        StoreLocal,             //store local value
        StoreInternal,          //store internal value
        StoreGlobal,            //store global value by index
        StoreGlobalString,      //store global value by string
        StoreValue,             //store a value by index
        StoreValueString,       //store a value by string
        StoreValueObject,       //store a value by object
        StoreEnd,

        //运算指令
        ComputeBegin,
        Plus,
        Minus,
        Multiply,
        Divide,
        Modulo,
        InclusiveOr,
        Combine,
        XOR,
        Shr,
        Shi,
        FlagNot,                //取反操作
        FlagMinus,              //取负操作
        FlagNegative,           //取非操作
        ComputeEnd,

        //比较指令
        CompareBegin,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        Equal,
        NotEqual,
        And,
        Or,
        CompareEnd,

        //跳转指令
        JumpBegin,
        Jump,                   //跳转到执行索引
        Pop,                    //弹出栈顶的值
        PopNumber,              //弹出一定数量的栈顶的值
        FalseTo,                //栈顶如果是false则跳转
        TrueTo,                 //栈顶如果是true则跳转
        FalseLoadFalse,         //如果是false则压入一个false
        TrueLoadTrue,           //如果是true则压入一个true
        CallEach,               //call a function when in foreach
        Call,                   //调用一个函数
        CallVi,                 //调用内部函数
        RetNone,                //return
        Ret,                    //return a value
        JumpEnd,
    }
    //不能使用 struct 编译时会稍后修改内部值 
    //单条指令
    public class ScriptInstruction {
        public OpcodeType optype;   //指令类型
        public Opcode opcode;       //指令类型
        public int opvalue;         //指令值
        public int line;            //代码在多少行
        public ScriptInstruction(Opcode opcode, int opvalue, int line) {
            this.optype = OpcodeType.None;
            this.opcode = opcode;
            this.opvalue = opvalue;
            this.line = line;
            SetOpcode(opcode, opvalue);
        }
        public void SetOpcode(Opcode opcode) {
            SetOpcode(opcode, opvalue);
        }
        public void SetOpcode(Opcode opcode, int opvalue) {
            this.opcode = opcode;
            this.opvalue = opvalue;
            if (opcode > Opcode.LoadBegin && opcode < Opcode.LoadEnd) {
                this.optype = OpcodeType.Load;
            } else if (opcode > Opcode.NewBegin && opcode < Opcode.NewEnd) {
                this.optype = OpcodeType.New;
            } else if (opcode > Opcode.StoreBegin && opcode < Opcode.StoreEnd) {
                this.optype = OpcodeType.Store;
            } else if (opcode > Opcode.ComputeBegin && opcode < Opcode.ComputeEnd) {
                this.optype = OpcodeType.Compute;
            } else if (opcode > Opcode.CompareBegin && opcode < Opcode.CompareEnd) {
                this.optype = OpcodeType.Compare;
            } else if (opcode > Opcode.JumpBegin && opcode < Opcode.JumpEnd) {
                this.optype = OpcodeType.Jump;
            }
        }
    }
    //单个函数的信息
    public class ScriptFunctionData {
        public ScriptInstruction[] scriptInstructions;      //命令列表
        public int parameterCount;                          //参数个数
        public bool param;                                  //是否是变长参数
        public int variableCount;                           //局部变量数量
        public int internalCount;                           //内部变量数量
        public int[] internals;                             //内部变量赋值 前16位为父级索引 后16为自己索引
        public string ToString(double[] constDouble, long[] constLong, string[] constString) {
            var builder = new StringBuilder();
            foreach (var inter in internals) {
                int source = (inter >> 16);
                int target = (inter & 0xffff);
                builder.AppendLine($"internal  {source} => {target}");
            }
            for (int i = 0; i < scriptInstructions.Length; ++i) {
                var instruction = scriptInstructions[i];
                var opcode = instruction.opcode.ToString();
                if (opcode.Length < 20) {
                    for (int j = opcode.Length; j < 20; ++j) {
                        opcode += " ";
                    }
                }
                builder.Append($"{i.ToString("D5")} {opcode} ");
                var value = "";
                switch (instruction.opcode) {
                    case Opcode.LoadConstDouble: value = constDouble[instruction.opvalue].ToString(); break;
                    case Opcode.LoadConstLong: value = constLong[instruction.opvalue].ToString(); break;
                    case Opcode.LoadConstString:
                    case Opcode.LoadGlobalString:
                    case Opcode.StoreGlobalString:
                    case Opcode.LoadValueString:
                    case Opcode.StoreValueString:
                        value = constString[instruction.opvalue].ToString();
                        break;
                    case Opcode.LoadInternal:
                        value = "load_internal_" + instruction.opvalue;
                        break;
                    case Opcode.LoadLocal:
                        value = "load_local_" + instruction.opvalue;
                        break;
                    case Opcode.LoadGlobal:
                        value = "load_global_" + instruction.opvalue;
                        break;
                    case Opcode.StoreInternal:
                        value = "store_internal_" + instruction.opvalue;
                        break;
                    case Opcode.StoreLocal:
                        value = "store_local_" + instruction.opvalue;
                        break;
                    case Opcode.StoreGlobal:
                        value = "store_global_" + instruction.opvalue;
                        break;
                    case Opcode.Jump:
                    case Opcode.FalseTo:
                    case Opcode.TrueTo:
                    case Opcode.TrueLoadTrue:
                    case Opcode.FalseLoadFalse:
                        value = instruction.opvalue.ToString("D5");
                        break;
                    case Opcode.LoadConstNull: value = "null"; break;
                    case Opcode.LoadConstTrue: value = "true"; break;
                    case Opcode.LoadConstFalse: value = "false"; break;
                    case Opcode.LoadValueObject:
                    case Opcode.CopyStackTop:
                    case Opcode.Plus:
                    case Opcode.Pop:
                        break;
                    default: value = instruction.opvalue.ToString(); break;
                }
                builder.AppendLine(value);
            }
            return builder.ToString();
        }
    }
    //单个类的信息
    public class ScriptClassData {
        public int name;            //类名
        public int parent;          //父级字符串索引，如果是-1则无父级
        public long[] functions;    //所有的函数 前32位是名字字符串索引 后32位为函数索引
    }
}
