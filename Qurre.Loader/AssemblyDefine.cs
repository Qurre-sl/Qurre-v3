using System.Reflection;
using JetBrains.Annotations;

namespace Qurre.Loader;

[PublicAPI]
public readonly struct AssemblyDefine(Assembly assembly, string path)
{
    public Assembly Assembly { get; } = assembly;
    public string Path { get; } = path;
}