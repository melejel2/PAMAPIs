using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using PAMAPIs.Models;
using PAMAPIs.Services;
using PAMAPIs.Data;


namespace PAM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly FuzzyMatchingService _fuzzyMatchingService;
        private readonly PAMContext _dbContext;
        private readonly Common _common;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<LoginController> _logger;
        private readonly IConfiguration _configuration;

        public LoginController(
            PAMContext dbContext,
            Common common,
            IWebHostEnvironment hostingEnvironment,
            ILoggerFactory loggerFactory,
            FuzzyMatchingService fuzzyMatchingService,
            IConfiguration configuration)
        {
            _fuzzyMatchingService = fuzzyMatchingService;
            _dbContext = dbContext;
            _common = common;
            _hostingEnvironment = hostingEnvironment;
            _logger = loggerFactory.CreateLogger<LoginController>();
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            try
            {
                // Fetch users with the same email
                var users = await _dbContext.Users
                    .Where(u => u.UserEmail == model.Email)
                    .ToListAsync();

                if (users == null || users.Count == 0)
                {
                    return Unauthorized("Invalid username.");
                }

                // Match the user with the correct password
                var user = users.FirstOrDefault(u => u.UserPassword == model.Password);
                if (user == null)
                {
                    return Unauthorized("Invalid password.");
                }

                // Generate token
                var token = GenerateJwtToken(user);

                // Update last login time
                user.LastLogin = DateTime.Now;
                _dbContext.Entry(user).State = EntityState.Modified;
                await _dbContext.SaveChangesAsync();

                if (!user.UpdatePass)
                {
                    return Ok(new { requirePasswordUpdate = true, token });
                }

                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during login");
                return StatusCode(500, "An error occurred. Please try again.");
            }
        }

        [Authorize]
        [HttpGet("usercountries")]
        public async Task<IActionResult> GetUserCountries()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                // Use the existing helper method
                var countries = await GetUserCountriesAsync(user);
                return Ok(countries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUserCountries");
                return StatusCode(500, "An error occurred while retrieving user countries.");
            }
        }


        [Authorize]
        [HttpGet("usersites")]
        public async Task<IActionResult> GetUserSites()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                // Use the existing helper method
                var sites = await GetUserSitesAsync(user);
                return Ok(sites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUserSites");
                return StatusCode(500, "An error occurred while retrieving user sites.");
            }
        }
       private async Task<List<Country>> GetUserCountriesAsync(User user)
        {
            switch (user.RoleId)
            {
                case 1: // Admin
                    return await _dbContext.Countries.ToListAsync();
                case 2:
                case 3:
                case 6:
                case 8:
                case 9:
                    return await _dbContext.UserCountries
                        .Where(uc => uc.UsrId == user.UsrId)
                        .Join(_dbContext.Countries,
                            uc => uc.CountryId,
                            c => c.CountryId,
                            (uc, c) => c)
                        .ToListAsync();
                case 4:
                case 5:
                case 7:
                    var countriesFromSites = await _dbContext.UserSites
                        .Where(us => us.UsrId == user.UsrId)
                        .Join(_dbContext.Sites,
                            us => us.SiteId,
                            s => s.SiteId,
                            (us, s) => s.CountryId)
                        .Distinct()
                        .Join(_dbContext.Countries,
                            cId => cId,
                            c => c.CountryId,
                            (cId, c) => c)
                        .ToListAsync();

                    if (user.CountryId != 0)
                    {
                        var userCountry = await _dbContext.Countries.FindAsync(user.CountryId);
                        if (userCountry != null && !countriesFromSites.Any(c => c.CountryId == user.CountryId))
                        {
                            countriesFromSites.Add(userCountry);
                        }
                    }

                    return countriesFromSites;
                default:
                    return new List<Country>();
            }
        }

        private async Task<List<Site>> GetUserSitesAsync(User user)
        {
            switch (user.RoleId)
            {
                case 1: // Admin
                    return await _dbContext.Sites.ToListAsync();

                case 2:
                case 3:
                case 6:
                case 8:
                case 9:
                    // Retrieve all country IDs the user has access to
                    var countryIds = await GetUserCountryIdsAsync(user);

                    if (!countryIds.Any())
                    {
                        // If no countries assigned, return empty list or handle accordingly
                        return new List<Site>();
                    }

                    // Use .Contains() with an array to avoid EF Core generating OPENJSON
                    return await _dbContext.Sites
                        .Where(s => countryIds.Contains(s.CountryId))
                        .ToListAsync();

                case 4:
                case 5:
                case 7:
                // Existing logic for roles 4, 5, and 7
                // ...

                default:
                    return new List<Site>();
            }
        }
        private async Task<int[]> GetUserCountryIdsAsync(User user)
        {
            var countryIds = new List<int>();

            // Add primary country if assigned
            if (user.CountryId != 0)
            {
                countryIds.Add(user.CountryId);
            }

            // Add additional countries from UserCountries
            var additionalCountryIds = await _dbContext.UserCountries
                .Where(uc => uc.UsrId == user.UsrId)
                .Select(uc => uc.CountryId)
                .ToListAsync();

            countryIds.AddRange(additionalCountryIds);

            // Remove duplicates if any
            return countryIds.Distinct().ToArray();
        }




        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];

            if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer))
            {
                throw new InvalidOperationException("JWT configuration is missing or invalid.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UsrId.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.RoleId.ToString()),
                new Claim("CountryId", user.CountryId.ToString()),
                new Claim("SiteId", user.SiteId.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddDays(Convert.ToDouble(_configuration["Jwt:ExpireDays"] ?? "7"));

            var token = new JwtSecurityToken(
                jwtIssuer,
                jwtIssuer,
                claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class LoginModel
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }
    }
}