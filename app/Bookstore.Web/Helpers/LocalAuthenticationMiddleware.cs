using Bookstore.Domain.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Bookstore.Web.Helpers
{
    public class LocalAuthenticationMiddleware : IMiddleware
    {
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";
        private const string CookieName = "LocalAuthentication";

        private readonly ICustomerService _customerService;

        public LocalAuthenticationMiddleware(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Path.StartsWithSegments("/Authentication/Login"))
            {
                var principal = CreateClaimsPrincipal();
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                await SaveCustomerDetailsAsync(principal);
                context.Response.Redirect("/");
                return;
            }

            if (context.Request.Cookies.ContainsKey(CookieName))
            {
                var principal = CreateClaimsPrincipal();
                context.User = principal;
                await SaveCustomerDetailsAsync(principal);
            }

            await next(context);
        }

        private static ClaimsPrincipal CreateClaimsPrincipal()
        {
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));
            return new ClaimsPrincipal(identity);
        }

        private async Task SaveCustomerDetailsAsync(ClaimsPrincipal principal)
        {
            var identity = (ClaimsIdentity)principal.Identity!;
            var dto = new CreateOrUpdateCustomerDto(
                identity.FindFirst("nameidentifier")!.Value,
                identity.Name ?? string.Empty,
                identity.FindFirst("given_name")!.Value,
                identity.FindFirst("family_name")!.Value);

            await _customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
