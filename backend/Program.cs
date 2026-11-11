using System.Text.Json.Serialization;
using KrizevciBus.Api.Data;
using KrizevciBus.Api.Hubs;
using KrizevciBus.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddScoped<EtaService>();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// React dev server (Vite) radi na drugom portu, pa mu treba dozvola
builder.Services.AddCors(o => o.AddPolicy("web", p => p
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));   // SignalR to traži

var app = builder.Build();

// Primijeni migracije (na serveru nema ručnog "dotnet ef database update")
// i napuni bazu iz Data/seed.json ako je prazna
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db,
        Path.Combine(app.Environment.ContentRootPath, "Data", "seed.json"));
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors("web");
app.MapControllers();
app.MapHub<VehicleHub>("/hubs/vehicles");

app.Run();
