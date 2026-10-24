using FamilyGraph.Infrastructure;
using Server.Accounts;
using Server.Client;
using Server.Errors;
using Server.Export;
using Server.Graph;
using Server.Handovers;
using Server.Import;
using Server.People;
using Server.Relationships;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAccounts(builder.Configuration);
builder.Services.AddPeople(builder.Configuration);
builder.Services.AddRelationships(builder.Configuration);
builder.Services.AddGraph(builder.Configuration);
builder.Services.AddHandovers(builder.Configuration);
builder.Services.AddExport();
builder.Services.AddImport();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddControllers(options => options.Filters.Add<ValidateAntiforgeryFilter>());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // In development the Angular dev server proxies /api over HTTP (proxy.conf.json), so redirecting
    // to HTTPS would leave the SPA's origin.
    app.UseHttpsRedirection();
}
// The production Client build is published into wwwroot (src/Server/Dockerfile). In development
// wwwroot is empty and the SPA Proxy serves the Client instead.
var clientFiles = ClientFiles.StaticFileOptions();
app.UseDefaultFiles();
app.UseStaticFiles(clientFiles);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Client routes fall back to index.html. Paths under /api never do, so API errors stay API errors.
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html", clientFiles);

app.Run();
