using Core.Utilities.Results;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Business.Helpers
{
    /// <summary>
    /// B4: Yüklenen dosyalar için (resim / chat medyası / belge / ses) merkezi
    /// güvenlik validasyonu. Boyut, MIME allow-list, magic-byte tutarlılığı,
    /// uzantı doğrulaması ve dosya adı sanitize işlemlerini yapar.
    ///
    /// Kullanım: <see cref="Business.Concrete.ImageManager"/> içinde her upload
    /// öncesi <c>Validate</c> çağrılır; hata durumunda blob/DB yazılmaz.
    /// </summary>
    public static class UploadFileValidator
    {
        /// <summary>
        /// Mutlak maksimum boyutlar — kategori bazında. Aşan dosya reddedilir.
        /// </summary>
        public const long MaxImageBytes = 10L * 1024 * 1024;     // 10 MB
        public const long MaxAudioBytes = 20L * 1024 * 1024;     // 20 MB
        public const long MaxDocumentBytes = 25L * 1024 * 1024;  // 25 MB
        public const long MaxVideoBytes = 50L * 1024 * 1024;     // 50 MB

        public const int MaxFileNameLength = 200;

        private static readonly HashSet<string> ImageMimeAllowList = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/jpg", "image/pjpeg",
            "image/png",
            "image/webp",
            "image/gif",
            "image/heic", "image/heif",
        };

        private static readonly HashSet<string> ImageExtensionAllowList = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".heif",
        };

        private static readonly HashSet<string> AudioMimeAllowList = new(StringComparer.OrdinalIgnoreCase)
        {
            "audio/mp4", "audio/x-m4a", "audio/aac",
            "audio/mpeg", "audio/mp3",
            "audio/ogg", "audio/opus",
            "audio/wav", "audio/x-wav", "audio/wave",
            "audio/webm",
            "audio/3gpp", "audio/amr",
        };

        private static readonly HashSet<string> AudioExtensionAllowList = new(StringComparer.OrdinalIgnoreCase)
        {
            ".m4a", ".mp4", ".aac", ".mp3", ".ogg", ".opus",
            ".wav", ".webm", ".3gp", ".3gpp", ".amr",
        };

        private static readonly HashSet<string> DocumentMimeAllowList = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "text/plain", "text/csv",
            "application/zip", "application/x-zip-compressed",
            "application/rtf", "text/rtf",
        };

        private static readonly HashSet<string> DocumentExtensionAllowList = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".doc", ".docx",
            ".xls", ".xlsx",
            ".ppt", ".pptx",
            ".txt", ".csv", ".rtf",
            ".zip",
        };

        private static readonly HashSet<string> VideoMimeAllowList = new(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4", "video/quicktime", "video/webm",
        };

        private static readonly HashSet<string> VideoExtensionAllowList = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".webm",
        };

        /// <summary>
        /// Yasak / tehlikeli uzantılar — hiçbir kategoride kabul edilmez.
        /// </summary>
        private static readonly HashSet<string> ForbiddenExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".com", ".msi", ".sh", ".bash", ".zsh",
            ".ps1", ".psm1", ".vbs", ".js", ".mjs", ".jar", ".scr", ".pif",
            ".cpl", ".dll", ".sys", ".reg", ".lnk", ".html", ".htm", ".svg",
            ".php", ".asp", ".aspx", ".jsp",
        };

        /// <summary>
        /// Profil/dükkan/serbest berber/manuel berber resimleri için sıkı validasyon.
        /// Yalnızca image MIME + image extension kabul edilir.
        /// </summary>
        public static IResult ValidateProfileOrOwnerImage(IFormFile file)
            => ValidateInternal(file, allowImages: true, allowAudio: false,
                allowDocuments: false, allowVideo: false, MaxImageBytes);

        /// <summary>
        /// Chat medyası için geniş validasyon — kullanıcının User OwnerType'ıyla
        /// gönderdiği isProfileImage=false uploadlar için.
        /// Image + Audio + Document + Video kabul edilir, kategoriye göre boyut sınırı.
        /// </summary>
        public static IResult ValidateChatMedia(IFormFile file)
            => ValidateInternal(file, allowImages: true, allowAudio: true,
                allowDocuments: true, allowVideo: true, maxBytesOverride: null);

        /// <summary>
        /// Tüm uploadlar için ortak validasyon. Kategori bayrakları hangi MIME setlerinin
        /// kabul edildiğini belirler. <paramref name="maxBytesOverride"/> verilmezse
        /// kategoriye göre maksimum (image/audio/document/video) seçilir.
        /// </summary>
        private static IResult ValidateInternal(
            IFormFile file,
            bool allowImages,
            bool allowAudio,
            bool allowDocuments,
            bool allowVideo,
            long? maxBytesOverride)
        {
            if (file == null || file.Length <= 0)
                return new ErrorResult("Dosya boş veya gönderilmedi.");

            // 1) Dosya adı + uzantı
            var rawName = file.FileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawName))
                return new ErrorResult("Dosya adı boş olamaz.");

            var sanitizedName = SanitizeFileName(rawName);
            if (string.IsNullOrWhiteSpace(sanitizedName))
                return new ErrorResult("Dosya adı geçersiz karakterler içeriyor.");

            var ext = Path.GetExtension(sanitizedName);
            if (string.IsNullOrEmpty(ext))
                return new ErrorResult("Dosya uzantısı eksik.");

            ext = ext.ToLowerInvariant();
            if (ForbiddenExtensions.Contains(ext))
                return new ErrorResult($"'{ext}' uzantılı dosyalar güvenlik sebebiyle yüklenemez.");

            // 2) Kategori belirle (extension öncelikli — MIME spoof edilebilir)
            var category = ResolveCategoryByExtension(ext);
            if (category == FileCategory.Unknown)
                return new ErrorResult($"'{ext}' uzantısı desteklenmiyor.");

            // 3) Kategori izinli mi?
            var categoryAllowed = category switch
            {
                FileCategory.Image => allowImages,
                FileCategory.Audio => allowAudio,
                FileCategory.Document => allowDocuments,
                FileCategory.Video => allowVideo,
                _ => false,
            };
            if (!categoryAllowed)
                return new ErrorResult("Bu yükleme tipinde desteklenmeyen bir dosya formatı.");

            // 4) Boyut kontrolü
            var maxBytes = maxBytesOverride ?? category switch
            {
                FileCategory.Image => MaxImageBytes,
                FileCategory.Audio => MaxAudioBytes,
                FileCategory.Document => MaxDocumentBytes,
                FileCategory.Video => MaxVideoBytes,
                _ => MaxImageBytes,
            };
            if (file.Length > maxBytes)
                return new ErrorResult(
                    $"Dosya boyutu çok büyük ({FormatBytes(file.Length)}). " +
                    $"Bu kategori için en fazla {FormatBytes(maxBytes)} yüklenebilir.");

            // 5) MIME allow-list (declared content type — frontend'in beyanı)
            var declaredMime = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();
            if (!IsMimeAllowedForCategory(declaredMime, category))
            {
                // Bazı tarayıcılar/cihazlar generic application/octet-stream gönderir;
                // extension uyuyor + magic byte uyuyorsa kabul edilebilir.
                if (declaredMime != "application/octet-stream")
                    return new ErrorResult(
                        $"Beyan edilen dosya tipi ('{declaredMime}') bu kategoride kabul edilmiyor.");
            }

            // 6) Magic-byte sniff — declared MIME / extension ile gerçek bayt yapısı tutarlı mı?
            var sniffResult = SniffAndValidate(file, ext, category);
            if (!sniffResult.Success)
                return sniffResult;

            return new SuccessResult(sanitizedName);
        }

        /// <summary>
        /// Validasyon başarılı ise <see cref="SuccessResult.Message"/> alanında
        /// sanitize edilmiş dosya adı döner. Validator çağıranı bu adı kullanmalıdır.
        /// </summary>
        public static string GetSanitizedFileName(IResult validationResult, string fallback = "file")
        {
            if (validationResult.Success && !string.IsNullOrWhiteSpace(validationResult.Message))
                return validationResult.Message!;
            return SanitizeFileName(fallback) ?? "file";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private enum FileCategory { Unknown, Image, Audio, Document, Video }

        private static FileCategory ResolveCategoryByExtension(string ext)
        {
            if (ImageExtensionAllowList.Contains(ext)) return FileCategory.Image;
            if (AudioExtensionAllowList.Contains(ext)) return FileCategory.Audio;
            if (DocumentExtensionAllowList.Contains(ext)) return FileCategory.Document;
            if (VideoExtensionAllowList.Contains(ext)) return FileCategory.Video;
            return FileCategory.Unknown;
        }

        private static bool IsMimeAllowedForCategory(string mime, FileCategory category)
        {
            if (string.IsNullOrEmpty(mime)) return false;
            return category switch
            {
                FileCategory.Image => ImageMimeAllowList.Contains(mime),
                FileCategory.Audio => AudioMimeAllowList.Contains(mime),
                FileCategory.Document => DocumentMimeAllowList.Contains(mime),
                FileCategory.Video => VideoMimeAllowList.Contains(mime),
                _ => false,
            };
        }

        /// <summary>
        /// Dosya adını filesystem ve URL güvenliği için temizler.
        /// - Dizin ayraçlarını ve null bayt'ı kaldırır
        /// - Kontrol karakterlerini siler
        /// - Windows reserved isimleri (CON, PRN, NUL...) reddeder
        /// - 200 karakter ile sınırlar
        /// </summary>
        public static string SanitizeFileName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Sadece dosya adı parçasını al — istemci ".../etc/passwd" gönderirse
            // path traversal yapamasın.
            var nameOnly = Path.GetFileName(input.Trim());
            if (string.IsNullOrEmpty(nameOnly)) return string.Empty;

            var sb = new StringBuilder(nameOnly.Length);
            foreach (var ch in nameOnly)
            {
                if (ch == '\0') continue;
                if (char.IsControl(ch)) continue;
                if (ch == '/' || ch == '\\') continue;
                if (Path.GetInvalidFileNameChars().Contains(ch)) continue;
                sb.Append(ch);
            }
            var cleaned = sb.ToString().Trim().Trim('.');

            if (string.IsNullOrEmpty(cleaned)) return string.Empty;

            // Windows reserved
            var nameWithoutExt = Path.GetFileNameWithoutExtension(cleaned);
            var reserved = new[] { "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (reserved.Contains(nameWithoutExt, StringComparer.OrdinalIgnoreCase))
                return string.Empty;

            if (cleaned.Length > MaxFileNameLength)
            {
                var ext = Path.GetExtension(cleaned);
                var stem = Path.GetFileNameWithoutExtension(cleaned);
                var allowed = MaxFileNameLength - ext.Length;
                if (allowed < 1) allowed = 1;
                cleaned = stem.Substring(0, Math.Min(stem.Length, allowed)) + ext;
            }

            return cleaned;
        }

        /// <summary>
        /// Magic-byte (file signature) tabanlı tutarlılık denetimi.
        /// İçerik tipi extension ile uyuşmuyorsa hata döner.
        /// Tüm formatlar için signature mevcut değil — bilinmeyen formatlar
        /// (ör. .txt, .csv, bazı audio formatları) sniff'i atlar (extension + MIME yeterli).
        /// </summary>
        private static IResult SniffAndValidate(IFormFile file, string ext, FileCategory category)
        {
            try
            {
                using var stream = file.OpenReadStream();
                Span<byte> head = stackalloc byte[16];
                int read = stream.Read(head);
                if (read < 4)
                    return new ErrorResult("Dosya çok kısa veya bozuk görünüyor.");

                bool ok = ext switch
                {
                    ".jpg" or ".jpeg" => head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
                    ".png" => head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47,
                    ".gif" => head[0] == 0x47 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x38,
                    ".webp" => read >= 12 && IsRiff(head) && IsAsciiAt(head, 8, "WEBP"),
                    ".heic" or ".heif" => read >= 12 && IsAsciiAt(head, 4, "ftyp"),
                    ".pdf" => head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46,
                    ".docx" or ".xlsx" or ".pptx" or ".zip" =>
                        head[0] == 0x50 && head[1] == 0x4B
                        && (head[2] == 0x03 || head[2] == 0x05 || head[2] == 0x07)
                        && (head[3] == 0x04 || head[3] == 0x06 || head[3] == 0x08),
                    ".doc" or ".xls" or ".ppt" =>
                        head[0] == 0xD0 && head[1] == 0xCF && head[2] == 0x11 && head[3] == 0xE0,
                    ".rtf" =>
                        head[0] == 0x7B && head[1] == 0x5C && head[2] == 0x72 && head[3] == 0x74, // {\rt
                    ".mp4" or ".m4a" or ".mov" or ".3gp" or ".3gpp" =>
                        read >= 12 && IsAsciiAt(head, 4, "ftyp"),
                    ".mp3" => (head[0] == 0x49 && head[1] == 0x44 && head[2] == 0x33)
                              || (head[0] == 0xFF && (head[1] & 0xE0) == 0xE0),
                    ".aac" => (head[0] == 0xFF && (head[1] & 0xF0) == 0xF0)
                              || (head[0] == 0x49 && head[1] == 0x44 && head[2] == 0x33),
                    ".ogg" or ".opus" =>
                        head[0] == 0x4F && head[1] == 0x67 && head[2] == 0x67 && head[3] == 0x53,
                    ".wav" => read >= 12 && IsRiff(head) && IsAsciiAt(head, 8, "WAVE"),
                    ".webm" =>
                        head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3,
                    ".amr" =>
                        read >= 6 && head[0] == 0x23 && head[1] == 0x21 && head[2] == 0x41
                        && head[3] == 0x4D && head[4] == 0x52,
                    // Plain text / csv için signature yok; içerik testini atla.
                    ".txt" or ".csv" => true,
                    _ => true,
                };

                if (!ok)
                    return new ErrorResult(
                        "Dosya içeriği uzantı ile uyuşmuyor. " +
                        "Lütfen geçerli bir dosya yükleyin.");

                return new SuccessResult();
            }
            catch (Exception)
            {
                return new ErrorResult("Dosya okunamadı.");
            }
        }

        private static bool IsRiff(ReadOnlySpan<byte> head)
            => head.Length >= 4 && head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46;

        private static bool IsAsciiAt(ReadOnlySpan<byte> buf, int offset, string ascii)
        {
            if (buf.Length < offset + ascii.Length) return false;
            for (int i = 0; i < ascii.Length; i++)
                if (buf[offset + i] != (byte)ascii[i]) return false;
            return true;
        }

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            if (bytes >= GB) return $"{bytes / (double)GB:0.##} GB";
            if (bytes >= MB) return $"{bytes / (double)MB:0.##} MB";
            if (bytes >= KB) return $"{bytes / (double)KB:0.##} KB";
            return $"{bytes} B";
        }
    }
}
