using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfAdminSearchDal(DatabaseContext context) : IAdminSearchDal
    {
        private readonly DatabaseContext _context = context;

        private const int GlobalKindCount = 16;

        private static readonly HashSet<string> ValidKinds = new(StringComparer.OrdinalIgnoreCase)
        {
            "User", "Store", "FreeBarber", "Service", "ManuelBarber", "Category",
            "Appointment", "Admin", "Chair", "Complaint", "Request", "ChatThread",
            "Rating", "Favorite", "Blocked", "SavedFilter",
        };

        public async Task<List<AdminSearchResultDto>> SearchAsync(string query, int limit, string? kind = null)
        {
            var term = Normalize(query);
            if (string.IsNullOrEmpty(term))
                return new List<AdminSearchResultDto>();

            if (!string.IsNullOrWhiteSpace(kind))
            {
                var k = kind.Trim();
                if (!ValidKinds.Contains(k))
                    return new List<AdminSearchResultDto>();

                limit = Math.Clamp(limit, 1, 500);
                var single = await SearchByKindAsync(k, term, limit);
                return OrderResults(single, term, limit);
            }

            limit = Math.Clamp(limit, 1, 50);
            var perType = Math.Max(2, limit / GlobalKindCount);
            var results = new List<AdminSearchResultDto>();

            foreach (var k in ValidKinds)
                results.AddRange(await SearchByKindAsync(k, term, perType));

            return OrderResults(results, term, limit);
        }

        private Task<List<AdminSearchResultDto>> SearchByKindAsync(string kind, string term, int take) =>
            kind switch
            {
                "User" => SearchUsersAsync(term, take),
                "Store" => SearchStoresAsync(term, take),
                "FreeBarber" => SearchFreeBarbersAsync(term, take),
                "Service" => SearchServicesAsync(term, take),
                "ManuelBarber" => SearchManuelBarbersAsync(term, take),
                "Category" => SearchCategoriesAsync(term, take),
                "Appointment" => SearchAppointmentsAsync(term, take),
                "Admin" => SearchAdminsAsync(term, take),
                "Chair" => SearchChairsAsync(term, take),
                "Complaint" => SearchComplaintsAsync(term, take),
                "Request" => SearchRequestsAsync(term, take),
                "ChatThread" => SearchChatThreadsAsync(term, take),
                "Rating" => SearchRatingsAsync(term, take),
                "Favorite" => SearchFavoritesAsync(term, take),
                "Blocked" => SearchBlockedAsync(term, take),
                "SavedFilter" => SearchSavedFiltersAsync(term, take),
                _ => Task.FromResult(new List<AdminSearchResultDto>()),
            };

        // "217-017", "217 bin 017" gibi yazımları rakam dizisine indirger ("217017").
        private static string DigitsOnly(string s) => new string(s.Where(char.IsDigit).ToArray());

        private async Task<List<AdminSearchResultDto>> SearchUsersAsync(string term, int take)
        {
            var digits = DigitsOnly(term);
            return await _context.Users
                .AsNoTracking()
                .Where(u =>
                    (u.FirstName + " " + u.LastName).ToLower().Contains(term) ||
                    u.PhoneNumber.Contains(term) ||
                    u.CustomerNumber.ToLower().Contains(term) ||
                    (digits != "" && (u.CustomerNumber.Contains(digits) || u.PhoneNumber.Contains(digits))))
                .OrderBy(u => u.FirstName)
                .Take(take)
                .Select(u => new AdminSearchResultDto
                {
                    Kind = "User",
                    EntityId = u.Id,
                    Title = u.FirstName + " " + u.LastName,
                    Subtitle = u.PhoneNumber,
                })
                .ToListAsync();
        }

        private async Task<List<AdminSearchResultDto>> SearchStoresAsync(string term, int take)
        {
            var digits = DigitsOnly(term);
            return await _context.BarberStores
                .AsNoTracking()
                .Where(s =>
                    s.StoreName.ToLower().Contains(term) ||
                    (s.AddressDescription != null && s.AddressDescription.ToLower().Contains(term)) ||
                    (s.StoreNo != null && s.StoreNo.ToLower().Contains(term)) ||
                    (digits != "" && s.StoreNo != null && s.StoreNo.Contains(digits)))
                .OrderBy(s => s.StoreName)
                .Take(take)
                .Select(s => new AdminSearchResultDto
                {
                    Kind = "Store",
                    EntityId = s.Id,
                    Title = s.StoreName,
                    Subtitle = s.AddressDescription,
                })
                .ToListAsync();
        }

        private async Task<List<AdminSearchResultDto>> SearchFreeBarbersAsync(string term, int take)
        {
            var digits = DigitsOnly(term);
            var results = await _context.FreeBarbers
                .AsNoTracking()
                .Where(fb =>
                    (fb.FirstName + " " + fb.LastName).ToLower().Contains(term))
                .OrderBy(fb => fb.FirstName)
                .Take(take)
                .Select(fb => new AdminSearchResultDto
                {
                    Kind = "FreeBarber",
                    EntityId = fb.Id,
                    Title = fb.FirstName + " " + fb.LastName,
                    Subtitle = fb.IsAvailable ? "Müsait" : "Meşgul",
                })
                .ToListAsync();

            if (results.Count >= take)
                return results;

            var fbByNumber = await _context.FreeBarbers
                .AsNoTracking()
                .Join(
                    _context.Users,
                    fb => fb.FreeBarberUserId,
                    u => u.Id,
                    (fb, u) => new { fb, u })
                .Where(x => x.u.CustomerNumber.ToLower().Contains(term) ||
                            (digits != "" && x.u.CustomerNumber.Contains(digits)))
                .OrderBy(x => x.fb.FirstName)
                .Take(take)
                .Select(x => new AdminSearchResultDto
                {
                    Kind = "FreeBarber",
                    EntityId = x.fb.Id,
                    Title = x.fb.FirstName + " " + x.fb.LastName,
                    Subtitle = "#" + x.u.CustomerNumber,
                })
                .ToListAsync();

            var existingIds = results.Select(r => r.EntityId).ToHashSet();
            results.AddRange(fbByNumber.Where(x => !existingIds.Contains(x.EntityId)));
            return results.Take(take).ToList();
        }

        private async Task<List<AdminSearchResultDto>> SearchServicesAsync(string term, int take) =>
            await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => o.ServiceName != null && o.ServiceName.ToLower().Contains(term))
                .OrderBy(o => o.ServiceName)
                .Take(take)
                .Select(o => new AdminSearchResultDto
                {
                    Kind = "Service",
                    EntityId = o.Id,
                    Title = o.ServiceName!,
                    Subtitle = o.Price.ToString("0.##") + " ₺",
                })
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchManuelBarbersAsync(string term, int take) =>
            await _context.ManuelBarbers
                .AsNoTracking()
                .Where(mb => mb.FullName.ToLower().Contains(term))
                .OrderBy(mb => mb.FullName)
                .Take(take)
                .Select(mb => new AdminSearchResultDto
                {
                    Kind = "ManuelBarber",
                    EntityId = mb.Id,
                    Title = mb.FullName,
                    Subtitle = "Manuel berber",
                })
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchCategoriesAsync(string term, int take) =>
            await _context.Categories
                .AsNoTracking()
                .Where(c => c.Name.ToLower().Contains(term))
                .OrderBy(c => c.Name)
                .Take(take)
                .Select(c => new AdminSearchResultDto
                {
                    Kind = "Category",
                    EntityId = c.Id,
                    Title = c.Name,
                    Subtitle = "Kategori",
                })
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchAppointmentsAsync(string term, int take) =>
            await (
                from a in _context.Appointments.AsNoTracking()
                join cu in _context.Users.AsNoTracking() on a.CustomerUserId equals cu.Id into cuj
                from cu in cuj.DefaultIfEmpty()
                join st in _context.BarberStores.AsNoTracking() on a.StoreId equals st.Id into stj
                from st in stj.DefaultIfEmpty()
                join mb in _context.ManuelBarbers.AsNoTracking() on a.ManuelBarberId equals mb.Id into mbj
                from mb in mbj.DefaultIfEmpty()
                join fu in _context.Users.AsNoTracking() on a.FreeBarberUserId equals fu.Id into fuj
                from fu in fuj.DefaultIfEmpty()
                where
                    (a.ChairName != null && a.ChairName.ToLower().Contains(term)) ||
                    (cu != null && (cu.FirstName + " " + cu.LastName).ToLower().Contains(term)) ||
                    (cu != null && cu.CustomerNumber.ToLower().Contains(term)) ||
                    (st != null && st.StoreName.ToLower().Contains(term)) ||
                    (mb != null && mb.FullName.ToLower().Contains(term)) ||
                    (fu != null && (fu.FirstName + " " + fu.LastName).ToLower().Contains(term))
                orderby a.CreatedAt descending
                select new AdminSearchResultDto
                {
                    Kind = "Appointment",
                    EntityId = a.Id,
                    Title = cu != null
                        ? cu.FirstName + " " + cu.LastName
                        : "Randevu",
                    Subtitle = st != null
                        ? st.StoreName
                        : mb != null
                            ? mb.FullName
                            : fu != null
                                ? fu.FirstName + " " + fu.LastName
                                : a.Status.ToString(),
                })
                .Take(take)
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchAdminsAsync(string term, int take) =>
            await _context.AdminUsers
                .AsNoTracking()
                .Where(a =>
                    a.Email.ToLower().Contains(term) ||
                    a.FullName.ToLower().Contains(term))
                .OrderBy(a => a.FullName)
                .Take(take)
                .Select(a => new AdminSearchResultDto
                {
                    Kind = "Admin",
                    EntityId = a.Id,
                    Title = a.FullName,
                    Subtitle = a.Email,
                })
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchChairsAsync(string term, int take) =>
            await (
                from ch in _context.BarberChairs.AsNoTracking()
                join st in _context.BarberStores.AsNoTracking() on ch.StoreId equals st.Id
                where
                    (ch.Name != null && ch.Name.ToLower().Contains(term)) ||
                    st.StoreName.ToLower().Contains(term)
                orderby st.StoreName, ch.Name
                select new AdminSearchResultDto
                {
                    Kind = "Chair",
                    EntityId = ch.Id,
                    Title = ch.Name ?? "Koltuk",
                    Subtitle = st.StoreName,
                })
                .Take(take)
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchComplaintsAsync(string term, int take) =>
            await (
                from c in _context.Complaints.AsNoTracking()
                where !c.IsDeleted
                join tu in _context.Users.AsNoTracking() on c.ComplaintToUserId equals tu.Id into tuj
                from tu in tuj.DefaultIfEmpty()
                join fu in _context.Users.AsNoTracking() on c.ComplaintFromUserId equals fu.Id into fuj
                from fu in fuj.DefaultIfEmpty()
                where
                    c.ComplaintReason.ToLower().Contains(term) ||
                    (tu != null && (tu.FirstName + " " + tu.LastName).ToLower().Contains(term)) ||
                    (fu != null && (fu.FirstName + " " + fu.LastName).ToLower().Contains(term))
                orderby c.CreatedAt descending
                select new AdminSearchResultDto
                {
                    Kind = "Complaint",
                    EntityId = c.Id,
                    Title = tu != null ? tu.FirstName + " " + tu.LastName : "Şikayet",
                    Subtitle = c.ComplaintReason.Length > 80
                        ? c.ComplaintReason.Substring(0, 80) + "…"
                        : c.ComplaintReason,
                })
                .Take(take)
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchRequestsAsync(string term, int take) =>
            await _context.Requests
                .AsNoTracking()
                .Where(r =>
                    !r.IsDeleted &&
                    (r.RequestTitle.ToLower().Contains(term) ||
                     r.RequestMessage.ToLower().Contains(term)))
                .OrderByDescending(r => r.CreatedAt)
                .Take(take)
                .Select(r => new AdminSearchResultDto
                {
                    Kind = "Request",
                    EntityId = r.Id,
                    Title = r.RequestTitle,
                    Subtitle = r.RequestMessage.Length > 80
                        ? r.RequestMessage.Substring(0, 80) + "…"
                        : r.RequestMessage,
                })
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchChatThreadsAsync(string term, int take) =>
            await (
                from t in _context.ChatThreads.AsNoTracking()
                join cu in _context.Users.AsNoTracking() on t.CustomerUserId equals cu.Id into cuj
                from cu in cuj.DefaultIfEmpty()
                join su in _context.Users.AsNoTracking() on t.StoreOwnerUserId equals su.Id into suj
                from su in suj.DefaultIfEmpty()
                join fu in _context.Users.AsNoTracking() on t.FreeBarberUserId equals fu.Id into fuj
                from fu in fuj.DefaultIfEmpty()
                join ff in _context.Users.AsNoTracking() on t.FavoriteFromUserId equals ff.Id into ffj
                from ff in ffj.DefaultIfEmpty()
                join ft in _context.Users.AsNoTracking() on t.FavoriteToUserId equals ft.Id into ftj
                from ft in ftj.DefaultIfEmpty()
                join st in _context.BarberStores.AsNoTracking() on t.StoreId equals st.Id into stj
                from st in stj.DefaultIfEmpty()
                where
                    (cu != null && (cu.FirstName + " " + cu.LastName).ToLower().Contains(term)) ||
                    (su != null && (su.FirstName + " " + su.LastName).ToLower().Contains(term)) ||
                    (fu != null && (fu.FirstName + " " + fu.LastName).ToLower().Contains(term)) ||
                    (ff != null && (ff.FirstName + " " + ff.LastName).ToLower().Contains(term)) ||
                    (ft != null && (ft.FirstName + " " + ft.LastName).ToLower().Contains(term)) ||
                    (st != null && st.StoreName.ToLower().Contains(term)) ||
                    (t.LastMessagePreview != null && t.LastMessagePreview.ToLower().Contains(term))
                orderby t.LastMessageAt descending
                select new AdminSearchResultDto
                {
                    Kind = "ChatThread",
                    EntityId = t.Id,
                    Title = t.AppointmentId.HasValue ? "Randevu sohbeti" : "Favori sohbeti",
                    Subtitle = cu != null
                        ? cu.FirstName + " " + cu.LastName
                        : ff != null && ft != null
                            ? ff.FirstName + " · " + ft.FirstName
                            : st != null
                                ? st.StoreName
                                : t.LastMessagePreview,
                })
                .Take(take)
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchRatingsAsync(string term, int take) =>
            await (
                from r in _context.Ratings.AsNoTracking()
                join u in _context.Users.AsNoTracking() on r.RatedFromId equals u.Id into uj
                from u in uj.DefaultIfEmpty()
                where
                    (r.Comment != null && r.Comment.ToLower().Contains(term)) ||
                    (u != null && (u.FirstName + " " + u.LastName).ToLower().Contains(term))
                orderby r.CreatedAt descending
                select new AdminSearchResultDto
                {
                    Kind = "Rating",
                    EntityId = r.Id,
                    Title = u != null ? u.FirstName + " " + u.LastName : "Değerlendirme",
                    Subtitle = r.Score.ToString("0.#") + " ★" +
                               (r.Comment != null && r.Comment.Length > 0
                                   ? " · " + (r.Comment.Length > 60 ? r.Comment.Substring(0, 60) + "…" : r.Comment)
                                   : ""),
                })
                .Take(take)
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchFavoritesAsync(string term, int take)
        {
            var merged = new Dictionary<Guid, AdminSearchResultDto>();

            async Task AddFromQuery(IQueryable<AdminSearchResultDto> query)
            {
                var batch = await query.Take(take).ToListAsync();
                foreach (var row in batch)
                {
                    if (!merged.ContainsKey(row.EntityId))
                        merged[row.EntityId] = row;
                    if (merged.Count >= take)
                        return;
                }
            }

            var active = _context.Favorites.AsNoTracking().Where(f => f.IsActive);

            await AddFromQuery(
                from f in active
                join s in _context.BarberStores.AsNoTracking() on f.FavoritedToId equals s.Id
                where s.StoreName.ToLower().Contains(term)
                orderby f.CreatedAt descending
                select new AdminSearchResultDto
                {
                    Kind = "Favorite",
                    EntityId = f.Id,
                    Title = s.StoreName,
                    Subtitle = "Salon favorisi",
                });

            if (merged.Count < take)
            {
                await AddFromQuery(
                    from f in active
                    join u in _context.Users.AsNoTracking() on f.FavoritedToId equals u.Id
                    where
                        (u.FirstName + " " + u.LastName).ToLower().Contains(term) ||
                        u.CustomerNumber.ToLower().Contains(term)
                    orderby f.CreatedAt descending
                    select new AdminSearchResultDto
                    {
                        Kind = "Favorite",
                        EntityId = f.Id,
                        Title = u.FirstName + " " + u.LastName,
                        Subtitle = "Kullanıcı favorisi",
                    });
            }

            if (merged.Count < take)
            {
                await AddFromQuery(
                    from f in active
                    join mb in _context.ManuelBarbers.AsNoTracking() on f.FavoritedToId equals mb.Id
                    where mb.FullName.ToLower().Contains(term)
                    orderby f.CreatedAt descending
                    select new AdminSearchResultDto
                    {
                        Kind = "Favorite",
                        EntityId = f.Id,
                        Title = mb.FullName,
                        Subtitle = "Manuel berber favorisi",
                    });
            }

            if (merged.Count < take)
            {
                await AddFromQuery(
                    from f in active
                    join fb in _context.FreeBarbers.AsNoTracking() on f.FavoritedToId equals fb.Id
                    where (fb.FirstName + " " + fb.LastName).ToLower().Contains(term)
                    orderby f.CreatedAt descending
                    select new AdminSearchResultDto
                    {
                        Kind = "Favorite",
                        EntityId = f.Id,
                        Title = fb.FirstName + " " + fb.LastName,
                        Subtitle = "Serbest berber favorisi",
                    });
            }

            return merged.Values.Take(take).ToList();
        }

        private async Task<List<AdminSearchResultDto>> SearchBlockedAsync(string term, int take) =>
            await (
                from b in _context.Blockeds.AsNoTracking()
                where !b.IsDeleted
                join u in _context.Users.AsNoTracking() on b.BlockedToUserId equals u.Id into uj
                from u in uj.DefaultIfEmpty()
                where
                    b.BlockReason.ToLower().Contains(term) ||
                    (u != null && (u.FirstName + " " + u.LastName).ToLower().Contains(term))
                orderby b.CreatedAt descending
                select new AdminSearchResultDto
                {
                    Kind = "Blocked",
                    EntityId = b.Id,
                    Title = u != null ? u.FirstName + " " + u.LastName : "Engellenen",
                    Subtitle = b.BlockReason.Length > 80
                        ? b.BlockReason.Substring(0, 80) + "…"
                        : b.BlockReason,
                })
                .Take(take)
                .ToListAsync();

        private async Task<List<AdminSearchResultDto>> SearchSavedFiltersAsync(string term, int take) =>
            await _context.SavedFilters
                .AsNoTracking()
                .Where(f => f.Name.ToLower().Contains(term))
                .OrderByDescending(f => f.CreatedAt)
                .Take(take)
                .Select(f => new AdminSearchResultDto
                {
                    Kind = "SavedFilter",
                    EntityId = f.Id,
                    Title = f.Name,
                    Subtitle = "Kayıtlı filtre",
                })
                .ToListAsync();

        private static List<AdminSearchResultDto> OrderResults(
            List<AdminSearchResultDto> results,
            string term,
            int limit) =>
            results
                .OrderBy(r => r.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(r => r.Title)
                .Take(limit)
                .ToList();

        private static string Normalize(string input) =>
            input
                .Trim()
                .ToLowerInvariant()
                .Replace('ı', 'i')
                .Replace('ş', 's')
                .Replace('ğ', 'g')
                .Replace('ü', 'u')
                .Replace('ö', 'o')
                .Replace('ç', 'c');
    }
}
