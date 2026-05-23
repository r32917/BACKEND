using AutoMapper;
using project.Models.BabiesModels;
using project.Models.NursesModels;
using project.Models.TurnesModel;
using Project.Core.DTO;

namespace project
{
    public class MappingPutProfile:Profile
    {
        public MappingPutProfile()
        {
            CreateMap<BabiesPutModel, Babies>().ReverseMap();
            CreateMap<NursesPutModel, Nurses>().ReverseMap();
            CreateMap<TurnesPutModel, Turns>().ReverseMap();
        }
    }
}
