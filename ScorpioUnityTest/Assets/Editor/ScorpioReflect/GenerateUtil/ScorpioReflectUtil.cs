﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
public class ScorpioReflectUtil {
    public const BindingFlags BindingFlag = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    struct ComparerType : IComparer<Type> {
        public int Compare(Type x, Type y) {
            if (x == null || y == null) { return 0; }
            return GetFullName(x).CompareTo(GetFullName(y));
        }
    }
    //public static string GetFullName(Type type) {
    //    return GetFullName(type, null);
    //}
    //获得一个类的完整名字
    public static string GetFullName(Type type) {
        if (type.IsGenericParameter) {
            return GetFullName(type.BaseType);
        } else {
            var fullName = type.FullName;
            if (string.IsNullOrEmpty(fullName))
                return "";
            fullName = fullName.Replace("+", ".");
            var builder = new StringBuilder();
            if (type.IsGenericType) {
                if (type.IsGenericTypeDefinition) {
                    throw new Exception("未声明的模板类 : " + fullName);
                }
                var index = fullName.IndexOf("`");
                builder.Append(fullName.Substring(0, index));
                builder.Append("<");
                var types = type.GetGenericArguments();
                bool first = true;
                foreach (var t in types) {
                    if (first == false) { builder.Append(","); } else { first = false; }
                    builder.Append(GetFullName(t));
                }
                builder.Append(">");
            } else {
                builder.Append(fullName);
            }
            return builder.ToString();
        }
    }
    public static void SortType(List<Type> types) {
        types.Sort(new ComparerType());
    }
    //判断函数是否有未使用的模板定义
    public static bool CheckGenericMethod(MethodBase method) {
        if (!method.IsGenericMethod) { return true; }
        var genericArgs = new List<Type>(method.GetGenericArguments());
        var parameters = Array.ConvertAll(method.GetParameters(), _ => _.ParameterType);
        Array.ForEach(parameters, _ => genericArgs.Remove(_));
        return genericArgs.Count == 0;
    }
}
