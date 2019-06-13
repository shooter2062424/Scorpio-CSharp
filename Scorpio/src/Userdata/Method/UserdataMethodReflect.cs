﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Scorpio.Tools;
namespace Scorpio.Userdata {
    public class UserdataMethodReflect : UserdataMethod {
        //构造函数
        public UserdataMethodReflect(Type type, string methodName, ConstructorInfo[] methods) :
            base(type, methodName) {
            Initialize(new List<MethodBase>(methods), true);
        }
        //普通函数
        public UserdataMethodReflect(Type type, string methodName, MethodInfo[] methods) :
            base(type, methodName) {
            Initialize(new List<MethodBase>(methods), false);
        }
        protected void Initialize(List<MethodBase> methods, bool isConstructor) {
            var functionMethod = new List<FunctionData>();          //实例函数
            var functionStaticMethod = new List<FunctionData>();    //静态函数
            var genericMethods = new List<FunctionDataReflect>();   //模板函数
            var parameterTypes = new List<Type>();                  //参数类型
            var defaultParameter = new List<object>();              //默认参数
            var refOut = new List<bool>();                          //是否是 ref out
            var length = methods.Count;                             //函数数量
            var requiredNumber = 0;                                 //必须的参数个数
            Type paramType = null;                                  //变长参数类型
            FunctionData functionData;
            for (int i = 0; i < length; ++i) {
                var method = methods[i];
                var parameters = method.GetParameters();            //所有参数
                if (Util.IsExtensionMethod(method)) {
                    //返回不定参类型
                    ParseParameters(parameters, 1, parameterTypes, defaultParameter, refOut, ref requiredNumber, ref paramType);
                    functionData = new FunctionDataReflectExtension(method as MethodInfo, parameterTypes.ToArray(), defaultParameter.ToArray(), refOut.ToArray(), requiredNumber, paramType);
                } else {
                    //返回不定参类型
                    ParseParameters(parameters, 0, parameterTypes, defaultParameter, refOut, ref requiredNumber, ref paramType);
                    if (method is ConstructorInfo) {
                        functionData = new FunctionDataConstructor(method as ConstructorInfo, parameterTypes.ToArray(), defaultParameter.ToArray(), refOut.ToArray(), requiredNumber, paramType);
                    } else {
                        functionData = new FunctionDataReflectMethod(method as MethodInfo, parameterTypes.ToArray(), defaultParameter.ToArray(), refOut.ToArray(), requiredNumber, paramType);
                    }
                }
                if (functionData.IsGeneric)
                    genericMethods.Add(functionData as FunctionDataReflect);
                else if (functionData.IsStatic)
                    functionStaticMethod.Add(functionData);
                else
                    functionMethod.Add(functionData);
            }
            if (isConstructor && m_Type.IsValueType) {
                functionMethod.Add(new FunctionDataStructConstructor(m_Type));
            }
            m_Methods = functionMethod.ToArray();
            m_StaticMethods = functionStaticMethod.ToArray();
            m_GenericMethods = genericMethods.ToArray();
            m_GenericMethodCount = m_GenericMethods.Length;
            
        }
        void ParseParameters(ParameterInfo[] parameters, int begin, List<Type> parameterTypes, List<object> defaultParameter, List<bool> refOut, ref int requiredNumber, ref Type paramType) {
            var hadDefault = false;
            paramType = null;
            parameterTypes.Clear();
            defaultParameter.Clear();
            refOut.Clear();
            for (int i = begin; i < parameters.Length; ++i) {
                var parameter = parameters[i];
                parameterTypes.Add(parameter.ParameterType);
                refOut.Add(parameter.IsRetval || parameter.IsOut);
                if (parameter.DefaultValue != DBNull.Value) {
                    if (!hadDefault) {
                        hadDefault = true;
                        requiredNumber = i - begin;
                    }
                    defaultParameter.Add(parameter.DefaultValue);
                } else {
                    defaultParameter.Add(null);
                }
                if (Util.IsParams(parameter)) {
                    paramType = parameter.ParameterType.GetElementType();
                    break;
                }
            }
            if (!hadDefault) { requiredNumber = parameterTypes.Count; }
        }
    }
}
