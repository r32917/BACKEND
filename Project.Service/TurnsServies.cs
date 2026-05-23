using project;
using Project.core.Repository;
using Project.core.Servies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Service
{
    public class TurnsServies: ITurnsServies
    {
        private readonly ITurnsRepository _turnsRepository;
        public TurnsServies(ITurnsRepository turnsRepository)
        {
            _turnsRepository = turnsRepository;
        }

        public Task<IEnumerable<Turns>> GetAllTurnsAsync()
        {
            return _turnsRepository.GetAllTurnsAsync();
        }
        public Task<Turns> GetByIdAsync(int id)
        {
            return _turnsRepository.GetAsync(id);
        }
        public async Task _PostAsync(Turns value)
        {
            _turnsRepository.Post(value);
            await _turnsRepository.SaveAsync();

        }
        public async Task _PutAsync(int id, Turns value)
        {
            _turnsRepository.Put(id, value);
            await _turnsRepository.SaveAsync();
        }
        public async Task _DeleteAsync(int id)
        {
            _turnsRepository.Delete(id);
            await _turnsRepository.SaveAsync();
        }

    }
}
