using System;
using System.IO;
using JetBrains.Annotations;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;

namespace Qurre.Loader;

[PublicAPI]
public class Loader : Plugin
{
    public override string Name => "Qurre.Loader";

    public override string Description => "Simple, convenient and functional plugin loader for SCP: Secret Laboratory";

    public override string Author => "Qurre Team";

    public override LoadPriority Priority => LoadPriority.Highest;

    public override Version Version { get; } = new();

    public override Version RequiredApiVersion { get; } = new(LabApiProperties.CompiledVersion);

    public override void Enable()
    {
        Logger.Raw("[Loader] [Qurre] Initialization...", ConsoleColor.Yellow);

        if (!Directory.Exists(Manager.Qurre))
            Directory.CreateDirectory(Manager.Qurre);

        if (!File.Exists(Manager.QurreDll))
        {
            Logger.Raw("[Loader] [Error] Qurre.dll not found", ConsoleColor.Red);
            // TODO: download qurre here
            return;
        }

        Manager.LoadDependencies();
        Manager.InvokeAssembly(Manager.LoadAssembly(Manager.QurreDll));
    }

    public override void Disable()
    {
    }
}