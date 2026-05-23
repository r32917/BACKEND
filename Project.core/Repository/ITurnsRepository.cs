using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using project;

namespace Project.core.Repository
{
    public interface ITurnsRepository
    {
        public Task<IEnumerable<Turns>> GetAllTurnsAsync();
        public Task<Turns> GetAsync(int id);
        public Task SaveAsync();

        public void Post(Turns value);
        public void Put(int id, Turns value);
        public void Delete(int id);
    }
}
