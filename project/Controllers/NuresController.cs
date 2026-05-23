using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using project.Models.NursesModels;
using Project.core.Servies;
using Project.Core.DTO;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace project.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NuresController : ControllerBase
    {
        private readonly INursesServies _nursesServies;
        private readonly IMapper _mapper;

        public NuresController(INursesServies nursesService, IMapper map)
        {
            _nursesServies = nursesService;
            _mapper = map;
        }
        // GET: api/<NuresController>
        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var nurses = await _nursesServies.GetAllAsync();
            return Ok(_mapper.Map<List<NursesDTO>>(nurses));
        }

        // GET api/<NuresController>/5
        [HttpGet("{id}")]
        public async Task<ActionResult> Get(int id)
        {
            var nurse = await _nursesServies.GetByIdAsync(id);
            if (nurse == null)
                return NotFound();
            return Ok(_mapper.Map<NursesDTO>(nurse));
        }

        // POST api/<NuresController>
        [HttpPost]
        public async Task<ActionResult> Post([FromBody] NursesPostModel value)
        {
            var nurse = await _nursesServies.GetByIdAsync(value.id);
            if (nurse != null)
                return Conflict();
            var nurses = _mapper.Map<Nurses>(value);
            await _nursesServies.AddAsync(nurses);
            return Ok();
        }

        // PUT api/<NuresController>/5
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(int id, [FromBody] NursesPutModel value)
        {
            var nurse = await _nursesServies.GetByIdAsync(id);
            if (nurse == null)
                return NotFound();
            var nurses = _mapper.Map<Nurses>(value);
            await _nursesServies._PutAsync(id, nurses);
            return Ok();
        }

        // DELETE api/<NuresController>/5
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var nurse = await _nursesServies.GetByIdAsync(id);
            if (nurse == null)
                return NotFound();
            await _nursesServies._DeleteAsync(id);
            return Ok();
        }
    }
}
