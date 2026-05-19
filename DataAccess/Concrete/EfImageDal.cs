using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfImageDal : EfEntityRepositoryBase<Image, DatabaseContext>, IImageDal
    {
        public EfImageDal(DatabaseContext context) : base(context)
        {
        }

        public async Task<Image?> GetLatestImageAsync(Guid ownerId, ImageOwnerType ownerType)
        {
            return await Context.Images
                .AsNoTracking()
                .Where(x => x.ImageOwnerId == ownerId && x.OwnerType == ownerType)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<Dictionary<(Guid OwnerId, ImageOwnerType OwnerType), string?>> GetLatestImagesAsync(
            IReadOnlyCollection<(Guid OwnerId, ImageOwnerType OwnerType)> requests)
        {
            if (requests.Count == 0)
                return new Dictionary<(Guid, ImageOwnerType), string?>();

            var ownerIds = requests.Select(r => r.OwnerId).ToHashSet();

            var images = await Context.Images
                .AsNoTracking()
                .Where(x => ownerIds.Contains(x.ImageOwnerId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var requestSet = requests.ToHashSet();
            return images
                .Where(x => requestSet.Contains((x.ImageOwnerId, x.OwnerType)))
                .GroupBy(x => (x.ImageOwnerId, x.OwnerType))
                .ToDictionary(
                    g => g.Key,
                    g => (string?)g.First().ImageUrl
                );
        }
    }
}
