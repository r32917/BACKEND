using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using project;

namespace Project.core.Servies
{
    public interface INursesServies
    {
        public Task<List<Nurses>> GetAllAsync();
        public Task<Nurses> GetByIdAsync(int id);
        public Task AddAsync(Nurses value);
        public Task _PutAsync(int id, Nurses value);
        public Task _DeleteAsync(int id);
    }
}
