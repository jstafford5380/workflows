namespace Engine.Activities;

public sealed class ActivityRunnerOptions
{
    public string ScriptsBasePath { get; set; } = "Scripts";

    public string BundleStoragePath { get; set; } = "App_Data/Bundles";

    public int DefaultTimeoutSeconds { get; set; } = 120;

    public Dictionary<string, string> ScriptMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
