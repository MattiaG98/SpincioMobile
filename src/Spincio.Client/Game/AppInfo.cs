using System.Reflection;

namespace Spincio.Client.Game;

/// <summary>The app version, from <c>Version</c> in Directory.Build.props (semantic versioning, CHANGELOG.md).</summary>
public static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
}
