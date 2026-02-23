#nullable enable
using System;

namespace Arenyx.RemoteConfig.SourceGen
{
    /// <summary>
    /// Marks a class or record as a config model for binder generation.
    /// The source generator will produce a <c>&lt;ModelName&gt;Binder</c> class
    /// implementing <c>IConfigBinder&lt;TModel&gt;</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ConfigModelAttribute : Attribute
    {
    }
}
