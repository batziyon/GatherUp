using System.ComponentModel.DataAnnotations;

namespace GatherUp.API.DTOs
{
    public record LoginRequest(
        [Required, EmailAddress] string Email,
        [Required, MinLength(3)] string Password
    );

    public record RegisterRequest(
        [Required, MinLength(2)] string Name,
        [Required, EmailAddress] string Email,
        [Required, MinLength(5)] string Password
    );

    public record LoginResponse(
        int    Id,
        string Name,
        string Email,
        string? Token,
        string? Role
    );
}
