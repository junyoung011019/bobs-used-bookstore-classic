using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Bookstore.Common;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.AWS.Logger;
using NLog.Config;
using NLog.Web;
using NLogTarget = NLog.Targets.Target;
using System;
using System.IO;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Initialize BookstoreConfiguration with IConfiguration
BookstoreConfiguration.Initialize(builder.Configuration);

// Load AWS SSM parameters if needed
LoadAwsConfiguration(builder.Configuration);

// Configure NLog
ConfigureLogging(builder.Configuration);
builder.Host.UseNLog();

// Add MVC
builder.Services.AddControllersWithViews();

// Configure EF Core
var connectionString = builder.Configuration.GetConnectionString("BookstoreDatabaseConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register domain services
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

// Register repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

// Register file service
if (builder.Configuration["Services:FileService"] == "aws")
{
    builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
    builder.Services.AddScoped<IFileService>(sp =>
    {
        var s3 = sp.GetRequiredService<IAmazonS3>();
        var bucketName = builder.Configuration["Files:BucketName"] ?? string.Empty;
        var cloudFrontDomain = builder.Configuration["Files:CloudFrontDomain"] ?? string.Empty;
        return new S3FileService(s3, bucketName, cloudFrontDomain);
    });
}
else
{
    builder.Services.AddScoped<IFileService>(sp =>
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var webRootPath = Path.Combine(env.WebRootPath ?? env.ContentRootPath, "Content");
        return new LocalFileService(webRootPath);
    });
}

// Register image validation service
if (builder.Configuration["Services:ImageValidationService"] == "aws")
{
    builder.Services.AddSingleton<IAmazonRekognition, AmazonRekognitionClient>();
    builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
}
else
{
    builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
}

// Configure authentication
var authService = builder.Configuration["Services:Authentication"];
if (authService == "aws")
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Cognito:LocalClientId"];
        options.MetadataAddress = builder.Configuration["Authentication:Cognito:MetadataAddress"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.UseTokenLifetime = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "cognito:username",
            RoleClaimType = "cognito:groups"
        };
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = ctx =>
            {
                var returnUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}/signin-oidc";
                ctx.ProtocolMessage.RedirectUri = returnUrl;
                return System.Threading.Tasks.Task.CompletedTask;
            },
            OnAuthorizationCodeReceived = ctx =>
            {
                ctx.TokenEndpointRequest!.RedirectUri = $"{ctx.Request.Scheme}://{ctx.Request.Host}/signin-oidc";
                return System.Threading.Tasks.Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var service = ctx.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                var dto = new CreateOrUpdateCustomerDto(
                    identity.GetSub(),
                    identity.Name,
                    identity.FindFirst(y => y.Type.Contains("givenname"))?.Value ?? string.Empty,
                    identity.FindFirst(y => y.Type.Contains("surname"))?.Value ?? string.Empty);
                await service.CreateOrUpdateCustomerAsync(dto);
            }
        };
    });
}
else
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Authentication/Login";
        });
    builder.Services.AddScoped<LocalAuthenticationMiddleware>();
}

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

// Local auth middleware must run before UseAuthentication
if (authService != "aws")
{
    app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "Admin_default",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { area = "Admin" },
    constraints: null,
    dataTokens: new { area = "Admin" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static void LoadAwsConfiguration(IConfiguration configuration)
{
    var rootPath = "/" + Constants.AppName;

    if (configuration["Services:Database"] == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParameterRequest { Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection" };
        var response = client.GetParameterAsync(request).GetAwaiter().GetResult();
        BookstoreConfiguration.AddSetting("ConnectionStrings:BookstoreDatabaseConnection", response.Parameter.Value);
    }

    if (configuration["Services:Authentication"] == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParametersByPathRequest { Path = $"{rootPath}/Authentication/", Recursive = true };
        var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
        foreach (var parameter in response.Parameters)
            BookstoreConfiguration.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty).Replace("/", ":"), parameter.Value);
    }

    if (configuration["Services:FileService"] == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParametersByPathRequest { Path = $"{rootPath}/Files/", Recursive = true };
        var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
        foreach (var parameter in response.Parameters)
            BookstoreConfiguration.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty).Replace("/", ":"), parameter.Value);
    }
}

static void ConfigureLogging(IConfiguration configuration)
{
    var config = new LoggingConfiguration();
    NLogTarget loggingTarget;

    if (configuration["Services:LoggingService"] == "aws")
        loggingTarget = new AWSTarget { LogGroup = Constants.AppName };
    else
        loggingTarget = new NLog.Targets.DebuggerTarget();

    config.AddTarget("target", loggingTarget);
    config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, loggingTarget));
    LogManager.Configuration = config;
}
