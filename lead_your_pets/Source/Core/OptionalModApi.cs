using System;
using System.Linq;
using System.Reflection;

namespace LeadYourPet
{
    // Resolve the complete signature before invoking: reflection does not fill optional arguments.
    internal static class OptionalModApi
    {
        internal static MethodInfo Resolve(Type type, string name, params Type[] required)
        {
            return type?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == name && !method.ContainsGenericParameters)
                .Where(method =>
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length >= required.Length &&
                        parameters.Take(required.Length).Select(p => p.ParameterType).SequenceEqual(required) &&
                        parameters.Skip(required.Length).All(p => p.IsOptional);
                })
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();
        }

        internal static object Invoke(MethodInfo method, params object[] arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (arguments.Length == parameters.Length)
                return method.Invoke(null, arguments);
            object[] complete = new object[parameters.Length];
            Array.Copy(arguments, complete, arguments.Length);
            for (int i = arguments.Length; i < complete.Length; i++)
                complete[i] = parameters[i].DefaultValue;
            return method.Invoke(null, complete);
        }
    }
}
