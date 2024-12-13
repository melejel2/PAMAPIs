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
            IConfiguration configuration)
        {
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
                var users = await _dbContext.Users
                    .Where(u => u.UserEmail == model.Email)
                    .ToListAsync();

                if (users == null || users.Count == 0)
                {
                    return Unauthorized("Invalid username.");
                }

                var user = users.FirstOrDefault(u => u.UserPassword == model.Password);
                if (user == null)
                {
                    return Unauthorized("Invalid password.");
                }

                var token = GenerateJwtToken(user);

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
        public async Task<IActionResult> GetUserSites([FromQuery] int countryId)
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

                var sites = await GetUserSitesAsync(user, countryId);
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
                    // Access to all countries
                    return await _dbContext.Countries.ToListAsync();

                case 2:
                case 3:
                case 6:
                case 8:
                case 9:
                    // Has a primary CountryId and may have additional countries in UserCountries
                    var role2xCountries = new List<Country>();

                    if (user.CountryId != 0)
                    {
                        var primaryCountry = await _dbContext.Countries.FindAsync(user.CountryId);
                        if (primaryCountry != null) role2xCountries.Add(primaryCountry);
                    }

                    var additionalCountries2x = await _dbContext.UserCountries
                        .Where(uc => uc.UsrId == user.UsrId)
                        .Join(_dbContext.Countries,
                            uc => uc.CountryId,
                            c => c.CountryId,
                            (uc, c) => c)
                        .ToListAsync();

                    role2xCountries.AddRange(additionalCountries2x);
                    return role2xCountries.Distinct().ToList();

                case 4:
                case 5:
                case 7:
                case 10:
                    // Has primary CountryId & SiteId, may have additional UserCountries and UserSites
                    var role4xCountries = new List<Country>();

                    // Add primary country if available
                    if (user.CountryId != 0)
                    {
                        var primaryCountry = await _dbContext.Countries.FindAsync(user.CountryId);
                        if (primaryCountry != null) role4xCountries.Add(primaryCountry);
                    }

                    // Add countries from UserCountries
                    var additionalCountries4x = await _dbContext.UserCountries
                        .Where(uc => uc.UsrId == user.UsrId)
                        .Join(_dbContext.Countries,
                            uc => uc.CountryId,
                            c => c.CountryId,
                            (uc, c) => c)
                        .ToListAsync();

                    role4xCountries.AddRange(additionalCountries4x);

                    // Add countries from UserSites
                    var countriesFromUserSites = await _dbContext.UserSites
                        .Where(us => us.UsrId == user.UsrId)
                        .Join(_dbContext.Sites,
                            us => us.SiteId,
                            s => s.SiteId,
                            (us, s) => s.CountryId)
                        .Distinct()
                        .Join(_dbContext.Countries,
                            cid => cid,
                            c => c.CountryId,
                            (cid, c) => c)
                        .ToListAsync();

                    role4xCountries.AddRange(countriesFromUserSites);

                    return role4xCountries.Distinct().ToList();

                default:
                    return new List<Country>();
            }
        }

        private async Task<List<Site>> GetUserSitesAsync(User user, int countryId)
        {
            switch (user.RoleId)
            {
                case 1: // Admin
                    // Admin has full access to all sites of the requested country
                    return await _dbContext.Sites
                        .Where(s => s.CountryId == countryId)
                        .ToListAsync();

                case 2:
                case 3:
                case 6:
                case 8:
                case 9:
                    // These roles have a primary country and possibly additional countries (full access to those countries).
                    var accessibleCountries2x = await GetAccessibleCountriesFor2xRoles(user);
                    if (!accessibleCountries2x.Contains(countryId))
                    {
                        return new List<Site>();
                    }
                    return await _dbContext.Sites
                        .Where(s => s.CountryId == countryId)
                        .ToListAsync();

                case 4:
                case 5:
                case 7:
                case 10:
                    // For these roles:
                    // - If countryId == user's primary CountryId and not in UserCountries, return the primary site plus any UserSites in that country.
                    // - If countryId is in UserCountries, return all sites in that country.
                    // - If countryId is only from UserSites, return only those assigned sites.

                    var (primaryCountryId, primarySiteId) = (user.CountryId, user.SiteId);

                    // Get all userCountries and userSites data
                    var userCountries = await GetUserCountriesFor4xRoles(user);
                    var userCountryIds = userCountries.Select(c => c.CountryId).Distinct().ToList();

                    // Check if requested country is user's primary country
                    bool isPrimaryCountry = (countryId == primaryCountryId && primaryCountryId != 0);

                    // Check if requested country is in userCountries
                    bool inUserCountries = userCountryIds.Contains(countryId);

                    // Collect sites
                    if (inUserCountries)
                    {
                        // Full access to that country's sites
                        return await _dbContext.Sites
                            .Where(s => s.CountryId == countryId)
                            .ToListAsync();
                    }

                    // If not in userCountries:
                    // Check userSites for sites in the requested country
                    var userSiteIds = await _dbContext.UserSites
                        .Where(us => us.UsrId == user.UsrId)
                        .Select(us => us.SiteId)
                        .ToListAsync();

                    var userSitesInCountry = await _dbContext.Sites
                        .Where(s => userSiteIds.Contains(s.SiteId) && s.CountryId == countryId)
                        .ToListAsync();

                    if (isPrimaryCountry)
                    {
                        // Add primary site if it belongs to this country
                        if (primarySiteId != 0)
                        {
                            var primarySite = await _dbContext.Sites
                                .FirstOrDefaultAsync(s => s.SiteId == primarySiteId && s.CountryId == countryId);
                            if (primarySite != null && !userSitesInCountry.Any(s => s.SiteId == primarySite.SiteId))
                            {
                                userSitesInCountry.Add(primarySite);
                            }
                        }
                        return userSitesInCountry;
                    }
                    else
                    {
                        // Country is not primary and not in userCountries, so we only return sites from userSites that match this country
                        return userSitesInCountry;
                    }

                default:
                    return new List<Site>();
            }
        }

        private async Task<List<Country>> GetUserCountriesFor4xRoles(User user)
        {
            var countries = new List<Country>();

            if (user.CountryId != 0)
            {
                var primaryCountry = await _dbContext.Countries.FindAsync(user.CountryId);
                if (primaryCountry != null) countries.Add(primaryCountry);
            }

            var additionalCountries = await _dbContext.UserCountries
                .Where(uc => uc.UsrId == user.UsrId)
                .Join(_dbContext.Countries,
                    uc => uc.CountryId,
                    c => c.CountryId,
                    (uc, c) => c)
                .ToListAsync();

            countries.AddRange(additionalCountries);

            var countriesFromUserSites = await _dbContext.UserSites
                .Where(us => us.UsrId == user.UsrId)
                .Join(_dbContext.Sites,
                    us => us.SiteId,
                    s => s.SiteId,
                    (us, s) => s.CountryId)
                .Distinct()
                .Join(_dbContext.Countries,
                    cid => cid,
                    c => c.CountryId,
                    (cid, c) => c)
                .ToListAsync();

            countries.AddRange(countriesFromUserSites);

            return countries.Distinct().ToList();
        }

        private async Task<List<int>> GetAccessibleCountriesFor2xRoles(User user)
        {
            var countryIds = new List<int>();

            if (user.CountryId != 0)
            {
                countryIds.Add(user.CountryId);
            }

            var additionalCountryIds = await _dbContext.UserCountries
                .Where(uc => uc.UsrId == user.UsrId)
                .Select(uc => uc.CountryId)
                .ToListAsync();

            countryIds.AddRange(additionalCountryIds);

            return countryIds.Distinct().ToList();
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
