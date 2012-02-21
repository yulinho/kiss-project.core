﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Kiss.Utils;

namespace Kiss
{

    /// <summary>
    /// A class that finds types by looping assemblies in the 
    /// currently executing AppDomain. Only assemblies whose names matches
    /// certain patterns are investigated and an optional list of assemblies
    /// referenced by <see cref="AssemblyNames"/> are always investigated.
    /// </summary>
    public class AppDomainTypeFinder : ITypeFinder
    {
        private bool loadAppDomainAssemblies = true;
        private string assemblySkipLoadingPattern = "^System|^mscorlib|^Microsoft|^CppCodeProvider|^VJSharpCodeProvider|^WebDev|^Castle|^Iesi|^log4net|^NHibernate|^nunit|^TestDriven|^MbUnit|^Rhino|^QuickGraph|^TestFu|^Telerik|^ComponentArt|^MvcContrib|^NLog|^SMDiagnostics|^NVelocity|^IronPython|^CookComputing|^antlr|^Newtonsoft.Json|^StringTemplate|^ICSharpCode";
        private string assemblyRestrictToLoadingPattern = ".*";
        private IList<string> assemblyNames = new List<string>();

        #region props

        /// <summary>The app domain to look for types in.</summary>
        public virtual AppDomain App
        {
            get { return AppDomain.CurrentDomain; }
        }

        /// <summary>Gets or sets wether N2 should iterate assemblies in the app domain when loading N2 types. Loading patterns are applied when loading these assemblies.</summary>
        public bool LoadAppDomainAssemblies
        {
            get { return loadAppDomainAssemblies; }
            set { loadAppDomainAssemblies = value; }
        }

        /// <summary>Gets or sets assemblies loaded a startup in addition to those loaded in the AppDomain.</summary>
        public IList<string> AssemblyNames
        {
            get { return assemblyNames; }
            set { assemblyNames = value; }
        }

        /// <summary>Gets the pattern for dlls that we know don't need to be investigated for content items.</summary>
        public string AssemblySkipLoadingPattern
        {
            get { return assemblySkipLoadingPattern; }
            set { assemblySkipLoadingPattern = value; }
        }

        /// <summary>Gets or sets the pattern for dll that will be investigated. For ease of use this defaults to match all but to increase performance you might want to configure a pattern that includes N2 assemblies and your own.</summary>
        /// <remarks>If you change this so that N2 assemblies arn't investigated (e.g. by not including something like "^N2|..." you may break core functionality.</remarks>
        public string AssemblyRestrictToLoadingPattern
        {
            get { return assemblyRestrictToLoadingPattern; }
            set { assemblyRestrictToLoadingPattern = value; }
        }

        #endregion

        /// <summary>Finds types assignable from of a certain type in the app domain.</summary>
        /// <param name="requestedType">The type to find.</param>
        /// <returns>A list of types found in the app domain.</returns>
        public virtual IList<Type> Find(Type requestedType)
        {
            List<Type> types = new List<Type>();
            foreach (Assembly a in GetAssemblies())
            {
                Type[] list;

                try
                {
                    list = a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    list = ex.Types;

                    StringBuilder msg = new StringBuilder("Error getting types from assembly ");
                    msg.Append(a.FullName);

                    if (ex.LoaderExceptions != null && ex.LoaderExceptions.Length > 0)
                    {
                        msg.AppendLine();
                        msg.AppendLine("LoaderExceptions:");
                        foreach (Exception e in ex.LoaderExceptions)
                        {
                            msg.AppendLine(ExceptionUtil.WriteException(e));
                        }
                    }

                    if (ex.Types != null && ex.Types.Length > 0)
                    {
                        msg.AppendLine();
                        msg.AppendLine("Types:");
                        foreach (var item in ex.Types)
                        {
                            if (item == null) continue;
                            msg.AppendLine(item.Name);
                        }
                    }

                    msg.AppendLine(ExceptionUtil.WriteException(ex));

                    LogManager.GetLogger<AppDomainTypeFinder>().Error(msg.ToString());
                }

                foreach (Type t in list)
                {
                    if (t == null) continue;

                    if ((requestedType.IsInterface && t.GetInterface(requestedType.Name) != null) || requestedType.IsAssignableFrom(t))
                        types.Add(t);
                }

            }

            return types;
        }

