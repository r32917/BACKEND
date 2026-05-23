using Microsoft.EntityFrameworkCore;
using project;
using Project.core.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Data.Repository
{
    public class NursesRepository:INursesRepository
    {
        private readonly DataContext _context;
        public NursesRepository(DataContext context)
        {
            _context = context;
        }
        public async Task<List<Nurses>> GetAllNursesAsync()
        {
            return await _context.nurses.ToListAsync();

        }
        public async Task<Nurses> GetAsync(int id)
        {
            var nurse =await _context.nurses.FirstOrDefaultAsync(n => n.id == id);
            return nurse;
        }

        public void Post( Nurses value)
        {
            _context.nurses.Add(value);
        }

        public async void Put(int id, Nurses value)
        {
            var nurse = await _context.nurses.FirstOrDefaultAsync(n => n.id == id);
            if (nurse != null)
            {
                nurse.name = value.name;
                nurse.phone = value.phone;
                nurse.email = value.email;
                nurse.specialization = value.specialization;
            }
        }
        public void Delete(int id)
        {
            var nurth = _context.nurses.ToList().Find(n => n.id == id);
            _context.nurses.Remove(nurth);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
