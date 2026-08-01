using System;
using JetBrains.Annotations;

namespace Qurre.Internal.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[MeansImplicitUse(ImplicitUseTargetFlags.Itself)]
internal class SelfInvoke : Attribute;