using AutoMapper;
using project.Models.BabiesModels;
using project.Models.NursesModels;
using project.Models.TurnesModel;
using Project.Core.DTO;

namespace project
{
    public class MappingPostProfile:Profile
    {
        public MappingPostProfile()
        {
            CreateMap<BabiesPostModel, Babies>().ReverseMap();
            CreateMap<NursesPostModel, Nurses>().ReverseMap();
            CreateMap<TurnesPostModel, Turns>().ReverseMap();

        }
    }
}
