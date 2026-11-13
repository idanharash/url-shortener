using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Application.Extensions;

public static class SettingsExtensions
{
    /// <summary>
    /// Binds and validates a configuration section, then registers it as a singleton.
    /// Throws at startup with the names (never the values) of missing or invalid settings.
    /// </summary>
    public static T AddValidatedSettings<T>(this IServiceCollection services, IConfiguration config, string sectionName)
        where T : class, new()
    {
        var settings = config.GetSection(sectionName).Get<T>() ?? new T();

        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(settings, new ValidationContext(settings), errors, validateAllProperties: true))
        {
            var invalid = errors.SelectMany(e => e.MemberNames).Select(member => $"{sectionName}:{member}");
            throw new InvalidOperationException(
                $"Missing or invalid configuration: {string.Join(", ", invalid)}. " +
                "Set it in SharedConfig/appsettings.json, user-secrets or environment variables.");
        }

        services.AddSingleton(settings);
        return settings;
    }
}
