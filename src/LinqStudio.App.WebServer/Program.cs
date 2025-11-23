using LinqStudio.App.WebServer;
using LinqStudio.Blazor.Extensions;
using LinqStudio.Core.Extensions;
using LinqStudio.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration.AddJsonFile(SettingsService.FILE_NAME, optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddLinqStudio()
    .AddLinqStudioBlazor();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(LinqStudio.Blazor.Components.Pages.Home).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
