using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Core.DTO
{
    public class TurnesDTO
    {
        public int id { get; set; }

        public int babyId { get; set; }

        public int nurseId { get; set; }

        public string date { get; set; } = string.Empty;

        public string time { get; set; } = string.Empty;

        public string notes { get; set; } = string.Empty;
    }
}
