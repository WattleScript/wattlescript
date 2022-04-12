using System;
using System.Reflection;

namespace WattleScript.Interpreter.Interop
{
    static class ReflectionExtensions
    {
        const BindingFlags BINDINGFLAGS_MEMBER = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public static ConstructorInfo[] GetAllConstructors(this Type type)
        {
            return type.GetConstructors(BINDINGFLAGS_MEMBER);
        }

        public static EventInfo[] GetAllEvents(this Type type)
        {
            return type.GetEvents(BINDINGFLAGS_MEMBER);
        }

        public static FieldInfo[] GetAllFields(this Type type)
        {
            return type.GetFields(BINDINGFLAGS_MEMBER);
        }
        
        public static MethodInfo[] GetAllMethods(this Type type)
        {
            return type.GetMethods(BINDINGFLAGS_MEMBER);
        }
        
        public static PropertyInfo[] GetAllProperties(this Type type)
        {
            return type.GetProperties(BINDINGFLAGS_MEMBER);
        }
    }
}