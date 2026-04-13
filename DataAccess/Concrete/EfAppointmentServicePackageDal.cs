using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfAppointmentServicePackageDal : EfEntityRepositoryBase<AppointmentServicePackage, DatabaseContext>, IAppointmentServicePackageDal
    {
        private readonly DatabaseContext _context;

        public EfAppointmentServicePackageDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task AddRangeAsync(List<AppointmentServicePackage> records)
        {
            await _context.AppointmentServicePackages.AddRangeAsync(records);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteByAppointmentIdAsync(Guid appointmentId)
        {
            var rows = await _context.AppointmentServicePackages
                .Where(x => x.AppointmentId == appointmentId)
                .ToListAsync();
            if (rows.Count == 0) return;
            _context.AppointmentServicePackages.RemoveRange(rows);
            await _context.SaveChangesAsync();
        }
    }
}
