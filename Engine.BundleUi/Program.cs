using Engine.BundleUi.Components;
using Engine.BundleUi.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));
builder.Services.AddHttpClient<BundleApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
