using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GatherUp.API.DTOs;
using GatherUp.BL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace GatherUp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PersonService _personService;
        private readonly IConfiguration _config;

        public AuthController(PersonService personService, IConfiguration config)
        {
            _personService = personService;
            _config        = config;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var manager = await _personService.GetManagerByEmailAndPasswordAsync(request.Email, request.Password);
            if (manager != null)
                return Ok(new LoginResponse(manager.Id, manager.Name, manager.Email,
                    BuildToken(manager.Id, manager.Name, manager.Email, "Manager"), "Manager"));

            var participant = await _personService.GetParticipantByEmailAndPasswordAsync(request.Email, request.Password);
            if (participant != null)
            {
                var matchingManager = await _personService.GetManagerByEmailAsync(request.Email);
                if (matchingManager != null)
                    return Ok(new LoginResponse(matchingManager.Id, matchingManager.Name, matchingManager.Email,
                        BuildToken(matchingManager.Id, matchingManager.Name, matchingManager.Email, "Manager"), "Manager"));

                var autoManager = await _personService.EnsureManagerRecordAsync(participant);
                int useId     = autoManager?.Id ?? participant.Id;
                string useName  = autoManager?.Name  ?? participant.Name;
                string useEmail = autoManager?.Email ?? participant.Email;
                return Ok(new LoginResponse(useId, useName, useEmail,
                    BuildToken(useId, useName, useEmail, "Manager"), "Manager"));
            }

            return Unauthorized(new { message = "פרטי כניסה שגויים." });
        }

        [AllowAnonymous]
        [HttpPost("register")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var user = await _personService.RegisterUserAsync(request.Name, request.Email, request.Password);
            return StatusCode(StatusCodes.Status201Created,
                new LoginResponse(user.Id, user.Name, user.Email,
                    BuildToken(user.Id, user.Name, user.Email, "Manager"), "Manager"));
        }

        private string BuildToken(int id, string name, string email, string role)
        {
            var jwtKey = _config["Jwt:Key"];
            var jwtIssuer = _config["Jwt:Issuer"];
            var jwtAudience = _config["Jwt:Audience"];
            
            if (string.IsNullOrEmpty(jwtKey))
                throw new InvalidOperationException("JWT:Key configuration is missing or empty");
            if (string.IsNullOrEmpty(jwtIssuer))
                throw new InvalidOperationException("JWT:Issuer configuration is missing or empty");
            if (string.IsNullOrEmpty(jwtAudience))
                throw new InvalidOperationException("JWT:Audience configuration is missing or empty");

            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            int expiry = int.Parse(_config["Jwt:ExpiryMinutes"] ?? "120");
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Name,               name),
                new Claim(ClaimTypes.Role,               role),
                new Claim("userId",                      id.ToString())
            };
            var token = new JwtSecurityToken(
                issuer:             jwtIssuer,
                audience:           jwtAudience,
                claims:             claims,
                expires:            DateTime.UtcNow.AddMinutes(expiry),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
