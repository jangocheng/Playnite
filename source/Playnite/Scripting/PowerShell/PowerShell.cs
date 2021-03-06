﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Collections.ObjectModel;
using Playnite.API;
using Microsoft.Win32;

namespace Playnite.Scripting.PowerShell
{
    public class PowerShellRuntime : IScriptRuntime
    {
        private static NLog.Logger logger = NLog.LogManager.GetLogger("PowerShell");
        private Runspace runspace;
        
        public static bool IsInstalled
        {
            get
            {
                return Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PowerShell\3", "Install", null)?.ToString() == "1";
            }
        }

        public PowerShellRuntime()
        {
            runspace = RunspaceFactory.CreateRunspace();
            runspace.ApartmentState = System.Threading.ApartmentState.MTA;
            runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
            runspace.Open();

            using (var pipe = runspace.CreatePipeline())
            {
                pipe.Commands.AddScript("Set-ExecutionPolicy -Scope Process -ExecutionPolicy Unrestricted");
                pipe.Commands.AddScript("$global:ErrorActionPreference = \"Stop\"");
                pipe.Invoke();
            }

            SetVariable("__logger", new Logger("PowerShell"));
        }

        public void Dispose()
        {
            runspace.Close();
        }

        public static PowerShellRuntime CreateRuntime()
        {
            return new PowerShellRuntime();
        }

        public object Execute(string script)
        {
            return Execute(script, null);
        }

        public object Execute(string script, Dictionary<string, object> variables)
        {
            using (var pipe = runspace.CreatePipeline(script))
            {
                if (variables != null)
                {
                    foreach (var key in variables.Keys)
                    {                        
                        runspace.SessionStateProxy.SetVariable(key, variables[key]);
                    }
                }

                var result = pipe.Invoke();
                if (result.Count == 1)
                {
                    return result[0].BaseObject;
                }
                else
                {
                    return result.Select(a => a?.BaseObject).ToList();
                }
            }
        }

        public object ExecuteFile(string path)
        {
            return Execute($"& \"{path}\"");
        }

        public void SetVariable(string name, object value)
        {
            runspace.SessionStateProxy.SetVariable(name, value);
        }

        public object GetVariable(string name)
        {
            return runspace.SessionStateProxy.GetVariable(name);
        }

        public bool GetFunctionExits(string name)
        {
            using (var pipe = runspace.CreatePipeline($"Get-Command {name} -EA 0"))
            {
                var res = pipe.Invoke();
                return res.Count != 0;
            }
        }
    }
}
