using INI.AUB.Inventory.System.API.Enums;

namespace invetoryBackGroundServices.Params
{
    public class MachineConfig
    {

        public string ip { get; set; }
        public int feederId { get; set; }
        public int hooperId { get; set; }
        public int rejectedId { get; set; }
        public string port { get; set; }

        public string model { get; set; }
        public string name { get; set; }
        public string sn { get; set; }
        public string branchName { get; set; }

    }
}
