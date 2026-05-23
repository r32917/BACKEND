using project;
using Project.core.Servies;
using Project.Data.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Service
{
    public class BabiesServies:IBabiesServies
    {
        private readonly IBabiesRepository _babiesRepository;
        public BabiesServies(IBabiesRepository babiesRepository)
        {
            _babiesRepository= babiesRepository;
        }
        public Task<List<Babies>> GetAllAsync()
        {
            return _babiesRepository.GetAllBabiesAsync();
        }
        public Task<Babies> GetByIdAsync(int id)
        {
            return _babiesRepository.GetAsync(id);
        }
        public async Task AddAsync(Babies value)
        {
            _babiesRepository.Post(value);
            await _babiesRepository.SaveAsync();

        }

        public async Task _Put(int id, Babies value)
        {
            _babiesRepository.Put(id, value);
            await _babiesRepository.SaveAsync();
        }
        public async Task _Delete(int id)
        {
            _babiesRepository.Delete(id);
            await _babiesRepository.SaveAsync();
        }
    }
}
