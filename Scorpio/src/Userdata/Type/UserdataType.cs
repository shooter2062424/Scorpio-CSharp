﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Scorpio;
using Scorpio.Exception;
using Scorpio.Variable;
using Scorpio.Compiler;
namespace Scorpio.Userdata {
    /// <summary> 一个c#类的所有数据 </summary>
    public abstract class UserdataType {
        protected Type m_Type;                                                                          //Type
        protected UserdataMethod[] m_Operators = new UserdataMethod[UserdataOperator.OperatorCount];    //所有重载函数
        protected bool m_InitializeOperators;                                                           //是否初始化过所有重载函数
        public UserdataType(Type type) {
            m_Type = type;
        }
        public Type Type { get { return m_Type; } }
        /// <summary> 获取一个变量的类型,只能获取 Field Property Event </summary>
        public Type GetVariableType(string name) { return GetVariableType_impl(name); }
        /// <summary> 获得一个类变量 </summary>
        public object GetValue(object obj, string name) { return GetValue_impl(obj, name); }
        /// <summary> 设置一个类变量 </summary>
        public void SetValue(object obj, string name, ScriptValue value) { SetValue_impl(obj, name, value); }
        //创建一个模板类
        public ScriptValue MakeGenericType(Type[] parameters) {
            if (m_Type.IsGenericType && m_Type.IsGenericTypeDefinition) {
                var types = m_Type.GetTypeInfo().GetGenericArguments();
                var length = types.Length;
                if (length != parameters.Length)
                    throw new ExecutionException($"{m_Type.FullName} 传入的泛型参数个数错误 需要:{types.Length} 传入:{parameters.Length}");
                for (int i = 0; i < length; ++i) {
                    if (!types[i].GetTypeInfo().BaseType.GetTypeInfo().IsAssignableFrom(parameters[i]))
                        throw new ExecutionException($"{m_Type.FullName} 泛型类第{i+1}个参数不符合传入规则 需要:{types[i].GetTypeInfo().BaseType.FullName} 传入:{parameters[i].FullName}");
                }
                return TypeManager.GetUserdataType(m_Type.MakeGenericType(parameters));
            }
            throw new ExecutionException($"类 {m_Type.FullName} 不是未定义的泛型类");
        }
        protected void InitializeOperators() {
            if (m_InitializeOperators) { return; }
            m_InitializeOperators = true;
            for (var i = 0 ; i < UserdataOperator.OperatorCount; ++i) {
                try {
                    string operatorName = "";
                    switch (i) {
                        case UserdataOperator.PlusIndex: operatorName = UserdataOperator.Plus; break;
                        case UserdataOperator.MinusIndex: operatorName = UserdataOperator.Minus; break;
                        case UserdataOperator.MultiplyIndex: operatorName = UserdataOperator.Multiply; break;
                        case UserdataOperator.DivideIndex: operatorName = UserdataOperator.Divide; break;
                        case UserdataOperator.ModuloIndex: operatorName = UserdataOperator.Modulo; break;
                        case UserdataOperator.InclusiveOrIndex: operatorName = UserdataOperator.InclusiveOr; break;
                        case UserdataOperator.CombineIndex: operatorName = UserdataOperator.Combine; break;
                        case UserdataOperator.XORIndex: operatorName = UserdataOperator.XOR; break;
                        case UserdataOperator.ShiIndex: operatorName = UserdataOperator.Shi; break;
                        case UserdataOperator.ShrIndex: operatorName = UserdataOperator.Shr; break;
                        case UserdataOperator.GreaterIndex: operatorName = UserdataOperator.Greater; break;
                        case UserdataOperator.GreaterOrEqualIndex: operatorName = UserdataOperator.GreaterOrEqual; break;
                        case UserdataOperator.LessIndex: operatorName = UserdataOperator.Less; break;
                        case UserdataOperator.LessOrEqualIndex: operatorName = UserdataOperator.LessOrEqual; break;
                        case UserdataOperator.EqualIndex: operatorName = UserdataOperator.Equal; break;
                        case UserdataOperator.GetItemIndex: operatorName = UserdataOperator.GetItem; break;
                        case UserdataOperator.SetItemIndex: operatorName = UserdataOperator.SetItem; break;
                    }
                    m_Operators[i] = GetValue(null, operatorName) as UserdataMethod;
                } catch (System.Exception) {}
            }
        }
        public UserdataMethod GetOperator(int operate) {
            return m_Operators[operate];
        }
        /// <summary> 创建一个实例 </summary>
        public abstract ScriptUserdata CreateInstance(ScriptValue[] parameters, int length);
        /// <summary> 获取一个变量的类型,只能获取 Field Property Event </summary>
        protected abstract Type GetVariableType_impl(string name);
        /// <summary> 获得一个类变量 </summary>
        protected abstract object GetValue_impl(object obj, string name);
        /// <summary> 设置一个类变量 </summary>
        protected abstract void SetValue_impl(object obj, string name, ScriptValue value);
        /// <summary> 添加一个扩展函数 </summary>
        public abstract void AddExtensionMethod(MethodInfo method);
    }
}
