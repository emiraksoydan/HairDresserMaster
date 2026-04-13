using Business.Abstract;
using Core.Extensions;
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
    public class AuthController(IAuthService authService) : ControllerBase
    {
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
