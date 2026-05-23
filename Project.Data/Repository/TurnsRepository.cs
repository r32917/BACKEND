using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using project;
using Project.core.Repository;

namespace Project.Data.Repository
{
    public class TurnsRepository:ITurnsRepository
    {
        private readonly DataContext _context;
        public TurnsRepository(DataContext context)
        {
            _context = context;

        }
        public async Task<IEnumerable<Turns>> GetAllTurnsAsync()
        {
            return await _context.turns.ToListAsync();
        }
        public async Task<Turns> GetAsync(int id)
        {
             var turn =await _context.turns.FirstOrDefaultAsync(t => t.id == id);
             return turn;
        }
        public void Post(Turns value)
        {
            _context.turns.Add(value);
        }
        public void Put(int id, Turns value)
        {
            var turn = _context.turns.Find(id);
            if (turn != null)
            {
                turn.date = value.date;
                turn.time = value.time;
                turn.notes = value.notes;
                turn.babyId = value.babyId;
                turn.nurseId = value.nurseId;
            }
        }
        public void Delete(int id)
        {
            var turn = _context.turns.Find(id);
            if (turn != null)
                _context.turns.Remove(turn);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
