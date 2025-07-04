# Orjnz.ResourceApi.TestClinic - Protected Resource API

This repository contains a .NET 7 Minimal API that serves as a protected resource server within the Orjnz OpenID Connect (OIDC) ecosystem. Its primary purpose is to demonstrate how a backend service can trust and validate JWT access tokens issued by the central `Orjnz.IdentityProvider` and serve data based on the claims contained within those tokens.

This API represents a "tenant-specific" service, in this case for `testclinic`. It is configured to only accept tokens intended for the `testclinic-api` audience.

## 1. API Overview

This API provides a proof-of-concept for a secure service layer. Its key responsibilities are:

- **Token Validation:** Securely validates incoming Bearer tokens to ensure they are authentic, untampered, and issued by the trusted Identity Provider.
- **Authorization:** Protects endpoints, ensuring only requests with a valid access token can access them.
- **Claim-Based Logic:** Demonstrates how to read claims (e.g., `sub`, `name`, and the custom `provider_id`) from a validated token to return user-specific or tenant-specific data.
- **CORS Configuration:** Includes the necessary Cross-Origin Resource Sharing (CORS) policy to allow requests from the designated Next.js frontend application.

## 2. Authentication and Authorization

This API does not perform user authentication itself. Instead, it delegates that responsibility to the central **Orjnz Identity Provider**. It secures its endpoints by implementing an OIDC token validation handler.

The validation process ensures that any incoming JWT access token meets the following criteria:

1.  **Trusted Issuer:** The token's `iss` (issuer) claim must match the URL of our central Identity Provider (`https.localhost:7066/`).
2.  **Correct Audience:** The token's `aud` (audience) claim must contain `testclinic-api`. This prevents tokens meant for other services from being used here.
3.  **Valid Signature:** The token's signature is cryptographically verified against the public keys fetched from the Identity Provider's discovery endpoint.
4.  **Not Expired:** The token's `exp` (expiration) claim is checked to ensure the token is still valid.

Endpoints are protected using the `.RequireAuthorization()` method, which triggers this validation pipeline.

## 3. Endpoints Documentation

This API exposes the following endpoints:

### Protected Endpoint

-   **GET `/api/data`**
    -   **Description:** Retrieves protected data for the authenticated user. This endpoint requires a valid Bearer token in the `Authorization` header.
    -   **Authorization:** Required.
    -   **Successful Response (200 OK):** A JSON object containing a welcome message and data extracted from the access token.
    -   **Example Response Body:**
        ```json
        {
          "message": "Hello user@example.com! You've accessed protected data.",
          "userId": "a1b2c3d4-e5f6-...",
          "providerId": "f7g8h9i0-j1k2-...",
          "grantedScopes": "openid profile email testclinic-api",
          "claims": [
            { "type": "sub", "value": "a1b2c3d4-e5f6-..." },
            { "type": "name", "value": "user@example.com" },
            { "type": "provider_id", "value": "f7g8h9i0-j1k2-..." }
          ]
        }
        ```
    -   **Error Response (401 Unauthorized):** Returned if no token is provided, or the token is invalid or expired.

### Public Endpoint

-   **GET `/api/public-data`**
    -   **Description:** Retrieves public data that does not require authentication.
    -   **Authorization:** Not required.
    -   **Successful Response (200 OK):** A JSON object with a public message.
    -   **Example Response Body:**
        ```json
        {
          "message": "This is public data."
        }
        ```

## 4. Environment Configuration

This API requires the following configuration values, typically set in `appsettings.Development.json` or environment variables for production.

```json
{
  "AllowedOrigins": {
    "NextJsClient": "http://localhost:3000"
  },
  "OpenIddict": {
    "Validation": {
      "Issuer": "https://localhost:7066/"
    }
  }
}
```

-   `AllowedOrigins:NextJsClient`: The URL of the frontend client application that is permitted to make requests to this API.
-   `OpenIddict:Validation:Issuer`: The URL of the trusted Identity Provider. **This must match the IdP's public-facing URL exactly.**

## 5. Running the Application

1.  **Prerequisites:**
    -   .NET 7 SDK
    -   The `Orjnz.IdentityProvider.Web` project must be running, as this API depends on it for token validation.

2.  **Configuration:**
    -   Ensure your `appsettings.Development.json` file is configured with the correct `Issuer` URL for your running Identity Provider and the `NextJsClient` origin.

3.  **Execution:**
    -   Run the project from your IDE or use the .NET CLI:
        ```bash
        dotnet run
        ```

The API will be available at the URLs specified in its `launchSettings.json` file (e.g., `https://localhost:7001`).

🔗 Related Projects
	•	[Frontend (Next.js): nextauth-openiddict-client](https://github.com/fady17/Frontend-.git)
	•	(https://github.com/fady17/identityProvider-.git)
