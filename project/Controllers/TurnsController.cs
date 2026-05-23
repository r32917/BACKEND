using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using project.Models.TurnesModel;
using Project.core.Servies;
using Project.Core.DTO;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace project.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TurnsController : ControllerBase
    {

        private readonly ITurnsServies _turnsServies;
        private readonly IMapper _mapper;
        public TurnsController(ITurnsServies turnsService, IMapper map)
        {
            _turnsServies = turnsService;
            _mapper = map;
        }
        // GET: api/<TurnsController>
        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var turnes = await _turnsServies.GetAllTurnsAsync();
            return Ok(_mapper.Map<List<TurnesDTO>>(turnes));

        }

        // GET api/<TurnsController>/5
        [HttpGet("{id}")]
        public async Task<ActionResult> Get(int id)
        {
            var turn = await _turnsServies.GetByIdAsync(id);
            if (turn == null)
                return NotFound();
            return Ok(_mapper.Map<TurnesDTO>(turn));
        }

        // POST api/<TurnsController>
        [HttpPost]
        public async Task<ActionResult> Post([FromBody] TurnesPostModel value)
        {
            var turn = await _turnsServies.GetByIdAsync(value.id);
            if (turn != null)
                return Conflict();
            var turnes = _mapper.Map<Turns>(value);
            await _turnsServies._PostAsync(turnes);
            return Ok();
        }

        // PUT api/<TurnsController>/5
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(int id, [FromBody] TurnesPutModel value)
        {
            var turn = await _turnsServies.GetByIdAsync(id);
            if (turn == null)
                return NotFound();
            var turnes = _mapper.Map<Turns>(value);
            await _turnsServies._PutAsync(id, turnes);
            return Ok();
        }

        // DELETE api/<TurnsController>/5
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var turn = await _turnsServies.GetByIdAsync(id);
            if (turn == null)
                return NotFound();
            await _turnsServies._DeleteAsync(id);
            return Ok();
        }
    }
}