        /// <summary>Gets tne assemblies related to the current implementation.</summary>
        /// <returns>A list of assemblies that should be loaded by the factory.</returns>
        public virtual IList<Assembly> GetAssemblies()
        {
            List<string> addedAssemblyNames = new List<string>();
            List<Assembly> assemblies = new List<Assembly>();

            if (LoadAppDomainAssemblies)
                AddAssembliesInAppDomain(addedAssemblyNames, assemblies);

            AddConfiguredAssemblies(addedAssemblyNames, assemblies);

            return assemblies;
        }

        /// <summary>Iterates all assemblies in the AppDomain and if it's name matches the configured patterns add it to our list.</summary>
        /// <param name="addedAssemblyNames"></param>
        /// <param name="assemblies"></param>
        private void AddAssembliesInAppDomain(List<string> addedAssemblyNames, List<Assembly> assemblies)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (Matches(assembly.FullName))
                {
                    if (!addedAssemblyNames.Contains(assembly.FullName))
                    {
                        assemblies.Add(assembly);
                        addedAssemblyNames.Add(assembly.FullName);
                    }
                }
            }
        }

        /// <summary>Adds specificly configured assemblies.</summary>
        protected virtual void AddConfiguredAssemblies(List<string> addedAssemblyNames, List<Assembly> assemblies)
        {
            foreach (string assemblyName in AssemblyNames)
            {
                Assembly assembly = Assembly.Load(assemblyName);
                if (!addedAssemblyNames.Contains(assembly.FullName))
                {
                    assemblies.Add(assembly);
                    addedAssemblyNames.Add(assembly.FullName);
                }
            }
        }

        /// <summary>Check if a dll is one of the shipped dlls that we know don't need to be investigated.</summary>
        /// <param name="assemblyFullName">The name of the assembly to check.</param>
        /// <returns>True if the assembly should be loaded into N2.</returns>
        public virtual bool Matches(string assemblyFullName)
        {
            return !Matches(assemblyFullName, AssemblySkipLoadingPattern)
                   && Matches(assemblyFullName, AssemblyRestrictToLoadingPattern);
        }

        /// <summary>Check if a dll is one of the shipped dlls that we know don't need to be investigated.</summary>
        /// <param name="assemblyFullName">The assembly name to match.</param>
        /// <param name="pattern">The regular expression pattern to match against the assembly name.</param>
        /// <returns>True if the pattern matches the assembly name.</returns>
        protected virtual bool Matches(string assemblyFullName, string pattern)
        {
            return Regex.IsMatch(assemblyFullName, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>Makes sure matching assemblies in the supplied folder are loaded in the app domain.</summary>
        /// <param name="directoryPath">The physical path to a directory containing dlls to load in the app domain.</param>
        protected virtual void LoadMatchingAssemblies(string directoryPath)
        {
            List<string> loadedAssemblyNames = new List<string>();
            foreach (Assembly a in GetAssemblies())
            {
                loadedAssemblyNames.Add(a.FullName);
            }

            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            foreach (string dllPath in Directory.GetFiles(directoryPath, "*.dll"))
            {
                try
                {
                    Assembly a = Assembly.ReflectionOnlyLoadFrom(dllPath);
                    if (Matches(a.FullName) && !loadedAssemblyNames.Contains(a.FullName))
                    {
                        App.Load(a.FullName);
                    }
                }
                catch (BadImageFormatException)
                {
                    LogManager.GetLogger<AppDomainTypeFinder>().Warn("{0} is not a valid .net assembly.", dllPath);
                }
            }
        }

        /// <summary>
        /// find assembly of a certain name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Assembly FindAssembly(string name)
        {
            foreach (Assembly ass in GetAssemblies())
            {
                if (ass.GetName().Name == name || ass.FullName == name)
                    return ass;
            }

            return null;
        }
    }
}
