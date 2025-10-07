using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegratoR.RELion.Domain.DTOs
{
    public class RelionCompanyDataWrapper<T>
    {
        /// <summary>
        /// The actual data returned from Relion.
        /// </summary>
        [JsonProperty("value")]
        public List<T> Data { get; set; } = new();
    }
}
