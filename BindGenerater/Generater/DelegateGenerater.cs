﻿using Mono.Cecil;
using System.Collections;
using System.Collections.Generic;

namespace Generater
{
    public class DelegateGenerater : CodeGenerater
    {
        EventDefinition genEvent;
        bool isStatic;
        List<MethodGenerater> methods = new List<MethodGenerater>();
        public DelegateGenerater(EventDefinition e)
        {
            genEvent = e;

            if (e.AddMethod != null)
            {
                methods.Add(new MethodGenerater(e.AddMethod));
                isStatic = e.AddMethod.IsStatic;
            }

            if (e.RemoveMethod != null)
            {
                methods.Add(new MethodGenerater(e.RemoveMethod));
                isStatic = e.RemoveMethod.IsStatic;
            }
        }

        /*
         static event global::UnityEngine.Application.LogCallback _logMessageReceived;
        static Action<int, int, int> logMessageReceivedAction = OnlogMessageReceived;
        static void OnlogMessageReceived(int arg0,int arg1,int arg2)
        {
            _logMessageReceived(unbox(arg0), unbox(arg1), unbox(arg2));
        }
        public static event global::UnityEngine.Application.LogCallback logMessageReceived
		{
			add
			{
                bool add = _logMessageReceived == null;
                _logMessageReceived += value;
                if(add)
                {
                    var value_p = Marshal.GetFunctionPointerForDelegate(logMessageReceivedAction);
                    MonoBind.UnityEngine_Application_add_logMessageReceived(value_p);
                }
			}
			remove
			{
                _logMessageReceived -= value;
                if(_logMessageReceived == null)
                {
                    var value_p = Marshal.GetFunctionPointerForDelegate(logMessageReceivedAction);
                    MonoBind.UnityEngine_Application_remove_logMessageReceived(value_p);
                }
			}
		}
             */
        public override void Gen()
        {
            var name = genEvent.Name;
            
            var flag = isStatic ? "static " : "";
            var type = genEvent.EventType; // LogCallback(string condition, string stackTrace, LogType type);

            var eventTypeName = TypeResolver.Resolve(type).RealTypeName();
            if (type.IsGenericInstance)
                eventTypeName = Utils.GetGenericTypeName(type);

            var eventDeclear = Utils.GetDelegateWrapTypeName(type, isStatic ? null : genEvent.DeclaringType); //Action <int,int,int>
            var paramTpes = Utils.GetDelegateParams(type, isStatic ? null : genEvent.DeclaringType, out var returnType); // string , string , LogType ,returnType
            var returnTypeName = returnType != null ? TypeResolver.Resolve(returnType).RealTypeName() : "void";

            //static event global::UnityEngine.Application.LogCallback _logMessageReceived;
            CS.Writer.WriteLine($"public {flag}event {eventTypeName} _{name}");

            //static Action<int, int, int> logMessageReceivedAction = OnlogMessageReceived;
            CS.Writer.WriteLine($"static {eventDeclear} {name}Action = On{name}");

            //static void OnlogMessageReceived(int arg0,int arg1,int arg2)
            var eventFuncDeclear = $"static {returnTypeName} On{name}(";
            for (int i = 0; i < paramTpes.Count; i++)
            {
                var p = paramTpes[i];
                eventFuncDeclear += TypeResolver.Resolve(p).LocalVariable($"arg{i}");
                if (i != paramTpes.Count - 1)
                {
                    eventFuncDeclear += ",";
                }
            }
            eventFuncDeclear += ")";

            CS.Writer.Start(eventFuncDeclear);

            //_logMessageReceived(unbox(arg0), unbox(arg1), unbox(arg2));
            var callCmd = $"_{name}(";
            var targetObj = "";

            for (int i = 0; i < paramTpes.Count; i++)
            {
                var p = paramTpes[i];
                var param = TypeResolver.Resolve(p).Unbox($"arg{i}");
                
                if (i == 0 && !isStatic)
                {
                    targetObj = param + ".";
                    continue;
                }

                callCmd += param;
                if (i != paramTpes.Count - 1)
                    callCmd += ",";
            }
            callCmd += ")";

            if (!string.IsNullOrEmpty(targetObj))
                callCmd = targetObj + callCmd;
            if (returnType != null)
                callCmd = $"var res = " + callCmd;

            CS.Writer.WriteLine(callCmd);
            if (returnType != null)
            {
                var res = TypeResolver.Resolve(returnType).Box("res");
                CS.Writer.WriteLine($"return {res}");
            }
            CS.Writer.End();

            //public static event LogCallback logMessageReceived
            CS.Writer.Start($"public {flag}event {eventTypeName} {name}");

            var targetHandle = isStatic ? "" : "this.Handle, ";
            if (genEvent.AddMethod != null)
            {
                CS.Writer.Start("add");
                CS.Writer.WriteLine($"bool attach = (_{name} == null)");
                CS.Writer.WriteLine($"_{name} += value");

                CS.Writer.Start("if(attach)");
                var res = TypeResolver.Resolve(type).Box($"{name}Action");
                
                CS.Writer.WriteLine(Utils.BindMethodName(genEvent.AddMethod, false, false) + $"({targetHandle}{res})");
                //var value_p = Marshal.GetFunctionPointerForDelegate(logMessageReceivedAction);
                //MonoBind.UnityEngine_Application_add_logMessageReceived(value_p);
                CS.Writer.End(); //if(attach)
                CS.Writer.End(); // add
            }
            if(genEvent.RemoveMethod != null)
            {
                CS.Writer.Start("remove");
                CS.Writer.WriteLine($"_{name} -= value");

                CS.Writer.Start($"if(_{name} == null)");
                var res = TypeResolver.Resolve(type).Box($"{name}Action");
                CS.Writer.WriteLine(Utils.BindMethodName(genEvent.RemoveMethod, false, false) + $"({targetHandle}{res})");
                CS.Writer.End(); //if(attach)
                CS.Writer.End(); // remove
            }

            CS.Writer.End();
        }

        static string GetEventFieldName(MethodDefinition method)
        {
            var name = method.Name;
            if (name.StartsWith("add_"))
                name = name.Substring("add_".Length);
            else if (name.StartsWith("remove_"))
                name = name.Substring("remove_".Length);
            return name;
        }



    }
}