using LMSFree.Infrastructure.Data;
using LMSFree.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LMSFree.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<LmsDbContext>(o =>
        {
            var inMemory = config["Database:InMemoryName"];
            if (!string.IsNullOrEmpty(inMemory))
                o.UseInMemoryDatabase(inMemory);
            else
                o.UseNpgsql(config.GetConnectionString("Lms")
                    ?? throw new InvalidOperationException("ConnectionStrings:Lms is required."));
        });

        services.AddSingleton(TimeProvider.System);
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.AddSingleton<TokenService>();
        services.AddScoped<PasswordResetService>();

        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.Section));
        if (string.IsNullOrWhiteSpace(config[$"{SmtpOptions.Section}:Host"]))
            services.AddSingleton<IEmailSender, LogEmailSender>();
        else
            services.AddSingleton<IEmailSender, SmtpEmailSender>();

        services.AddSingleton<IFileStore>(new LocalFileStore(config["Storage:Path"] ?? Path.Combine(AppContext.BaseDirectory, "uploads")));
        return services;
    }
}
