using System.Net.Http.Headers;
using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RukuServiceApi.Context;
using RukuServiceApi.Models;
using RukuServiceApi.Services;

namespace RukuServiceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(
        ApplicationDbContext context,
        IAuthService authService,
        ILogger<AuthController> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory
        ) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IAuthService _authService = authService;
        private readonly ILogger<AuthController> _logger = logger;
        private readonly IConfiguration _configuration = configuration;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Uid))
                {
                    return BadRequest(new { message = "Email and UID are required" });
                }

                // Find user by email and UID (case-insensitive email comparison)
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email.Equals(request.Email, StringComparison.CurrentCultureIgnoreCase) && u.Uid == request.Uid
                );

                if (user == null)
                {
                    _logger.LogWarning(
                        "Login attempt failed for email: {Email}, UID: {Uid}. User not found in database.",
                        request.Email,
                        request.Uid
                    );
                    return Unauthorized(
                        new
                        {
                            message = "Invalid credentials",
                            details = "User not found. Make sure the user exists and credentials are correct.",
                        }
                    );
                }

                // Generate JWT token
                var token = _authService.GenerateJwtToken(user);

                _logger.LogInformation("User {Email} logged in successfully", user.Email);

                return Ok(
                    new
                    {
                        token = token,
                        user = new
                        {
                            id = user.Id,
                            email = user.Email,
                            displayName = user.DisplayName,
                            role = user.Role.ToString(),
                            emailVerified = user.EmailVerified,
                        },
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] CreateUserRequest request)
        {
            try
            {
                // Check if user already exists (case-insensitive email comparison)
                var existingUser = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == request.Email.ToLower() || u.Uid == request.Uid
                );

                if (existingUser != null)
                {
                    return Conflict(new { message = "User already exists" });
                }

                // Create new user
                var user = new User
                {
                    Email = request.Email,
                    Uid = request.Uid,
                    DisplayName = request.DisplayName ?? request.Email,
                    EmailVerified = request.EmailVerified,
                    Role = UserRole.Subscriber, // Default role
                    Provider = request.Provider,
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Generate JWT token
                var token = _authService.GenerateJwtToken(user);

                _logger.LogInformation("New user registered: {Email}", user.Email);

                return Ok(
                    new
                    {
                        token = token,
                        user = new
                        {
                            id = user.Id,
                            email = user.Email,
                            displayName = user.DisplayName,
                            role = user.Role.ToString(),
                            emailVerified = user.EmailVerified,
                        },
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                var clientId = _configuration["OAuth:Google:ClientId"];
                if (string.IsNullOrEmpty(clientId))
                {
                    return StatusCode(500, new { message = "Google OAuth is not configured" });
                }

                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                };

                var payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential, settings);

                var user = await FindOrCreateOAuthUser(
                    payload.Email,
                    payload.Subject,
                    payload.Name ?? payload.Email,
                    payload.EmailVerified,
                    ProviderList.Google
                );

                var token = _authService.GenerateJwtToken(user);

                _logger.LogInformation("Google login successful for {Email}", user.Email);

                return Ok(new
                {
                    token,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        displayName = user.DisplayName,
                        role = user.Role.ToString(),
                        emailVerified = user.EmailVerified,
                    }
                });
            }
            catch (InvalidJwtException)
            {
                return Unauthorized(new { message = "Invalid Google credential" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google login");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("github")]
        public async Task<IActionResult> GitHubLogin([FromBody] GitHubLoginRequest request)
        {
            try
            {
                var clientId = _configuration["OAuth:GitHub:ClientId"];
                var clientSecret = _configuration["OAuth:GitHub:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    return StatusCode(500, new { message = "GitHub OAuth is not configured" });
                }

                var httpClient = _httpClientFactory.CreateClient();

                // Exchange code for access token
                var tokenResponse = await httpClient.PostAsJsonAsync(
                    "https://github.com/login/oauth/access_token",
                    new { client_id = clientId, client_secret = clientSecret, code = request.Code }
                );

                tokenResponse.EnsureSuccessStatusCode();
                var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
                var tokenParams = System.Web.HttpUtility.ParseQueryString(tokenBody);
                var accessToken = tokenParams["access_token"];

                if (string.IsNullOrEmpty(accessToken))
                {
                    return Unauthorized(new { message = "Failed to obtain GitHub access token" });
                }

                // Get user info
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RukuServiceApi");

                var userResponse = await httpClient.GetAsync("https://api.github.com/user");
                userResponse.EnsureSuccessStatusCode();
                var ghUser = await userResponse.Content.ReadFromJsonAsync<JsonElement>();

                // Get primary email if not public
                var email = ghUser.TryGetProperty("email", out var emailProp) && emailProp.ValueKind != JsonValueKind.Null
                    ? emailProp.GetString()
                    : null;

                if (string.IsNullOrEmpty(email))
                {
                    var emailsResponse = await httpClient.GetAsync("https://api.github.com/user/emails");
                    if (emailsResponse.IsSuccessStatusCode)
                    {
                        var emails = await emailsResponse.Content.ReadFromJsonAsync<JsonElement>();
                        foreach (var e in emails.EnumerateArray())
                        {
                            if (e.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                            {
                                email = e.GetProperty("email").GetString();
                                break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(email))
                {
                    return BadRequest(new { message = "Could not retrieve email from GitHub" });
                }

                var githubId = ghUser.GetProperty("id").GetInt64().ToString();
                var displayName = ghUser.TryGetProperty("name", out var nameProp) && nameProp.ValueKind != JsonValueKind.Null
                    ? nameProp.GetString() ?? email
                    : email;

                var user = await FindOrCreateOAuthUser(email, githubId, displayName, true, ProviderList.GitHub);

                var token = _authService.GenerateJwtToken(user);

                _logger.LogInformation("GitHub login successful for {Email}", user.Email);

                return Ok(new
                {
                    token,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        displayName = user.DisplayName,
                        role = user.Role.ToString(),
                        emailVerified = user.EmailVerified,
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during GitHub login");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private async Task<User> FindOrCreateOAuthUser(
            string email, string providerUid, string displayName, bool emailVerified, ProviderList provider)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email.ToLower() == email.ToLower() && u.Provider == provider);

            if (user != null)
            {
                return user;
            }

            user = new User
            {
                Email = email,
                Uid = providerUid,
                DisplayName = displayName,
                EmailVerified = emailVerified,
                Role = UserRole.Subscriber,
                Provider = provider
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }
    }

    public class GoogleLoginRequest
    {
        public string Credential { get; set; } = string.Empty;
    }

    public class GitHubLoginRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Uid { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Uid { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public bool EmailVerified { get; set; }
        public ProviderList Provider { get; set; }
    }
}
