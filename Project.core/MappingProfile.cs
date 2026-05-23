using AutoMapper;
using project;
using Project.Core.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Core
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Babies, BabyDTO>().ReverseMap();
            CreateMap<Nurses, NursesDTO>().ReverseMap();
            CreateMap<Turns, TurnesDTO>().ReverseMap();

        }
    }
}
