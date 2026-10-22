using FamilyGraph.Infrastructure;
using Server.Accounts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("FamilyGraph"));
builder.Services.AddAccounts(builder.Configuration);
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddControllers(options => options.Filters.Add<ValidateAntiforgeryFilter>());
builder.Services.AddProblemDetails();

var app = builder.Build();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
