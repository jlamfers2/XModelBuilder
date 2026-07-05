using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using XModelBuilder.Demo.Shop.Auth;
using XModelBuilder.Demo.Shop.Data;
using XModelBuilder.Demo.Shop.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<ShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Shop")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=XModelBuilderDemo;Trusted_Connection=True;TrustServerCertificate=True"));

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<CatalogService>();

builder.Services
    .AddAuthentication(HeaderAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so the integration tests can reference it via <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
