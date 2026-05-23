using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using project.Models.BabiesModels;
using Project.core.Servies;
using Project.Core.DTO;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace project.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BabiesController : ControllerBase
    {
        private readonly IBabiesServies _babiesService;

        private readonly IMapper _mapper;
        public BabiesController(IBabiesServies babiesService, IMapper map)
        {
            _babiesService = babiesService;
            _mapper = map;
        }
        // GET: api/<BabyCareController>
        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var babies = await _babiesService.GetAllAsync();

            return Ok(_mapper.Map<List<BabyDTO>>(babies));
        }

        // GET api/<BabyCareController>/5
        [HttpGet("{id}")]
        public async Task<ActionResult> Get(int id)
        {
            var baby = await _babiesService.GetByIdAsync(id);
            if (baby == null)
                return NotFound();
            return Ok(_mapper.Map<BabyDTO>(baby));
        }

        [HttpPost]
        public async Task<ActionResult> Post([FromBody] BabiesPostModel value)
        {
            var baby = await _babiesService.GetByIdAsync(value.id);
            if (baby != null)
                return Conflict();
            var babies = _mapper.Map<Babies>(value);
            await _babiesService.AddAsync(babies);
            return Ok();
        }

        // PUT api/<BabyCareController>/5
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(int id, [FromBody] BabiesPutModel value)
        {
            var  baby = await _babiesService.GetByIdAsync(id);
            if (baby == null)
                return NotFound();
            var babies = _mapper.Map<Babies>(value);
            await _babiesService._Put(id, babies);
            return Ok();
        }

        // DELETE api/<BabyCareController>/5
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var baby = await _babiesService.GetByIdAsync(id);
            if (baby == null)
                return Conflict();
            await _babiesService._Delete(id);
            return Ok();
        }
    }
}
