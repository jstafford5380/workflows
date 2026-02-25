namespace Engine.Api.Bundles;

public sealed class BundleOptions
{
    public string StorageRoot { get; set; } = "App_Data/Bundles";

    public string PreviewRoot { get; set; } = "App_Data/BundlePreviews";

    public int MaxUploadBytes { get; set; } = 50 * 1024 * 1024;

    public int PreviewTtlMinutes { get; set; } = 30;
}
