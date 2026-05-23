using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Core.DTO
{
    public class NursesDTO
    {
        public int id { get; set; }

        public string name { get; set; } = string.Empty;

        public string phone { get; set; } = string.Empty;

        public string email { get; set; } = string.Empty;

        public string specialization { get; set; } = string.Empty;
    }
}
