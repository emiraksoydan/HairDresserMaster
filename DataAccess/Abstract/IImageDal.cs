using Core.DataAccess;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IImageDal : IEntityRepository<Image>
    {
        Task<Image?> GetLatestImageAsync(Guid ownerId, ImageOwnerType ownerType);
        Task<Dictionary<(Guid OwnerId, ImageOwnerType OwnerType), string?>> GetLatestImagesAsync(
            IReadOnlyCollection<(Guid OwnerId, ImageOwnerType OwnerType)> requests);
    }
}
