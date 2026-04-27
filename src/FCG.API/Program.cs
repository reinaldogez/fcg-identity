using System.Threading.RateLimiting;
using FCG.API.Middlewares;
using Microsoft.AspNetCore.RateLimiting;
using FCG.Application.Interfaces;
using FCG.Application.UseCases;
using FCG.Domain.Interfaces;
using FCG.Domain.Services;
using FCG.Infrastructure.Persistence;
using FCG.Infrastructure.Persistence.Repositories;
using FCG.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

IConfigurationSection rateLimitConfig = builder.Configuration.GetSection("RateLimit");
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = rateLimitConfig.GetValue<int>("PermitLimit");
        opt.Window = TimeSpan.FromSeconds(rateLimitConfig.GetValue<int>("WindowInSeconds"));
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
builder.Services.AddDbContext<FcgDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<ISenhaService, SenhaService>();
builder.Services.AddScoped<IUsuarioDomainService, UsuarioDomainService>();
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<FcgDbContext>());
builder.Services.AddScoped<CadastrarUsuarioUseCase>();
builder.Services.AddScoped<ObterUsuarioPorIdUseCase>();
builder.Services.AddScoped<ListarUsuariosUseCase>();
builder.Services.AddScoped<AtualizarUsuarioUseCase>();
builder.Services.AddScoped<AlterarSenhaUseCase>();
builder.Services.AddScoped<DesativarUsuarioUseCase>();
builder.Services.AddScoped<AlterarTipoUsuarioUseCase>();
builder.Services.AddHostedService<AdminSeedService>();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapControllers();

app.Run();
