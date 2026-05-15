using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SmileyApi.Api.Endpoints;
using SmileyApi.Core.Interfaces;
using SmileyApi.Infrastructure.Data;
using SmileyApi.Infrastructure.Repositories;
using SmileyApi.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SmileyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IEstablishmentRepository, EstablishmentRepository>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseStaticFiles();
app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .ExcludeFromDescription();

app.MapEstablishmentEndpoints();

app.Run();

public partial class Program { }
