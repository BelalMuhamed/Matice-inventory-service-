using INI.AUB.Inventory.System.API.Enums;
using MATICA_S3300e.CLS;

namespace AUBServicesLayer.Params
{
    public class MachineConfigResponse
    {
        public string Ip { get; set; }
        public int FeederId { get; set; }
        public int HooperId { get; set; }
        public int RejectedId { get; set; }
        public string Port { get; set; }
        public int TipTemp { get; set; }
        public int TipPres { get; set; }
        public int TipCons { get; set; }
        public int TipTime { get; set; }
        public string Model { get; set; }
        public string Name { get; set; }
        public string Sn { get; set; }
        public string BranchName { get; set; }
        public MachineStatus MStatus { get; set; }
    }
}
