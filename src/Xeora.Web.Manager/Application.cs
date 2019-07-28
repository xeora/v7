﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Xeora.Web.Manager
{
    public class Application
    {
        private static readonly object PrepareLock = new object();
        private readonly List<string> _AssemblyPaths;
        private LibraryExecuter _LibraryExecuter;

        private readonly string _ExecutableName;

        private Application(string executableName)
        {
            this._ExecutableName = executableName;
            this._AssemblyPaths = new List<string>();
        }

        public void AddSearchPath(string assemblyPath)
        {
            if (!this._AssemblyPaths.Contains(assemblyPath))
                this._AssemblyPaths.Add(assemblyPath);
        }

        public object Invoke(Basics.Context.Request.HttpMethod httpMethod, string[] classNames, string functionName, object[] functionParams, bool instanceExecute, ExecuterTypes executerType) =>
            this._LibraryExecuter.Invoke(httpMethod, classNames, functionName, functionParams, instanceExecute, executerType);

        // Load must use the same appdomain because AppDomain logic is not supported in .NET Standard anymore
        private bool Load()
        {
            this._LibraryExecuter = 
                new LibraryExecuter(Loader.Current.Path, this._ExecutableName, this._AssemblyPaths.ToArray());
            this._LibraryExecuter.Load();

            return !this._LibraryExecuter.MissingFileException;
        }

        private static readonly ConcurrentDictionary<string, Application> ApplicationCache =
            new ConcurrentDictionary<string, Application>();
        public static Application Prepare(string executableName)
        {
            string applicationKey =
                $"KEY-{Loader.Current.Path}_{executableName}";

            lock (Application.PrepareLock)
            {
                if (Application.ApplicationCache.TryGetValue(applicationKey, out Application application))
                    return application;
                
                application = new Application(executableName);
                application.AddSearchPath(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                application.AddSearchPath(Loader.Current.Path);

                if (!application.Load())
                    throw new FileLoadException();

                if (!Application.ApplicationCache.TryAdd(applicationKey, application))
                    throw new OutOfMemoryException();

                return application;
            }
        }

        public static void Dispose()
        {
            foreach(string key in Application.ApplicationCache.Keys)
            {
                if (Application.ApplicationCache.TryRemove(key, out Application application))
                    application._LibraryExecuter.Dispose();
            }
        }
    }
}
