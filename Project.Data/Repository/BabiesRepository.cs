using Microsoft.EntityFrameworkCore;
using project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Data.Repository
{
    public class BabiesRepository: IBabiesRepository
    {
        private readonly DataContext _context;
        public BabiesRepository(DataContext context)
        {
            _context=context;
        }
        public async Task<List<Babies>> GetAllBabiesAsync()
        {
            return await _context.babies.ToListAsync();
        }

        public async Task<Babies> GetAsync(int id)
        {
            var baby = await _context.babies.FirstOrDefaultAsync(b => b.id == id);
            return baby;
        }

        public void Post( Babies value)
        {
            _context.babies.Add(value);

        }

        public void Put(int id,  Babies value)
        {
            var baby = _context.babies.FirstOrDefault(b => b.id == id);

            if (baby == null)
            {
                return;
            }

            baby.name = value.name;
            baby.age = value.age;
            baby.family = value.family;
        }
        public void Delete(int id)
        {
            var baby = _context.babies.ToList().Find(b => b.id == id);
            _context.babies.Remove(baby);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
