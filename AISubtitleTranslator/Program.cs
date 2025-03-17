using System.Net.Http.Headers;
using AISubtitleTranslator.Hubs;
using AISubtitleTranslator.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddScoped<IHubCommunicationService, HubCommunicationService>();
builder.Services.AddScoped<ISrtParser, SrtParser>();
builder.Services.AddScoped<IMistralTranslator>(sp => 
    new MistralTranslator(sp.GetRequiredService<IHttpClientFactory>().CreateClient("mistral"),
        sp.GetRequiredService<ISrtParser>()));
builder.Services.AddScoped<IVerificationService>(sp => 
    new VerificationService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("mistral"),
        sp.GetRequiredService<IHubCommunicationService>()));


builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});

builder.Services.AddHttpClient("mistral", client => 
{
    client.BaseAddress = new Uri("https://api.mistral.ai/v1/");
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", builder.Configuration["MistralApi:ApiKey"]);
});

builder.Services.AddSignalR();

builder.Services.AddMvc();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.MapHub<TranslationHub>("/translationHub");

app.Run();