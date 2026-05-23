using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using project;

namespace Project.core.Repository
{
    public interface INursesRepository
    {
        public Task<List<Nurses>> GetAllNursesAsync();
        public Task<Nurses> GetAsync(int id);
        public Task SaveAsync();

        public void Post(Nurses value);
        public void Put(int id, Nurses value);
        public void Delete(int id);

    }
}
