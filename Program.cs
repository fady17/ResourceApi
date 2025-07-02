using System.Security.Claims;
using Microsoft.AspNetCore.Authentication; // For AuthenticationScheme
using Microsoft.AspNetCore.Authorization;  // For [Authorize] equivalent
using Microsoft.IdentityModel.Tokens;      // For SymmetricSecurityKey (if using shared key encryption)
using OpenIddict.Validation.AspNetCore;  // For OpenIddictValidationAspNetCoreDefaults


var builder = WebApplication.CreateBuilder(args);
// --- Add CORS services ---
var nextJsClientUrl = builder.Configuration.GetValue<string>("AllowedOrigins:NextJsClient") ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy( // You can name this policy if you have multiple, e.g., "AllowNextJsApp"
        policy =>
        {
            policy.WithOrigins(nextJsClientUrl) // Specify the origin of your Next.js app
                  .AllowAnyHeader()             // Allows common headers like Authorization, Content-Type
                  .AllowAnyMethod();            // Allows GET, POST, PUT, DELETE, OPTIONS etc.
                  // .AllowCredentials();      // Only if you specifically need to send cookies from Next.js to API
                                                // and API needs to process them. For Bearer token auth, usually not needed.
        });
});
// --- End CORS services ---

// --- Configure Authentication and OpenIddict Validation ---
builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
// Or, if you want it as default:
// builder.Services.AddAuthentication(options =>
// {
//     options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
// })
// .AddScheme<AuthenticationSchemeOptions, OpenIddictValidationAspNetCoreHandler>( // Not needed if just setting default scheme
//     OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, options => { });
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {

        options.SetIssuer("https://localhost:7066/"); 

    
        options.AddAudiences("testclinic-api");




        // Configure the ASP.NET Core host integration.
        options.UseAspNetCore();

        // Configure the System.Net.Http integration (for discovery).
        options.UseSystemNetHttp();
    });

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();


// --- CORS Configuration (Essential for Next.js frontend) ---

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting(); // 1. UseRouting first

app.UseCors();    // 2. THEN UseCors - CRITICAL ORDERING

app.UseAuthentication(); // 3. THEN UseAuthentication
app.UseAuthorization();  // 4. THEN UseAuthorization




app.MapGet("/api/data", (ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier); // Standard OIDC 'sub' claim
    var userName = user.Identity?.Name; // From 'name' claim
    var providerId = user.FindFirstValue("provider_id"); // Your custom claim
    var scopes = string.Join(", ", user.FindAll("scope").Select(s => s.Value)); // Access token scopes
    
    return Results.Ok(new
    {
        Message = $"Hello {userName ?? "User"}! You've accessed protected data.",
        UserId = userId,
        ProviderId = providerId,
        GrantedScopes = scopes,
        Claims = user.Claims.Select(c => new { c.Type, c.Value }).ToList() // All claims in the token
    });
})
.RequireAuthorization()
.WithOpenApi();

// Example of an unprotected endpoint
app.MapGet("/api/public-data", () => Results.Ok(new { Message = "This is public data." }));

app.Run();

