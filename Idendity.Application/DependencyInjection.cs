using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Idendity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<Validators.RegisterRequestValidator>();
        
        return services;
    }
}


