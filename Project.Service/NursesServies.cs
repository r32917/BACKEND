using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using project;
using Project.core.Repository;
using Project.core.Servies;
using Project.Data.Repository;

namespace Project.Service
{
    public class NursesServies: INursesServies
    {
        private readonly INursesRepository _nursesRepository;
        public NursesServies(INursesRepository nursesRepository)
        {
            _nursesRepository = nursesRepository;
        }

        public async Task<List<Nurses>> GetAllAsync()
        {
            return await _nursesRepository.GetAllNursesAsync();
        }
        public async Task<Nurses> GetByIdAsync(int id)
        {
            return await _nursesRepository.GetAsync(id);
        }
        public async Task AddAsync(Nurses value)
        {
            _nursesRepository.Post(value);
            await _nursesRepository.SaveAsync();
        }
        public async Task _PutAsync(int id, Nurses value)
        {
            _nursesRepository.Put(id, value);
            await _nursesRepository.SaveAsync();
        }
        public async Task _DeleteAsync(int id) 
        {
            _nursesRepository.Delete(id);
            await _nursesRepository.SaveAsync();
        }




    }
}
