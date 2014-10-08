using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.CSharp.RuntimeBinder;
using Sigil;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace Nivot.PowerShell.Async
{
    public static class DynamicMethodBinder
    {
        // NOTE: Funcs/Actions with an arity of `1 to `9 are in System.dll,
        //       but `10 to `16 are [currently] in System.Core.dll
        private static readonly Lazy<IEnumerable<Type>> DelegateTypesLazy =
            new Lazy<IEnumerable<Type>>(
                () => (typeof (Func<>).Assembly.ExportedTypes
                    .Union(typeof (Func<,,,,,,,,,,>).Assembly.ExportedTypes))
                    .Where(f => f.IsGenericTypeDefinition &&
                        typeof (MulticastDelegate).IsAssignableFrom(f)));

        /// <summary>
        /// Invoke a method by name when you expect to receive a return a value. This method will
        /// fail if the late-bound method is of void return type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="target"></param>
        /// <param name="methodName"></param>
        /// <param name="staticContext"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static T Invoke<T>(object target, [NotNull] string methodName, bool staticContext, params object[] parameters)
        {
            return (T) Invoke(target, methodName,
                staticContext: staticContext, wantReturnValue: true, parameters: parameters);
        }

        /// <summary>
        /// Invoke a method by name when you don't want, or expect, to receive a return a value. This
        /// method can invoke both void and non-void late bound targets.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="methodName"></param>
        /// <param name="staticContext"></param>
        /// <param name="parameters"></param>
        public static void InvokeNoResult(object target, [NotNull] string methodName, bool staticContext, params object[] parameters)
        {
            Invoke(target, methodName,
                staticContext: staticContext, wantReturnValue: false, parameters: parameters);
        }

        private static object Invoke([NotNull] object target, [NotNull] string methodName, bool staticContext, bool wantReturnValue, params object[] parameters)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (methodName == null) throw new ArgumentNullException("methodName");

            if (parameters == null)
            {
                parameters = new object[] {null};
            }

            // This is the magic sauce that lets the DLR bind static method invocations
            // dynamically. The current C# 5 compiler (I don't know about Rosyln) does not
            // support this:
            //
            //     dynamic t = typeof(Console)
            //     t.Beep()
            //
            // This will fail at runtime complaining that TypeInfo does not contain a method
            // named Beep. By specifying the flags below, this will succeed. I presume this was
            // cut due to shipping pressure. 
            var binderArgs = new List<CSharpArgumentInfo>
            {
                (staticContext) ?
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.IsStaticType |
                        CSharpArgumentInfoFlags.UseCompileTimeType, null) :
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
            };

            // This is mostly just lip service for the C# binder as we really don't care about the
            // distinction between null references, numeric/string/null literals and const types
            // as everything is coming in - at runtime - boxed/byref in the parameters array to
            // this method. Binding of the target method should succeed regardless. There are a
            // few scenarios where these details matter - out/ref parameters for example. A null
            // literal being passed would be invalid for a ref/out; this would need a null ref.
            for (int i = 0; i < parameters.Length; i++)
            {
                object parameter = parameters[i];
                CSharpArgumentInfoFlags flags = (parameter == null)
                    ? CSharpArgumentInfoFlags.Constant
                    : CSharpArgumentInfoFlags.UseCompileTimeType;
                
                binderArgs.Add(CSharpArgumentInfo.Create(flags, null));
            }

            var callSite = BuildCallSite(methodName, wantReturnValue, parameters, binderArgs);

            // TODO: build this with expression tree and cache compiled output?
            Func<object> fn =
                () =>
                    ((Delegate) callSite.Target).DynamicInvoke(
                        (new object[] {callSite, target}).Concat(parameters).ToArray());

            return fn();
        }

        private static dynamic BuildCallSite(string methodName, bool wantReturnValue, object[] parameters, List<CSharpArgumentInfo> binderArgs)
        {
            // ResultDiscarded is an optimization that the compiler can make at compile time that we
            // cannot determine at runtime. That is to say, I have no idea if the caller will save the
            // result of the invocation (if there is any). This flag has nothing to do with the return
            // type of the target method in this case. If the target bound method's return type actually
            // is void then this is neccessary or we get "Cannot implicitly convert type 'void' to 'object'"
            CallSiteBinder binder = Binder.InvokeMember(
                wantReturnValue ? CSharpBinderFlags.None : CSharpBinderFlags.ResultDiscarded,
                methodName,
                null,
                typeof (DynamicMethodBinder), // The purpose of this ref is unclear to me.
                binderArgs);

            var typeArgs = new List<Type>
            {
                typeof (CallSite),
                typeof (object)
            };

            if (parameters.Length > 0)
            {
                typeArgs.AddRange(GetTypeArray(parameters)); // FIXME: fails if parameters contains a null
            }

            if (wantReturnValue)
            {
                typeArgs.Add(typeof (object));
            }

            // returns open generic type Func`n/Action`n
            Type callSiteParameterType = GetFuncOrActionTypeByArity(wantReturnValue, typeArgs.ToArray());
            Type callSiteType = typeof (CallSite<>).MakeGenericType(callSiteParameterType);

            MethodInfo create = callSiteType.GetMethod("Create", new[] {typeof (CallSiteBinder)});
            dynamic callSite = create.Invoke(callSiteType, new object[] {binder});
            
            return callSite;
        }

        private static Type[] GetTypeArray(object[] array)
        {
            if (array.Any(element => element == null))
            {
                return (
                    from element in array
                    let type = (element == null) ?
                        typeof (object) : element.GetType()
                    select type
                ).ToArray();
            }
            return Type.GetTypeArray(array);
        }

        private static Type GetFuncOrActionTypeByArity(bool hasReturnType, params Type[] typeArguments)
        {
            // yeah, hacky, but simpler than a giant switch with 32 type literals
            // and probably faster than repeated use of GetType(...)
            string delegateTypeName = hasReturnType ? "Func" : "Action";
            
            Type delegateType = (from fn in DelegateTypesLazy.Value
                where (fn.Name.StartsWith(delegateTypeName)) // Func`1, Func`2, ... etc.
                      && (fn.GetGenericArguments().Length == typeArguments.Length)
                select fn)
                .SingleOrDefault();

            if (delegateType == null)
            {
                throw new ArgumentOutOfRangeException("typeArguments",
                    "Unable to determine a suitable delegate for the given generic type arguments.");
            }

            return delegateType.MakeGenericType(typeArguments);
        }


    }
}