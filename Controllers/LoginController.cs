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

        private string GenerateJwtToken(User user, string countryName, string siteName)
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
                new Claim("SiteId", user.SiteId.ToString()),
                new Claim("CountryName", countryName ?? string.Empty),
                new Claim("SiteName", siteName ?? string.Empty)
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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.UserEmail == model.Email && u.UserPassword == model.Password);

                if (user == null)
                {
                    return Unauthorized("Invalid email or password.");
                }

                // Fetch CountryName
                string countryName = null;
                if (user.CountryId != 0)
                {
                    var country = await _dbContext.Countries
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.CountryId == user.CountryId);
                    countryName = country?.CountryName;
                }

                // Fetch SiteName
                string siteName = null;
                if (user.SiteId != 0)
                {
                    var site = await _dbContext.Sites
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.SiteId == user.SiteId);
                    siteName = site?.SiteName;
                }

                var token = GenerateJwtToken(user, countryName, siteName);

                user.LastLogin = DateTime.UtcNow;
                _dbContext.Entry(user).State = EntityState.Modified;
                await _dbContext.SaveChangesAsync();

                if (!user.UpdatePass)
                {
                    return Ok(new
                    {
                        requirePasswordUpdate = true,
                        token
                    });
                }

                return Ok(new
                {
                    token,
                    username = user.UserName,
                    roleid = user.RoleId,
                    countryid = user.CountryId,
                    siteid = user.SiteId,
                    countryName = countryName,
                    siteName = siteName,
                    updatepass = user.UpdatePass,
                    requirePasswordUpdate = !user.UpdatePass
                });
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

                // Determine accessible site IDs for the user
                List<int> accessibleSiteIds = await GetAccessibleSiteIdsAsync(user, countryId);

                // Fetch sites within the specified country that the user has access to
                var sites = await _dbContext.Sites
                    .Where(s => s.CountryId == countryId && accessibleSiteIds.Contains(s.SiteId))
                    .Select(s => new
                    {
                        s.SiteId,
                        s.SiteName,
                        s.SiteCode,
                        s.Acronym, // Added Acronym
                        s.CountryId
                    })
                    .ToListAsync();

                return Ok(sites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUserSites");
                return StatusCode(500, "An error occurred while retrieving user sites.");
            }
        }


        // Modified Helper Method
        private async Task<List<int>> GetAccessibleSiteIdsAsync(User user, int countryId)
        {
            List<int> accessibleSiteIds = new List<int>();

            switch (user.RoleId)
            {
                case 1: // Admin
                        // Admin has access to all sites in the requested country
                    accessibleSiteIds = await _dbContext.Sites
                        .Where(s => s.CountryId == countryId)
                        .Select(s => s.SiteId)
                        .ToListAsync();
                    break;

                case 3:
                    // RoleId = 3 has access to ALL sites in the country
                    accessibleSiteIds = await _dbContext.Sites
                        .Where(s => s.CountryId == countryId)
                        .Select(s => s.SiteId)
                        .ToListAsync();
                    break;

                case 2:
                case 6:
                case 8:
                case 9:
                    // These roles now require site-by-site access for the requested country
                    accessibleSiteIds = await _dbContext.UserSites
                        .Where(us => us.UsrId == user.UsrId)
                        .Join(
                            _dbContext.Sites,
                            us => us.SiteId,
                            s => s.SiteId,
                            (us, s) => new { us, s }
                        )
                        .Where(joined => joined.s.CountryId == countryId)
                        .Select(joined => joined.s.SiteId)
                        .ToListAsync();

                    // Include the user’s primary site if it belongs to the requested country
                    if (user.SiteId != 0)
                    {
                        var primarySite = await _dbContext.Sites
                            .FirstOrDefaultAsync(s => s.SiteId == user.SiteId && s.CountryId == countryId);
                        if (primarySite != null && !accessibleSiteIds.Contains(primarySite.SiteId))
                        {
                            accessibleSiteIds.Add(primarySite.SiteId);
                        }
                    }
                    break;

                case 4:
                case 5:
                case 7:
                case 10:
                    // These roles have specific site access (UserSites) plus their primary site if it matches the country
                    accessibleSiteIds = await _dbContext.UserSites
                        .Where(us => us.UsrId == user.UsrId)
                        .Join(
                            _dbContext.Sites,
                            us => us.SiteId,
                            s => s.SiteId,
                            (us, s) => new { us, s }
                        )
                        .Where(joined => joined.s.CountryId == countryId)
                        .Select(joined => joined.s.SiteId)
                        .ToListAsync();

                    if (user.SiteId != 0)
                    {
                        var primarySite = await _dbContext.Sites
                            .FirstOrDefaultAsync(s => s.SiteId == user.SiteId && s.CountryId == countryId);
                        if (primarySite != null && !accessibleSiteIds.Contains(primarySite.SiteId))
                        {
                            accessibleSiteIds.Add(primarySite.SiteId);
                        }
                    }
                    break;

                default:
                    // Other roles have no site access
                    accessibleSiteIds = new List<int>();
                    break;
            }

            return accessibleSiteIds.Distinct().ToList();
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

        public class LoginModel
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }
    }
}
