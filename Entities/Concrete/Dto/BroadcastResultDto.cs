using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    /// <summary>Admin toplu bildirim gönderiminin sonucu (başarılı/başarısız/toplam).</summary>
    public class BroadcastResultDto : IDto
    {
        public int Sent { get; set; }
        public int Failed { get; set; }
        public int Total { get; set; }
    }
}
