using Core.Utilities.Security.PhoneSetting;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Helpers
{
    public class SocialProfileOwnerEnricher(
        IUserDal userDal,
        IFreeBarberDal freeBarberDal,
        IBarberStoreDal barberStoreDal,
        IPhoneService phoneService)
    {
        public async Task EnrichOwnerMetaAsync(SocialProfileDto dto, SocialProfile profile)
        {
            switch (profile.OwnerType)
            {
                case SocialProfileOwnerType.Customer:
                {
                    var user = await userDal.Get(u => u.Id == profile.OwnerId);
                    if (user == null) return;
                    var first = phoneService.DecryptForRead(user.FirstNameEncrypted) ?? user.FirstName;
                    var last = phoneService.DecryptForRead(user.LastNameEncrypted) ?? user.LastName;
                    dto.OwnerDisplayName = $"{first} {last}".Trim();
                    dto.OwnerNumber = user.CustomerNumber;
                    break;
                }
                case SocialProfileOwnerType.FreeBarber:
                {
                    var fb = await freeBarberDal.Get(f => f.Id == profile.OwnerId);
                    if (fb == null) return;
                    dto.OwnerDisplayName = $"{fb.FirstName} {fb.LastName}".Trim();
                    dto.OwnerBarberType = fb.Type;
                    var fbUser = await userDal.Get(u => u.Id == fb.FreeBarberUserId);
                    dto.OwnerNumber = fbUser?.CustomerNumber;
                    break;
                }
                case SocialProfileOwnerType.BarberStore:
                {
                    var store = await barberStoreDal.Get(s => s.Id == profile.OwnerId);
                    if (store == null) return;
                    dto.OwnerDisplayName = store.StoreName;
                    dto.OwnerNumber = store.StoreNo;
                    dto.OwnerBarberType = store.Type;
                    break;
                }
            }
        }
    }
}
