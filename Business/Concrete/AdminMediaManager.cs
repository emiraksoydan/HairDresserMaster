using Business.Abstract;
using Core.Utilities.Results;
using Core.Utilities.Storage;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;

namespace Business.Concrete
{
    public class AdminMediaManager(
        IAdminMediaDal adminMediaDal,
        IImageDal imageDal,
        IChatMessageDal chatMessageDal,
        IBarberStoreDal barberStoreDal,
        IFreeBarberDal freeBarberDal,
        IUserDal userDal,
        IBlobStorageService blobStorageService,
        IMessageEncryptionService messageEncryption,
        IAuditService auditService) : IAdminMediaService
    {
        private static readonly HashSet<string> ChatCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "chat-image", "chat-audio", "chat-file",
        };

        public async Task<IDataResult<PagedResultDto<AdminMediaFileDto>>> GetMediaFilesAsync(
            string? category,
            string? search,
            int page,
            int pageSize)
        {
            var (items, total) = await adminMediaDal.GetMediaFilesAsync(category, search, page, pageSize);

            // Sohbet medyası URL'leri DB'de şifreli saklanır; listede gösterim için çöz.
            foreach (var it in items)
            {
                if (ChatCategories.Contains(it.Category) && !string.IsNullOrEmpty(it.MediaUrl))
                {
                    var dec = messageEncryption.Decrypt(it.MediaUrl);
                    if (!string.IsNullOrEmpty(dec)) it.MediaUrl = dec;
                }
            }

            return new SuccessDataResult<PagedResultDto<AdminMediaFileDto>>(new PagedResultDto<AdminMediaFileDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize,
            });
        }

        public async Task<IDataResult<AdminMediaStatsDto>> GetMediaStatsAsync()
        {
            var stats = await adminMediaDal.GetMediaStatsAsync();
            return new SuccessDataResult<AdminMediaStatsDto>(stats);
        }

        /// <summary>
        /// Admin moderasyon: bir medyayı kaldırır.
        /// - Sohbet medyası: blob silinir + mesajın MediaUrl'i temizlenir (kayıt durur).
        /// - Profil/galeri görselleri: blob + Image satırı silinir.
        /// - Sertifika / vergi levhası gibi belgeler KORUNUR (silinmez).
        /// </summary>
        public async Task<IResult> DeleteMediaAsync(Guid adminId, Guid id, string? category)
        {
            var cat = (category ?? string.Empty).Trim().ToLowerInvariant();

            if (ChatCategories.Contains(cat))
            {
                var msg = await chatMessageDal.Get(m => m.Id == id);
                if (msg == null)
                    return new ErrorResult("Medya bulunamadı.");

                var realUrl = messageEncryption.Decrypt(msg.MediaUrl);
                if (!string.IsNullOrEmpty(realUrl))
                {
                    try { await blobStorageService.DeleteAsync(realUrl); } catch { /* dosya zaten yoksa yut */ }
                }

                msg.MediaUrl = null;
                msg.DeletedByUserId = adminId;
                msg.DeletedAt = DateTime.UtcNow;
                await chatMessageDal.Update(msg);

                await auditService.RecordAsync(AuditAction.AdminMediaDeleted, adminId, id, null, true);
                return new SuccessResult("Sohbet medyası silindi.");
            }

            // Image kaynaklı (profil / galeri / manuel berber)
            var img = await imageDal.Get(i => i.Id == id);
            if (img == null)
                return new ErrorResult("Medya bulunamadı.");

            // KORUMA: vergi levhası / işyeri belgesi
            var isTaxDoc = await barberStoreDal.Get(s => s.TaxDocumentImageId == id) != null;
            if (isTaxDoc)
                return new ErrorResult("Bu görsel bir vergi/işyeri belgesi olduğundan silinemez.");

            // KORUMA: berber / güzellik uzmanı sertifikası
            var isCertificate = await freeBarberDal.Get(f =>
                f.BarberCertificateImageId == id || f.BeautySalonCertificateImageId == id) != null;
            if (isCertificate)
                return new ErrorResult("Bu görsel bir sertifika belgesi olduğundan silinemez.");

            if (!string.IsNullOrEmpty(img.ImageUrl))
            {
                try { await blobStorageService.DeleteAsync(img.ImageUrl); } catch { /* yut */ }
            }

            // Profil fotoğrafıysa kullanıcının referansını temizle (kırık link kalmasın)
            if (img.OwnerType == ImageOwnerType.User)
            {
                var owner = await userDal.Get(u => u.Id == img.ImageOwnerId && u.ImageId == id);
                if (owner != null)
                {
                    owner.ImageId = null;
                    await userDal.Update(owner);
                }
            }

            await imageDal.Remove(img);
            await auditService.RecordAsync(AuditAction.AdminMediaDeleted, adminId, id, img.ImageOwnerId, true);
            return new SuccessResult("Görsel silindi.");
        }
    }
}
