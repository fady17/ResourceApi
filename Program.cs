using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation.AspNetCore;

/// <summary>
/// This is the main entry point and configuration file for a protected Resource API.
/// Its primary responsibilities are to validate incoming JWT access tokens issued by the
/// central Identity Provider and to serve protected business data based on successful validation.
/// </summary>
var builder = WebApplication.CreateBuilder(args);

// --- 1. CORS (Cross-Origin Resource Sharing) Configuration ---
// This configuration is essential for allowing the Next.js client application,
// which is hosted on a different origin (e.g., localhost:3000), to make requests
// to this API (e.g., hosted on localhost:7001).
var nextJsClientUrl = builder.Configuration.GetValue<string>("AllowedOrigins:NextJsClient") ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    // A default policy is configured here. For more complex scenarios with multiple clients,
    // named policies could be used.
    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins(nextJsClientUrl) // Explicitly trust requests from our Next.js client.
                  .AllowAnyHeader()             // Allow standard headers like 'Authorization' and 'Content-Type'.
                  .AllowAnyMethod();            // Allow standard HTTP methods (GET, POST, etc.).
        });
});

// --- 2. Authentication & OpenIddict Token Validation Configuration ---
// This section configures the API to use OpenIddict's validation handler as the
// primary mechanism for authenticating requests.
builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

// This registers the OpenIddict validation services.
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        // --- A. Issuer Configuration ---
        // This is a critical security setting. The API will only accept tokens
        // that have been issued by this specific URL. This URL must exactly match
        // the `iss` (issuer) claim in the JWTs produced by our Identity Provider.
        options.SetIssuer("https://localhost:7066/"); 

        // --- B. Audience Configuration ---
        // This is another vital security check. The API will only accept tokens
        // that contain this specific string in their `aud` (audience) claim. This ensures
        // that a token intended for another API cannot be used to access this one.
        // This value ("testclinic-api") is dynamically constructed by the IdP's ClaimsGenerationService
        // based on the tenant context (Provider's ShortCode).
        options.AddAudiences("testclinic-api");

        // --- C. Integration Configuration ---
        // Configure the ASP.NET Core host integration. This allows the OpenIddict
        // validation middleware to plug into the ASP.NET Core authentication pipeline.
        options.UseAspNetCore();

        // Configure the System.Net.Http integration. This allows OpenIddict to make
        // HTTP requests to the IdP's discovery endpoint (`.well-known/openid-configuration`)
        // to automatically retrieve the signing keys needed to validate the token's signature.
        options.UseSystemNetHttp();
    });

// --- 3. Authorization & API Services Configuration ---
// Add basic authorization services to the dependency injection container.
// This is required for using policies and the [Authorize] attribute (or .RequireAuthorization()).
builder.Services.AddAuthorization();

// Add services for Swagger/OpenAPI documentation generation.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- 4. Application Pipeline Configuration ---
// The order of middleware in this section is critical for security and functionality.
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // In development, enable Swagger UI and detailed error pages.
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// 1. Adds route matching to the pipeline. This must come before CORS and Auth.
app.UseRouting();

// 2. Applies the CORS policy to incoming requests. This must come after routing
//    and before authentication/authorization to ensure pre-flight (OPTIONS) requests are handled correctly.
app.UseCors();

// 3. Adds the authentication middleware to the pipeline. This middleware inspects the
//    'Authorization: Bearer <token>' header of incoming requests, and if a token is present,
//    it uses the OpenIddict validation handler to validate it and construct a ClaimsPrincipal for the user.
app.UseAuthentication();

// 4. Adds the authorization middleware. This middleware checks if the authenticated user
//    (represented by the ClaimsPrincipal) is permitted to access the requested endpoint.
app.UseAuthorization();

// --- 5. API Endpoint Definitions ---

/// <summary>
/// A protected API endpoint that requires a valid access token to be called.
/// It demonstrates reading claims from the validated token to perform its work.
/// </summary>
app.MapGet("/api/data", (ClaimsPrincipal user) =>
{
    // The `user` parameter is automatically populated by ASP.NET Core from the
    // ClaimsPrincipal that the authentication middleware created after validating the token.

    // Extracting standard OIDC claims from the token.
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier); // The 'sub' claim.
    var userName = user.Identity?.Name; // The 'name' claim.
    
    // Extracting our custom multi-tenancy claim. This is the key to data segregation.
    // An actual application would use this `providerId` in a database query's WHERE clause.
    var providerId = user.FindFirstValue("provider_id");
    
    // Reading the scopes to understand what permissions this token was granted.
    var scopes = string.Join(", ", user.FindAll("scope").Select(s => s.Value));
    
    // Return a successful response with the extracted information.
    return Results.Ok(new
    {
        Message = $"Hello {userName ?? "User"}! You've accessed protected data.",
        UserId = userId,
        ProviderId = providerId, // This proves the tenant context was passed from the IdP.
        GrantedScopes = scopes,
        Claims = user.Claims.Select(c => new { c.Type, c.Value }).ToList() // For debugging: show all claims.
    });
})
.RequireAuthorization() // This is the Minimal API equivalent of the [Authorize] attribute.
.WithOpenApi();

/// <summary>
/// An example of an unprotected endpoint that can be accessed by anyone without a token.
/// </summary>
app.MapGet("/api/public-data", () => Results.Ok(new { Message = "This is public data." }));

app.Run();