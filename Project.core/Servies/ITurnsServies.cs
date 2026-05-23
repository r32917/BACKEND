using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using project;

namespace Project.core.Servies
{
    public interface ITurnsServies
    {
        public Task<IEnumerable<Turns>> GetAllTurnsAsync();
        public Task<Turns> GetByIdAsync(int id);

        public Task _PostAsync(Turns value);
        public Task _PutAsync(int id, Turns value);
        public Task _DeleteAsync(int id);
    }
}
