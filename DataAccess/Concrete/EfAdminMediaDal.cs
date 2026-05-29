using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DataAccess.Concrete
{
    public class EfAdminMediaDal : IAdminMediaDal
    {
        private readonly DatabaseContext _context;
        private readonly string _uploadRoot;

        public EfAdminMediaDal(DatabaseContext context, IConfiguration configuration)
        {
            _context = context;
            var root = configuration["LocalStorage:UploadRoot"];
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "hairdresser", "uploads");
            _uploadRoot = root;
        }

        public async Task<(List<AdminMediaFileDto> items, int total)> GetMediaFilesAsync(
            string? category,
            string? search,
            int page,
            int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var normalizedCategory = (category ?? "all").Trim().ToLowerInvariant();
            var searchLower = search?.Trim().ToLowerInvariant();

            var imageItems = await BuildImageMediaAsync(normalizedCategory);
            var chatItems = await BuildChatMediaAsync(normalizedCategory);

            var merged = imageItems.Concat(chatItems).ToList();

            if (!string.IsNullOrEmpty(searchLower))
            {
                merged = merged.Where(f =>
                        (f.ContextTitle?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                        (f.SenderDisplayName?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                        (f.FileName?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                        (f.CategoryLabel?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                        f.MediaUrl.ToLowerInvariant().Contains(searchLower))
                    .ToList();
            }

            var total = merged.Count;
            var items = merged
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (items, total);
        }

        public async Task<AdminMediaStatsDto> GetMediaStatsAsync()
        {
            var imageItems = await BuildImageMediaAsync("all");
            var chatItems = await BuildChatMediaAsync("all");
            var merged = imageItems.Concat(chatItems).ToList();

            var categoryLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = "Kullanıcı profili",
                ["store"] = "Berber salonu",
                ["freebarber"] = "Serbest berber",
                ["manuelbarber"] = "Manuel berber",
                ["chat-image"] = "Sohbet görselleri",
                ["chat-audio"] = "Sohbet sesleri",
                ["chat-file"] = "Sohbet dosyaları",
                ["other"] = "Diğer",
            };

            long? SumKind(string kind)
            {
                var withSize = merged.Where(x => x.MediaKind == kind && x.SizeBytes.HasValue).ToList();
                return withSize.Count > 0 ? withSize.Sum(x => x.SizeBytes!.Value) : (long?)null;
            }

            var totalSize = merged.Where(x => x.SizeBytes.HasValue).Sum(x => x.SizeBytes!.Value);

            return new AdminMediaStatsDto
            {
                TotalFiles = merged.Count,
                ImageCount = merged.Count(x => x.MediaKind == "image"),
                VideoCount = merged.Count(x => x.MediaKind == "video"),
                AudioCount = merged.Count(x => x.MediaKind == "audio"),
                FileCount = merged.Count(x => x.MediaKind == "file"),
                TotalSizeBytes = totalSize > 0 ? totalSize : (long?)null,
                ImageSizeBytes = SumKind("image"),
                VideoSizeBytes = SumKind("video"),
                AudioSizeBytes = SumKind("audio"),
                FileSizeBytes = SumKind("file"),
                Categories = merged
                    .GroupBy(x => x.Category)
                    .Select(g =>
                    {
                        var groupSize = g.Where(x => x.SizeBytes.HasValue).Sum(x => x.SizeBytes!.Value);
                        return new AdminMediaCategoryStatDto
                        {
                            CategoryId = g.Key,
                            Label = categoryLabels.GetValueOrDefault(g.Key, g.Key),
                            Count = g.Count(),
                            SizeBytes = groupSize > 0 ? groupSize : (long?)null,
                        };
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
            };
        }

        private async Task<List<AdminMediaFileDto>> BuildImageMediaAsync(string category)
        {
            if (category is "chat-image" or "chat-audio" or "chat-file")
                return new List<AdminMediaFileDto>();

            var images = await _context.Images.AsNoTracking().ToListAsync();
            if (images.Count == 0)
                return new List<AdminMediaFileDto>();

            var userIds = images.Where(i => i.OwnerType == ImageOwnerType.User).Select(i => i.ImageOwnerId).Distinct().ToList();
            var storeIds = images.Where(i => i.OwnerType == ImageOwnerType.Store).Select(i => i.ImageOwnerId).Distinct().ToList();
            var freeIds = images.Where(i => i.OwnerType == ImageOwnerType.FreeBarber).Select(i => i.ImageOwnerId).Distinct().ToList();
            var manuelIds = images.Where(i => i.OwnerType == ImageOwnerType.ManuelBarber).Select(i => i.ImageOwnerId).Distinct().ToList();

            var users = userIds.Count == 0
                ? new Dictionary<Guid, (string Name, string? Number)>()
                : await _context.Users.AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim(), u.CustomerNumber })
                    .ToDictionaryAsync(x => x.Id, x => (Name: x.Name, Number: (string?)x.CustomerNumber));

            var stores = storeIds.Count == 0
                ? new Dictionary<Guid, (string Name, string? Number)>()
                : await _context.BarberStores.AsNoTracking()
                    .Where(s => storeIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.StoreName, s.StoreNo })
                    .ToDictionaryAsync(x => x.Id, x => (Name: x.StoreName, Number: (string?)x.StoreNo));

            var freeBarbersRaw = freeIds.Count == 0
                ? new List<(Guid Id, string Name, Guid UserId)>()
                : (await _context.FreeBarbers.AsNoTracking()
                    .Where(f => freeIds.Contains(f.Id))
                    .Select(f => new { f.Id, Name = (f.FirstName + " " + f.LastName).Trim(), f.FreeBarberUserId })
                    .ToListAsync())
                    .Select(f => (Id: f.Id, Name: f.Name, UserId: f.FreeBarberUserId))
                    .ToList();

            var freeBarberUserIds = freeBarbersRaw.Select(f => f.UserId).Distinct().ToList();
            var freeBarberNumbers = freeBarberUserIds.Count == 0
                ? new Dictionary<Guid, string?>()
                : await _context.Users.AsNoTracking()
                    .Where(u => freeBarberUserIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.CustomerNumber })
                    .ToDictionaryAsync(x => x.Id, x => (string?)x.CustomerNumber);

            var freeBarbers = freeBarbersRaw.ToDictionary(
                f => f.Id,
                f => (Name: f.Name, Number: freeBarberNumbers.GetValueOrDefault(f.UserId)));

            var manuelBarbers = manuelIds.Count == 0
                ? new Dictionary<Guid, (string Name, string? Number)>()
                : await _context.ManuelBarbers.AsNoTracking()
                    .Where(m => manuelIds.Contains(m.Id))
                    .Select(m => new { m.Id, m.FullName })
                    .ToDictionaryAsync(x => x.Id, x => (Name: x.FullName, Number: (string?)null));

            var result = new List<AdminMediaFileDto>();

            foreach (var img in images)
            {
                var (cat, label, context, ownerUserId, ownerName, ownerNumber) = img.OwnerType switch
                {
                    ImageOwnerType.User => (
                        "user",
                        "Kullanıcı profili",
                        users.GetValueOrDefault(img.ImageOwnerId).Name,
                        (Guid?)img.ImageOwnerId,
                        users.GetValueOrDefault(img.ImageOwnerId).Name,
                        users.GetValueOrDefault(img.ImageOwnerId).Number),
                    ImageOwnerType.Store => (
                        "store",
                        "Berber salonu",
                        stores.GetValueOrDefault(img.ImageOwnerId).Name,
                        null,
                        stores.GetValueOrDefault(img.ImageOwnerId).Name,
                        stores.GetValueOrDefault(img.ImageOwnerId).Number),
                    ImageOwnerType.FreeBarber => (
                        "freebarber",
                        "Serbest berber paneli",
                        freeBarbers.GetValueOrDefault(img.ImageOwnerId).Name,
                        null,
                        freeBarbers.GetValueOrDefault(img.ImageOwnerId).Name,
                        freeBarbers.GetValueOrDefault(img.ImageOwnerId).Number),
                    ImageOwnerType.ManuelBarber => (
                        "manuelbarber",
                        "Manuel berber",
                        manuelBarbers.GetValueOrDefault(img.ImageOwnerId).Name,
                        null,
                        manuelBarbers.GetValueOrDefault(img.ImageOwnerId).Name,
                        (string?)null),
                    _ => ("other", "Diğer", (string?)null, (Guid?)null, (string?)null, (string?)null),
                };

                if (category != "all" && category != cat)
                    continue;

                result.Add(new AdminMediaFileDto
                {
                    Id = img.Id,
                    MediaUrl = img.ImageUrl,
                    MediaKind = InferKindFromUrl(img.ImageUrl, "image"),
                    Category = cat,
                    CategoryLabel = label,
                    ContextTitle = context,
                    SenderDisplayName = cat == "user" ? context : null,
                    SenderUserId = ownerUserId,
                    OwnerId = img.ImageOwnerId,
                    OwnerName = ownerName,
                    OwnerNumber = ownerNumber,
                    FileName = ExtractFileName(img.ImageUrl),
                    SizeBytes = ComputeSizeBytes(img.ImageUrl),
                    CreatedAt = img.CreatedAt,
                });
            }

            return result;
        }

        private async Task<List<AdminMediaFileDto>> BuildChatMediaAsync(string category)
        {
            if (category is "user" or "store" or "freebarber" or "manuelbarber")
                return new List<AdminMediaFileDto>();

            var messages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m =>
                    m.MediaUrl != null &&
                    m.MediaUrl != "" &&
                    (m.MessageType == ChatMessageType.Image ||
                     m.MessageType == ChatMessageType.File ||
                     m.MessageType == ChatMessageType.Audio))
                .Select(m => new
                {
                    m.Id,
                    m.ThreadId,
                    m.SenderUserId,
                    m.MediaUrl,
                    m.MessageType,
                    m.Text,
                    m.CreatedAt,
                })
                .ToListAsync();

            if (messages.Count == 0)
                return new List<AdminMediaFileDto>();

            var senderIds = messages.Select(m => m.SenderUserId).Distinct().ToList();
            var threadIds = messages.Select(m => m.ThreadId).Distinct().ToList();

            var senders = await _context.Users.AsNoTracking()
                .Where(u => senderIds.Contains(u.Id))
                .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim(), u.CustomerNumber })
                .ToDictionaryAsync(x => x.Id, x => (Name: x.Name, Number: (string?)x.CustomerNumber));

            var threads = await _context.ChatThreads.AsNoTracking()
                .Where(t => threadIds.Contains(t.Id))
                .Select(t => new { t.Id, t.LastMessagePreview, t.AppointmentId })
                .ToDictionaryAsync(
                    x => x.Id,
                    x => !string.IsNullOrWhiteSpace(x.LastMessagePreview)
                        ? x.LastMessagePreview
                        : x.AppointmentId.HasValue
                            ? $"Randevu sohbeti · {x.Id.ToString()[..8]}"
                            : $"Sohbet · {x.Id.ToString()[..8]}");

            var result = new List<AdminMediaFileDto>();

            foreach (var m in messages)
            {
                var cat = m.MessageType switch
                {
                    ChatMessageType.Image => "chat-image",
                    ChatMessageType.Audio => "chat-audio",
                    ChatMessageType.File => "chat-file",
                    _ => "chat-file",
                };

                if (category != "all" && category != cat)
                    continue;

                var label = m.MessageType switch
                {
                    ChatMessageType.Image => "Sohbet görseli",
                    ChatMessageType.Audio => "Sohbet sesi",
                    ChatMessageType.File => "Sohbet dosyası",
                    _ => "Sohbet medyası",
                };

                var kind = m.MessageType switch
                {
                    ChatMessageType.Image => "image",
                    ChatMessageType.Audio => "audio",
                    _ => "file",
                };

                result.Add(new AdminMediaFileDto
                {
                    Id = m.Id,
                    MediaUrl = m.MediaUrl!,
                    MediaKind = InferKindFromUrl(m.MediaUrl!, kind),
                    Category = cat,
                    CategoryLabel = label,
                    ContextTitle = threads.GetValueOrDefault(m.ThreadId),
                    SenderDisplayName = senders.GetValueOrDefault(m.SenderUserId).Name,
                    SenderUserId = m.SenderUserId,
                    ThreadId = m.ThreadId,
                    OwnerName = senders.GetValueOrDefault(m.SenderUserId).Name,
                    OwnerNumber = senders.GetValueOrDefault(m.SenderUserId).Number,
                    FileName = !string.IsNullOrWhiteSpace(m.Text) ? m.Text : ExtractFileName(m.MediaUrl!),
                    SizeBytes = ComputeSizeBytes(m.MediaUrl!),
                    CreatedAt = m.CreatedAt,
                });
            }

            return result;
        }

        private long? ComputeSizeBytes(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                var path = url;
                if (Uri.TryCreate(url, UriKind.Absolute, out var abs))
                    path = abs.AbsolutePath;

                const string marker = "/uploads/";
                var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return null;

                var relative = path[(idx + marker.Length)..].TrimStart('/');
                relative = Uri.UnescapeDataString(relative)
                    .Replace('/', Path.DirectorySeparatorChar);

                var full = Path.Combine(_uploadRoot, relative);
                var fi = new FileInfo(full);
                return fi.Exists ? fi.Length : (long?)null;
            }
            catch
            {
                return null;
            }
        }

        private static string InferKindFromUrl(string url, string fallback)
        {
            var lower = url.ToLowerInvariant();
            if (lower.EndsWith(".mp3") || lower.EndsWith(".wav") || lower.EndsWith(".m4a") || lower.EndsWith(".ogg") || lower.Contains("/audio"))
                return "audio";
            if (lower.EndsWith(".mp4") || lower.EndsWith(".webm") || lower.Contains("/video"))
                return "video";
            if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png") || lower.EndsWith(".gif") || lower.EndsWith(".webp"))
                return "image";
            return fallback;
        }

        private static string? ExtractFileName(string url)
        {
            try
            {
                var path = new Uri(url, UriKind.RelativeOrAbsolute).AbsolutePath;
                var name = Path.GetFileName(path);
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch
            {
                var idx = url.LastIndexOf('/');
                return idx >= 0 ? url[(idx + 1)..] : url;
            }
        }
    }
}
