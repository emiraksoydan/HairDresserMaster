using DataAccess.Concrete;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Helpers
{
    /// <summary>Mağaza ve serbest berber filtrelerinde ortak hizmet Id → kategori adı çözümlemesi.</summary>
    internal static class ServiceFilterCategoryHelper
    {
        public static async Task<List<string>> GetCategoryNamesByServiceIdsAsync(
            DatabaseContext ctx,
            IReadOnlyList<Guid> serviceIds,
            CancellationToken cancellationToken = default)
        {
            if (serviceIds == null || serviceIds.Count == 0)
                return new List<string>();

            return await ctx.Categories
                .AsNoTracking()
                .Where(c => serviceIds.Contains(c.Id))
                .Select(c => c.Name)
                .ToListAsync(cancellationToken);
        }
    }
}
