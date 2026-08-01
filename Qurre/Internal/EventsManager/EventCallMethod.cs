using System;
using System.Reflection;
using Qurre.Events.Structs;

namespace Qurre.Internal.EventsManager;

internal class EventCallMethod : IEventCall
{
    internal EventCallMethod(MethodInfo info, int priority)
    {
        Info = info;
        Priority = priority;
        Identifier = $"Method '{Info.Name}' of class '{Info.ReflectedType?.FullName}'";
    }

    internal MethodInfo Info { get; }
    public int Priority { get; }
    public string Identifier { get; }

    public void Call(IBaseEvent ev)
    {
        if (Info.IsStatic)
        {
            Invoke(ev, null);
            return;
        }

        if (Lists.ClassesOfNonStaticMethods.TryGetValue(Info, out var instance))
        {
            Invoke(ev, instance);
            return;
        }

        var type = Info.DeclaringType;
        var constructor = type?.GetConstructor(Type.EmptyTypes);

        instance = constructor?.Invoke([]) ?? throw new NullReferenceException(nameof(constructor));
        Lists.ClassesOfNonStaticMethods.Add(Info, instance);

        Invoke(ev, instance);
    }

    private void Invoke(IBaseEvent @event, object? root)
    {
        if (Info.GetParameters().Length == 0) Info.Invoke(root, []);
        else Info.Invoke(root, [@event]);
    }
}