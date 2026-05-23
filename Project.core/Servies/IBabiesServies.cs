using project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.core.Servies
{
    public interface IBabiesServies
    {
        public Task<List<Babies>> GetAllAsync();
        public Task<Babies> GetByIdAsync(int id);
        public Task AddAsync(Babies value);

        public Task _Put(int id, Babies value);
        public Task _Delete(int id);


    }
}
