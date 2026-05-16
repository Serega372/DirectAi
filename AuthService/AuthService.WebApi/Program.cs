var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;
var services = builder.Services;

services.AddControllers();

var app = builder.Build();

//app.UseAuthentication();
//app.UseAuthorization();
app.MapGet("/health", () => Results.Ok());
app.MapControllers();

await app.RunAsync();