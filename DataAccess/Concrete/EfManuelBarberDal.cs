using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfManuelBarberDal : EfEntityRepositoryBase<ManuelBarber, DatabaseContext>, IManuelBarberDal
    {
        private readonly DatabaseContext _context;

        public EfManuelBarberDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<ManuelBarberRatingDto>> GetManuelBarberRatingsAsync(List<Guid> barberIds)
        {
            var ratings = await(from mb in _context.ManuelBarbers
                                where barberIds.Contains(mb.Id)
                                join r in _context.Ratings on mb.Id equals r.TargetId into ratingGroup
                                from subRating in ratingGroup.DefaultIfEmpty()
                                group subRating by new { mb.Id, mb.FullName } into g
                                select new ManuelBarberRatingDto
                                {
                                    BarberId = g.Key.Id,
                                    BarberName = g.Key.FullName,
                                    Rating = g.Average(x => x != null ? x.Score : 0)
                                }).ToListAsync();

            return ratings;
        }

        public async Task<List<ManuelBarberDto>> GetBarberDtosByStoreIdAsync(Guid storeId)
        {
            var manuelBarbers = await _context.ManuelBarbers
                .AsNoTracking()
                .Where(b => b.StoreId == storeId)
                .ToListAsync();

            if (manuelBarbers.Count == 0)
                return new List<ManuelBarberDto>();

            var barberIds = manuelBarbers.Select(b => b.Id).ToList();

            var barberRatings = await _context.Ratings
                .AsNoTracking()
                .Where(r => barberIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    BarberId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score)
                })
                .ToDictionaryAsync(x => x.BarberId, x => x.AvgRating);

            var barberImages = await _context.Images
                .AsNoTracking()
                .Where(i =>
                    i.OwnerType == ImageOwnerType.ManuelBarber &&
                    barberIds.Contains(i.ImageOwnerId))
                .GroupBy(i => i.ImageOwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    ImageUrl = g.OrderByDescending(x => x.CreatedAt).First().ImageUrl
                })
                .ToDictionaryAsync(x => x.OwnerId, x => x.ImageUrl);

            return manuelBarbers
                .Select(b => new ManuelBarberDto
                {
                    Id = b.Id,
                    FullName = b.FullName,
                    Rating = barberRatings.ContainsKey(b.Id) ? barberRatings[b.Id] : 0,
                    ProfileImageUrl = barberImages.TryGetValue(b.Id, out var url) ? url : null!
                })
                .ToList();
        }

        public async Task<List<ManuelBarberAdminGetDto>> GetAllForAdminAsync()
        {
            var rows = await (
                from mb in _context.ManuelBarbers.AsNoTracking()
                join s in _context.BarberStores.AsNoTracking() on mb.StoreId equals s.Id
                orderby s.StoreName, mb.FullName
                select new { mb, s }
            ).ToListAsync();

            if (rows.Count == 0)
                return new List<ManuelBarberAdminGetDto>();

            var barberIds = rows.Select(r => r.mb.Id).ToList();

            var barberRatings = await _context.Ratings
                .AsNoTracking()
                .Where(r => barberIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    BarberId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score)
                })
                .ToDictionaryAsync(x => x.BarberId, x => x.AvgRating);

            var barberImages = await _context.Images
                .AsNoTracking()
                .Where(i =>
                    i.OwnerType == ImageOwnerType.ManuelBarber &&
                    barberIds.Contains(i.ImageOwnerId))
                .GroupBy(i => i.ImageOwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    ImageUrl = g.OrderByDescending(x => x.CreatedAt).First().ImageUrl
                })
                .ToDictionaryAsync(x => x.OwnerId, x => x.ImageUrl);

            return rows
                .Select(r => new ManuelBarberAdminGetDto
                {
                    Id = r.mb.Id,
                    StoreId = r.mb.StoreId,
                    StoreName = r.s.StoreName ?? string.Empty,
                    BarberStoreOwnerId = r.s.BarberStoreOwnerId,
                    FullName = r.mb.FullName,
                    Rating = barberRatings.TryGetValue(r.mb.Id, out var avg) ? avg : 0,
                    ProfileImageUrl = barberImages.TryGetValue(r.mb.Id, out var url) ? url : null,
                    CreatedAt = r.mb.CreatedAt,
                    UpdatedAt = r.mb.UpdatedAt
                })
                .ToList();
        }
    }
}
