using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using LabApi.Features.Console;

namespace Qurre.Loader;

public static class Manager
{
    private static readonly HashSet<AssemblyDefine> LocalLoaded = [];
    public static string AppData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static string Qurre { get; } = Path.Combine(AppData, "Qurre");
    public static string QurreDll { get; } = Path.Combine(Qurre, "Qurre.dll");
    public static string Plugins { get; } = Path.Combine(Qurre, "Plugins");
    public static string Depends { get; } = Path.Combine(Plugins, "Depends");

    public static byte[] ReadFile(string path)
    {
        return File.ReadAllBytes(path);
    }

    public static bool Loaded(string path)
    {
        return LocalLoaded.Any(x => x.Path == path);
    }


    public static Assembly LoadAssembly(string path)
    {
        if (Loaded(path))
            throw new Exception("Assembly already loaded");

        Assembly assembly = Assembly.Load(ReadFile(path));
        LocalLoaded.Add(new AssemblyDefine(assembly, path));

        return assembly;
    }


    internal static void InvokeAssembly(Assembly assembly)
    {
        try
        {
            foreach (Type type in assembly.GetTypes())
                try
                {
                    if (type.GetInterface("ICharacterLoader") == typeof(ICharacterLoader))
                        (Activator.CreateInstance(type) as ICharacterLoader)?.Enable();
                }
                catch (Exception ex)
                {
                    Logger.Raw($"[Loader] [DEBUG] {ex.Message}", ConsoleColor.Green);
                }
        }
        catch (Exception ex)
        {
            Logger.Raw($"[Loader] [Error] {ex.Message}", ConsoleColor.Red);
        }
    }

    internal static void LoadDependencies()
    {
        Logger.Raw("[Loader] [Qurre] Loading dependencies...", ConsoleColor.Magenta);

        if (!Directory.Exists(Plugins))
        {
            Logger.Raw($"[Loader] [Qurre] Plugins directory not found. Creating: {Plugins}", ConsoleColor.DarkYellow);
            Directory.CreateDirectory(Plugins);
        }

        if (!Directory.Exists(Depends))
        {
            Logger.Raw($"[Loader] [Qurre] Dependencies directory not found. Creating: {Depends}",
                ConsoleColor.DarkYellow);
            Directory.CreateDirectory(Depends);
        }

        string[] needDeps = ["0Harmony.dll", "Newtonsoft.Json.dll"];
        foreach (string dep in needDeps)
        {
            string path = Path.Combine(Depends, dep);

            if (Loaded(path))
                continue;

            if (!File.Exists(path))
                // TODO: change to git v3-storage with github actions
                Download("https://cdn.scpsl.shop/qurre.sl/Dependencies/" + dep, dep);

            Assembly assembly = LoadAssembly(path);

            Logger.Raw("[Loader] [Qurre] Loaded dependency " + assembly.FullName, ConsoleColor.Blue);
        }

        return;

        static void Download(string url, string name)
        {
            Logger.Raw($"[Loader] [Qurre] {name} not found. Downloading {name}", ConsoleColor.Red);

            WebRequest request = WebRequest.Create(url);
            WebResponse response = request.GetResponse();

            using Stream responseStream = response.GetResponseStream() ??
                                          throw new NullReferenceException("Response stream is null");
            using Stream fileStream = File.OpenWrite(Path.Combine(Depends, name));

            byte[] buffer = new byte[4096];
            int bytesRead = responseStream.Read(buffer, 0, 4096);

            while (bytesRead > 0)
            {
                fileStream.Write(buffer, 0, bytesRead);
                bytesRead = responseStream.Read(buffer, 0, 4096);
            }
        }
    }
}