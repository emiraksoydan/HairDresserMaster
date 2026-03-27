using Castle.DynamicProxy;
using Core.Extensions;
using Core.Utilities.Interceptors;
using Core.Utilities.IoC;
using Entities.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Aspect.Autofac.Logging
{
    public class LogAspect : MethodInterception
    {
        private readonly bool _logParameters;
        private readonly bool _logReturnValue;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            MaxDepth = 8,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        public LogAspect(bool logParameters = true, bool logReturnValue = false)
        {
            _logParameters = logParameters;
            _logReturnValue = logReturnValue;
        }

        protected override void OnBefore(IInvocation invocation)
        {
            var logger = GetLogger(invocation);
            if (logger == null) return;

            var parameters = _logParameters ? GetParameters(invocation) : null;
            var userInfo = GetUserInfo();

            if (!string.IsNullOrEmpty(userInfo))
                logger.LogInformation("Method started: {ClassName}.{MethodName} | User: {UserInfo} | Parameters: {Parameters}",
                    invocation.TargetType.Name, invocation.Method.Name, userInfo, parameters ?? "-");
            else
                logger.LogInformation("Method started: {ClassName}.{MethodName} | Parameters: {Parameters}",
                    invocation.TargetType.Name, invocation.Method.Name, parameters ?? "-");
        }

        protected override void OnAfter(IInvocation invocation)
        {
            // OnSuccess zaten başarılı sonucu logluyor, OnAfter gereksiz tekrar oluşturur
        }

        protected override void OnSuccess(IInvocation invocation)
        {
            var logger = GetLogger(invocation);
            if (logger == null) return;

            var returnValue = _logReturnValue ? GetReturnValue(invocation) : null;
            var userInfo = GetUserInfo();

            if (!string.IsNullOrEmpty(userInfo))
                logger.LogInformation("Method succeeded: {ClassName}.{MethodName} | User: {UserInfo}{ReturnPart}",
                    invocation.TargetType.Name, invocation.Method.Name, userInfo,
                    returnValue != null ? $" | Return: {returnValue}" : "");
            else
                logger.LogInformation("Method succeeded: {ClassName}.{MethodName}{ReturnPart}",
                    invocation.TargetType.Name, invocation.Method.Name,
                    returnValue != null ? $" | Return: {returnValue}" : "");
        }

        protected override void OnException(IInvocation invocation, Exception exception)
        {
            var logger = GetLogger(invocation);
            if (logger == null) return;

            var parameters = _logParameters ? GetParameters(invocation) : null;
            var userInfo = GetUserInfo();

            logger.LogError(exception, "Method failed: {ClassName}.{MethodName} | User: {UserInfo} | Parameters: {Parameters}",
                invocation.TargetType.Name, invocation.Method.Name,
                string.IsNullOrEmpty(userInfo) ? "background" : userInfo,
                parameters ?? "-");
        }

        private static ILogger? GetLogger(IInvocation invocation)
        {
            try
            {
                var loggerFactory = ServiceTool.ServiceProvider?.GetService<ILoggerFactory>();
                return loggerFactory?.CreateLogger(invocation.TargetType.Name);
            }
            catch
            {
                return null;
            }
        }

        private string? GetParameters(IInvocation invocation)
        {
            try
            {
                var parameters = new Dictionary<string, object?>();
                var methodParameters = invocation.Method.GetParameters();

                for (int i = 0; i < methodParameters.Length; i++)
                {
                    var paramName = methodParameters[i].Name ?? $"param{i}";
                    if (IsSensitiveParameter(paramName))
                    {
                        parameters[paramName] = "***REDACTED***";
                    }
                    else
                    {
                        parameters[paramName] = SanitizeObject(invocation.Arguments[i]);
                    }
                }

                return JsonSerializer.Serialize(parameters, _jsonOptions);
            }
            catch (Exception ex)
            {
                return $"SerializeError: {ex.Message}";
            }
        }

        // [LogIgnore] attribute'u olan property'leri nesneden çıkarır
        private static object? SanitizeObject(object? value)
        {
            if (value == null) return null;

            var type = value.GetType();

            // Primitive, string, Guid, enum, DateTime gibi basit tipler — doğrudan dön
            if (type.IsPrimitive || type.IsEnum || value is string || value is Guid || value is DateTime || value is DateTimeOffset)
                return value;

            // [LogIgnore] property'si olan class mı kontrol et
            var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var hasIgnored = props.Any(p => p.GetCustomAttributes(typeof(LogIgnoreAttribute), false).Length > 0);
            if (!hasIgnored) return value; // Yok — olduğu gibi bırak

            // Sadece [LogIgnore] olmayan property'leri al
            var sanitized = new Dictionary<string, object?>();
            foreach (var prop in props)
            {
                if (prop.GetCustomAttributes(typeof(LogIgnoreAttribute), false).Length > 0)
                    continue;
                try { sanitized[prop.Name] = prop.GetValue(value); }
                catch { sanitized[prop.Name] = "?"; }
            }
            return sanitized;
        }

        private static string? GetReturnValue(IInvocation invocation)
        {
            try
            {
                var returnValue = invocation.ReturnValue;
                if (returnValue == null) return "null";
                if (returnValue is System.Threading.Tasks.Task) return null; // async — loglama
                return JsonSerializer.Serialize(returnValue, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    MaxDepth = 3,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                });
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSensitiveParameter(string parameterName)
        {
            var lower = parameterName.ToLowerInvariant();
            return new[] { "password", "pwd", "token", "secret", "key", "credential", "auth" }
                .Any(k => lower.Contains(k));
        }

        private static string GetUserInfo()
        {
            try
            {
                var httpContextAccessor = ServiceTool.ServiceProvider?.GetService<IHttpContextAccessor>();
                var user = httpContextAccessor?.HttpContext?.User;
                if (user == null) return string.Empty;

                var parts = new List<string>();

                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId)) parts.Add($"Id:{userId}");

                var name = user.FindFirst("name")?.Value;
                if (!string.IsNullOrEmpty(name)) parts.Add($"Name:{name}");

                var roles = user.ClaimRoles();
                if (roles?.Any() == true) parts.Add($"Roles:{string.Join(",", roles)}");

                return string.Join(" ", parts);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
