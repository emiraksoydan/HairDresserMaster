namespace Entities.Concrete.Dto
{
    public class AdminLoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AdminForgotPasswordDto
    {
        public string Email { get; set; } = string.Empty;
    }

    public class AdminResetPasswordDto
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
