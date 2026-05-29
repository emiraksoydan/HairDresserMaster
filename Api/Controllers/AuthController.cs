using Business.Abstract;
using Business.Resources;
using Core.Extensions;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(IAuthService authService, IAdminUserService adminUserService) : ControllerBase
    {
        private Guid CurrentAdminId()
        {
            var idStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User?.FindFirst("identifier")?.Value
                     ?? User?.FindFirst("sub")?.Value;
            return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
        }

        private IActionResult? AdminOnly()
        {
            if (!User.ClaimRoles().Contains("Admin"))
                return StatusCode(403, new ErrorResult(Messages.AdminOperationRequiresAdminRole));
            return null;
        }
        [AllowAnonymous]
        [EnableRateLimiting("send-otp")]
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] UserForSendOtpDto req)
        {
            var r = await authService.SendOtpAsync(req);
            return r.Success ? Ok(r) : BadRequest(r);
        }
        [AllowAnonymous]
        [EnableRateLimiting("verify-otp")]
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] UserForVerifyDto req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var device = req.Device ?? Request.Headers.UserAgent.ToString();
            var res = await authService.VerifyOtpAsync(req, ip, device);
            return res.Success ? Ok(res) : BadRequest(res.Message);
        }
        [AllowAnonymous]
        [EnableRateLimiting("refresh-token")]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var res = await authService.RefreshAsync(req.RefreshToken, ip);
            return res.Success ? Ok(res) : Unauthorized(res.Message);
        }

        // ---- Admin auth (email + password) ----
        [AllowAnonymous]
        [HttpPost("admin/login")]
        public async Task<IActionResult> AdminLogin([FromBody] AdminLoginDto dto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var res = await authService.AdminLoginAsync(dto, ip);
            return res.Success ? Ok(res) : Unauthorized(new ErrorResult(res.Message));
        }

        [AllowAnonymous]
        [HttpPost("admin/forgot-password")]
        public async Task<IActionResult> AdminForgotPassword([FromBody] AdminForgotPasswordDto dto)
        {
            var res = await authService.AdminForgotPasswordAsync(dto);
            // Email enumeration'ı önlemek için her durumda 200 dönüyoruz.
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpPost("admin/reset-password")]
        public async Task<IActionResult> AdminResetPassword([FromBody] AdminResetPasswordDto dto)
        {
            var res = await authService.AdminResetPasswordAsync(dto);
            return res.Success ? Ok(res) : BadRequest(res.Message);
        }

        [AllowAnonymous]
        [HttpPost("admin/refresh")]
        public async Task<IActionResult> AdminRefresh([FromBody] RefreshTokenDto req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var res = await authService.AdminRefreshAsync(req.RefreshToken, ip);
            return res.Success ? Ok(res) : Unauthorized(res.Message);
        }

        [AllowAnonymous]
        [HttpPost("admin/logout")]
        public async Task<IActionResult> AdminLogout([FromBody] RefreshTokenDto req)
        {
            var res = await authService.AdminLogoutAsync(req?.RefreshToken ?? string.Empty);
            return Ok(res);
        }

        /// <summary>Giriş yapmış adminin profil özeti (panel /api/admin/admins/me ile aynı veri).</summary>
        [HttpGet("admin/me")]
        public async Task<IActionResult> GetAdminMe()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var adminId = CurrentAdminId();
            if (adminId == Guid.Empty)
                return Unauthorized(new ErrorResult(Messages.AdminAuthUserNotFound));

            var result = await adminUserService.GetMeAsync(adminId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // AllowAnonymous: erişim token'ı süresi dolmuşken de çıkış yapılabilsin.
        // Sahiplik doğrulaması refresh token hash'i üzerinden yapılır (RevokeAsync içinde).
        [AllowAnonymous]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] RefreshTokenDto req)
        {
            // JWT claim'den userId okumaya çalış; süresi dolmuş/yok ise null geçilir.
            // RevokeAsync token.UserId ile sahipliği doğrular; userId null ise atlar.
            Guid? userId = null;
            var userIdStr = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User?.FindFirst("identifier")?.Value
                         ?? User?.FindFirst("sub")?.Value;
            if (Guid.TryParse(userIdStr, out var parsed))
                userId = parsed;

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var r = await authService.RevokeAsync(userId, req.RefreshToken, ip);
            return r.Success ? Ok(r) : BadRequest(r);
        }
    }
}
