using DataAccess.Abstract;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class SubscriptionController(
        IUserDal userDal,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<SubscriptionController> logger) : BaseApiController
    {
        /// <summary>
        /// Mevcut kullanıcının deneme/abonelik durumunu döner.
        /// Aboneliği bitmiş kullanıcılar da bu endpoint'e erişebilir (UserStatusFilter izin verir).
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var user = await userDal.Get(u => u.Id == CurrentUserId);
            if (user == null) return NotFound();

            var now = DateTime.UtcNow;
            var trialActive = user.TrialEndDate > now;
            var subscriptionActive = user.SubscriptionEndDate.HasValue && user.SubscriptionEndDate.Value > now;

            string status;
            if (user.IsBanned)
                status = "Banned";
            else if (subscriptionActive)
                status = "Active";
            else if (trialActive)
                status = "Trial";
            else
                status = "Expired";

            return Ok(new
            {
                success = true,
                data = new
                {
                    status,
                    trialEndDate = user.TrialEndDate,
                    subscriptionEndDate = user.SubscriptionEndDate,
                    isBanned = user.IsBanned,
                    banReason = user.BanReason,
                    autoRenew = user.SubscriptionAutoRenew,
                    cancelAtPeriodEnd = user.SubscriptionCancelAtPeriodEnd,
                    trialDaysLeft = trialActive ? (int)(user.TrialEndDate - now).TotalDays : 0,
                    subscriptionDaysLeft = subscriptionActive ? (int)(user.SubscriptionEndDate!.Value - now).TotalDays : 0
                }
            });
        }

        /// <summary>
        /// Aktif aboneliği dönem sonunda sonlandıracak şekilde işaretler.
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel()
        {
            var user = await userDal.Get(u => u.Id == CurrentUserId);
            if (user == null) return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

            var now = DateTime.UtcNow;
            var subscriptionActive = user.SubscriptionEndDate.HasValue && user.SubscriptionEndDate.Value > now;
            if (!subscriptionActive)
            {
                return BadRequest(new { success = false, message = "Aktif abonelik bulunamadı" });
            }

            user.SubscriptionAutoRenew = false;
            user.SubscriptionCancelAtPeriodEnd = true;
            await userDal.Update(user);

            return Ok(new { success = true, message = "Abonelik dönem sonunda iptal edilecek" });
        }

        /// <summary>
        /// Dönem sonu iptal isteğini geri alır (auto-renew yeniden açılır).
        /// </summary>
        [HttpPost("reactivate")]
        public async Task<IActionResult> Reactivate()
        {
            var user = await userDal.Get(u => u.Id == CurrentUserId);
            if (user == null) return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

            var now = DateTime.UtcNow;
            var subscriptionActive = user.SubscriptionEndDate.HasValue && user.SubscriptionEndDate.Value > now;
            if (!subscriptionActive)
            {
                return BadRequest(new { success = false, message = "Aktif abonelik yok, lütfen yeniden satın alın" });
            }

            user.SubscriptionAutoRenew = true;
            user.SubscriptionCancelAtPeriodEnd = false;
            await userDal.Update(user);

            return Ok(new { success = true, message = "Abonelik yeniden etkinleştirildi" });
        }

        public sealed record CreatePaytrTokenRequest(string Plan, int Months = 1);

        public sealed record CreatePaytrTokenResponse(string Token, string MerchantOid, int PaymentAmount);

        /// <summary>
        /// PayTR iFrame token üretir (server-side).
        /// Frontend bu token ile PayTR iframe URL'ini açar.
        /// </summary>
        [HttpPost("paytr/token")]
        public async Task<IActionResult> CreatePaytrToken([FromBody] CreatePaytrTokenRequest req)
        {
            if (req.Months <= 0 || req.Months > 12)
                return BadRequest(new { success = false, message = "Geçersiz ay sayısı" });

            var merchantId = configuration["PayTR:MerchantId"];
            var merchantKey = configuration["PayTR:MerchantKey"];
            var merchantSalt = configuration["PayTR:MerchantSalt"];
            var currency = configuration["PayTR:Currency"] ?? "TL";
            var okUrl = configuration["PayTR:OkUrl"];
            var failUrl = configuration["PayTR:FailUrl"];
            var testMode = int.TryParse(configuration["PayTR:TestMode"], out var tm) ? tm : 0;

            if (string.IsNullOrWhiteSpace(merchantId) ||
                string.IsNullOrWhiteSpace(merchantKey) ||
                string.IsNullOrWhiteSpace(merchantSalt) ||
                string.IsNullOrWhiteSpace(okUrl) ||
                string.IsNullOrWhiteSpace(failUrl))
            {
                return StatusCode(500, new { success = false, message = "PayTR ayarları eksik (MerchantId/MerchantKey/MerchantSalt/OkUrl/FailUrl)" });
            }

            var plan = (req.Plan ?? "").Trim();
            if (plan != "FreeBarber" && plan != "BarberStore")
                return BadRequest(new { success = false, message = "Geçersiz plan" });

            var user = await userDal.Get(u => u.Id == CurrentUserId);
            if (user == null) return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });

            // Email JWT claim'den alınır (PayTR zorunlu)
            var email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email))
            {
                // Entegrasyon için email claim'inin set edilmesi önerilir.
                // Geçici fallback: userId üzerinden deterministic email üret.
                email = $"{user.Id:D}@gumusmakas.local";
            }

            var priceTry = plan == "FreeBarber"
                ? int.TryParse(configuration["PayTR:FreeBarberMonthlyPriceTry"], out var p1) ? p1 : 149
                : int.TryParse(configuration["PayTR:BarberStoreMonthlyPriceTry"], out var p2) ? p2 : 299;

            var totalTry = priceTry * req.Months;
            var paymentAmount = totalTry * 100; // kuruş

            var userIp = GetUserIp();
            var merchantOid = $"sub_{user.Id:D}_{plan}_{req.Months}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..64];

            // Sepet: [["Plan Adı","fiyat","adet"]]
            var basketArr = new object[]
            {
                new object[] { $"{plan} Subscription ({req.Months} ay)", totalTry.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), 1 }
            };
            var basketJson = JsonSerializer.Serialize(basketArr);
            var userBasket = Convert.ToBase64String(Encoding.UTF8.GetBytes(basketJson));

            var noInstallment = 0;
            var maxInstallment = 0;
            var debugOn = 1;
            var timeoutLimit = 30;
            var userName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(userName)) userName = "Kullanıcı";
            var userPhone = user.PhoneNumber ?? "";
            var userAddress = "Türkiye";

            var hashStr = $"{merchantId}{userIp}{merchantOid}{email}{paymentAmount}{userBasket}{noInstallment}{maxInstallment}{currency}{testMode}";
            var paytrToken = ComputeBase64HmacSha256(hashStr + merchantSalt, merchantKey);

            var postVals = new Dictionary<string, string>
            {
                ["merchant_id"] = merchantId,
                ["user_ip"] = userIp,
                ["merchant_oid"] = merchantOid,
                ["email"] = email,
                ["payment_amount"] = paymentAmount.ToString(),
                ["currency"] = currency,
                ["paytr_token"] = paytrToken,
                ["user_basket"] = userBasket,
                ["no_installment"] = noInstallment.ToString(),
                ["max_installment"] = maxInstallment.ToString(),
                ["debug_on"] = debugOn.ToString(),
                ["test_mode"] = testMode.ToString(),
                ["timeout_limit"] = timeoutLimit.ToString(),
                ["user_name"] = userName,
                ["user_address"] = userAddress,
                ["user_phone"] = userPhone,
                ["merchant_ok_url"] = okUrl,
                ["merchant_fail_url"] = failUrl,
            };

            var client = httpClientFactory.CreateClient("PayTR");
            using var resp = await client.PostAsync("odeme/api/get-token", new FormUrlEncodedContent(postVals));
            var respBody = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogError("PayTR get-token HTTP failure: status={Status}, body={Body}, userId={UserId}", resp.StatusCode, respBody, CurrentUserId);
                return StatusCode(502, new { success = false, message = "PayTR get-token başarısız", details = respBody });
            }

            try
            {
                using var doc = JsonDocument.Parse(respBody);
                var status = doc.RootElement.GetProperty("status").GetString();
                if (status != "success")
                {
                    var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : "unknown";
                    logger.LogError("PayTR get-token returned failure: reason={Reason}, userId={UserId}", reason, CurrentUserId);
                    return BadRequest(new { success = false, message = "PayTR token alınamadı", reason });
                }

                var token = doc.RootElement.GetProperty("token").GetString();
                if (string.IsNullOrWhiteSpace(token))
                {
                    logger.LogError("PayTR get-token returned empty token, userId={UserId}", CurrentUserId);
                    return BadRequest(new { success = false, message = "PayTR token boş döndü" });
                }

                // notify'de idempotency için merchantOid'i kısa süreli cache'e yaz (opsiyonel)
                cache.Set($"paytr_oid_{merchantOid}", new { userId = user.Id, plan, months = req.Months, paymentAmount }, TimeSpan.FromHours(12));

                logger.LogInformation("PayTR token created: merchantOid={MerchantOid}, userId={UserId}, plan={Plan}, amount={Amount}", merchantOid, CurrentUserId, plan, paymentAmount);
                return Ok(new
                {
                    success = true,
                    data = new CreatePaytrTokenResponse(token, merchantOid, paymentAmount)
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PayTR response parse failed: body={Body}, userId={UserId}", respBody, CurrentUserId);
                return StatusCode(502, new { success = false, message = "PayTR yanıtı parse edilemedi", raw = respBody });
            }
        }

        public sealed class PaytrNotifyRequest
        {
            public string merchant_oid { get; set; } = "";
            public string status { get; set; } = "";
            public string total_amount { get; set; } = "";
            public string hash { get; set; } = "";
            public string? failed_reason_code { get; set; }
            public string? failed_reason_msg { get; set; }
            public string? payment_type { get; set; }
            public string? currency { get; set; }
            public string? payment_amount { get; set; }
            public string? test_mode { get; set; }
        }

        /// <summary>
        /// PayTR ödeme sonucu bildirimi (notify_url). PayTR bu endpoint'ten düz "OK" cevabı bekler.
        /// AllowAnonymous olmalı (PayTR server-side çağırır).
        /// </summary>
        [HttpPost("paytr/notify")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> PaytrNotify([FromForm] PaytrNotifyRequest post)
        {
            var merchantKey = configuration["PayTR:MerchantKey"];
            var merchantSalt = configuration["PayTR:MerchantSalt"];
            if (string.IsNullOrWhiteSpace(merchantKey) || string.IsNullOrWhiteSpace(merchantSalt))
            {
                logger.LogError("PayTR notify: merchant config missing");
                return BadRequest(new { success = false, message = "PAYTR notification failed: config missing" });
            }

            var expectedHash = ComputeBase64HmacSha256($"{post.merchant_oid}{merchantSalt}{post.status}{post.total_amount}", merchantKey);
            if (!FixedTimeEquals(expectedHash, post.hash))
            {
                logger.LogWarning("PayTR notify: hash mismatch for merchant_oid={MerchantOid}", post.merchant_oid);
                return BadRequest(new { success = false, message = "PAYTR notification failed: bad hash" });
            }

            // aynı merchant_oid birden fazla gelebilir -> idempotent davran
            var processedKey = $"paytr_done_{post.merchant_oid}";
            if (cache.TryGetValue(processedKey, out _))
                return Content("OK", "text/plain");

            if (post.status == "success")
            {
                // merchant_oid format: sub_{userId}_{plan}_{months}_....
                var parts = post.merchant_oid.Split('_');
                if (parts.Length >= 4 && parts[0] == "sub" && Guid.TryParse(parts[1], out var userId))
                {
                    var plan = parts[2];
                    var months = int.TryParse(parts[3], out var m) ? m : 1;
                    if (months <= 0) months = 1;

                    var user = await userDal.Get(u => u.Id == userId);
                    if (user == null)
                    {
                        logger.LogWarning("PayTR notify success: user not found, userId={UserId}, merchant_oid={MerchantOid}", userId, post.merchant_oid);
                    }
                    else
                    {
                        var now = DateTime.UtcNow;
                        var start = user.SubscriptionEndDate.HasValue && user.SubscriptionEndDate.Value > now
                            ? user.SubscriptionEndDate.Value
                            : now;

                        user.SubscriptionEndDate = start.AddDays(30 * months);
                        if (user.TrialEndDate > now) user.TrialEndDate = now; // trial varsa kapat
                        user.SubscriptionAutoRenew = true;
                        user.SubscriptionCancelAtPeriodEnd = false;

                        await userDal.Update(user);
                        logger.LogInformation("PayTR payment applied: userId={UserId}, plan={Plan}, months={Months}, newEndDate={EndDate}, merchant_oid={MerchantOid}",
                            userId, plan, months, user.SubscriptionEndDate, post.merchant_oid);
                    }
                }
                else
                {
                    logger.LogWarning("PayTR notify: merchant_oid format invalid: {MerchantOid}", post.merchant_oid);
                }
            }
            else
            {
                logger.LogWarning("PayTR notify payment failed: merchant_oid={MerchantOid}, reason_code={ReasonCode}, reason_msg={ReasonMsg}",
                    post.merchant_oid, post.failed_reason_code, post.failed_reason_msg);
            }

            cache.Set(processedKey, true, TimeSpan.FromDays(2));
            return Content("OK", "text/plain");
        }

        private string GetUserIp()
        {
            // ForwardedHeaders middleware var, X-Forwarded-For gelebilir
            var fwd = HttpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(fwd))
            {
                var first = fwd.Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrWhiteSpace(first)) return first;
            }
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        }

        private static string ComputeBase64HmacSha256(string message, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return Convert.ToBase64String(hash);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var ba = Encoding.UTF8.GetBytes(a ?? "");
            var bb = Encoding.UTF8.GetBytes(b ?? "");
            return CryptographicOperations.FixedTimeEquals(ba, bb);
        }
    }
}
