using System;
using System.Reflection;

/// <summary>
/// Attribute to denote the project root directory.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
internal class LambdajectionVersionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LambdajectionVersionAttribute" /> class.
    /// </summary>
    /// <param name="version">The Lambdajection version being used.</param>
    public LambdajectionVersionAttribute(string version)
    {
        Version = version;
    }

    /// <summary>
    /// Gets the version of lambdajection being used for this assembly.
    /// </summary>
    public static string ThisAssemblyLambdajectionVersion
    {
        get
        {
            var thisAssembly = Assembly.GetExecutingAssembly();
            var attribute = thisAssembly.GetCustomAttribute<LambdajectionVersionAttribute>();
            return attribute!.Version;
        }
    }

    /// <summary>
    /// Gets the lambdajection version.
    /// </summary>
    private string Version { get; }
}
