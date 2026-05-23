using project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Data.Repository
{
    public interface IBabiesRepository
    {
        public Task<List<Babies>> GetAllBabiesAsync();
        public Task<Babies> GetAsync(int id);
        public Task SaveAsync();
        public void Post(Babies value);
        public void Put(int id, Babies value);
        public void Delete(int id);
    }
}
