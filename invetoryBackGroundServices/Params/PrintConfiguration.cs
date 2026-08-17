using INI.AUB.Inventory.System.API.Enums;

namespace AUBServicesLayer.Params
{
    public class PrintConfiguration
    {

        public string productName { get; set; }
        public int printedFace { get; set; }
        public int font { get; set; }
        public int cpi { get; set; }
        public int offSetX { get; set; }
        public int offSetY { get; set; }
        public string image { get; set; }

    }
}
