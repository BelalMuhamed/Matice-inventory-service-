using System.ComponentModel.DataAnnotations;

namespace invetoryBackGroundServices.Params
{
    public class Branch
    {
       
            public int id { get; set; }

            [MaxLength(255)]
            public string branchName { get; set; } = string.Empty;
            public int lowCardAmount { get; set; }
            public int mediumCardAmount { get; set; }

        
    }
}
