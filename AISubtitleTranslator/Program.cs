using AISubtitleTranslator.Hubs;
using AISubtitleTranslator.Services;
using Microsoft.AspNetCore.Mvc;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddScoped<IHubCommunicationService, HubCommunicationService>();
builder.Services.AddScoped<ISrtParser, SrtParser>();

builder.Services.AddSingleton<OpenAIClient>(_ =>
{
    var auth = new OpenAIAuthentication(builder.Configuration["OpenRouterApi:ApiKey"]);
    var settings = new OpenAIClientSettings(domain: "openrouter.ai/api");
    return new OpenAIClient(auth, settings);
});

builder.Services.AddScoped<ILlmTranslator>(sp =>
    new LlmTranslator(
        sp.GetRequiredService<OpenAIClient>(),
        "deepseek/deepseek-chat-v3-0324:free"
    ));

// Также нужно обновить VerificationService для работы с OpenAI клиентом
// builder.Services.AddScoped<IVerificationService>(sp =>
//     new VerificationService(
//         sp.GetRequiredService<OpenAIClient>(),
//         sp.GetRequiredService<IHubCommunicationService>()));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
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