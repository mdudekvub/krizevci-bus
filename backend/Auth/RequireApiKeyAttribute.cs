using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KrizevciBus.Api.Auth;

// Endpoint s ovim atributom prima zahtjev samo ako zaglavlje X-Api-Key
// odgovara ključu iz konfiguracije (Vehicles:ApiKey).
// Lokalno je ključ u user-secrets, na serveru u varijabli okoline Vehicles__ApiKey.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = config["Vehicles:ApiKey"];

        // Ako ključ nije postavljen, ne puštamo nikoga (sigurnije nego pustiti sve)
        if (string.IsNullOrEmpty(expected))
        {
            context.Result = new ObjectResult("API ključ nije postavljen na serveru.") { StatusCode = 503 };
            return;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!Matches(provided, expected))
            context.Result = new UnauthorizedResult();
    }

    // Usporedba u konstantnom vremenu, da se ključ ne može pogađati mjerenjem brzine odgovora
    static bool Matches(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}
