using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Doorstop
{
    // quick 'n dirty renderdoc injection

    // This needs to happen before GL init, so we intercept doorstop before passing on to UMM.
    // To use, install next to UMM, and update target_assembly in doorstop.ini to match.
    public static class Entrypoint
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        // doorstop looks specifically for this signature
        public static void Start()
        {
            InitRenderDoc();
            InitUmm();
        }

        private static void InitRenderDoc()
        {
            string renderDocPath = @"C:\Program Files\RenderDoc\renderdoc.dll";

            if (File.Exists(renderDocPath))
            {
                IntPtr rdHandle = LoadLibrary(renderDocPath);
                if (rdHandle != IntPtr.Zero)
                {
                    Console.WriteLine("[RenderDocBridge] RenderDoc injected successfully.");
                }
            }
        }

        private static void InitUmm()
        {
            string proxyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string ummPath = Path.Combine(proxyDirectory, "UnityModManager.dll");

            if (File.Exists(ummPath))
            {
                Assembly ummAssembly = Assembly.LoadFrom(ummPath);

                Type ummEntryType = ummAssembly.GetType("Doorstop.Entrypoint");

                MethodInfo startMethod = ummEntryType?.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
                startMethod?.Invoke(null, null);
            }
        }
    }
}
