using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Core.DTO
{
    public class BabyDTO
    {
        public int id { get; set; }

        public string name { get; set; } = string.Empty;

        public int age { get; set; }

        public string family { get; set; } = string.Empty;
    }
}
