namespace AUBServicesLayer.Params
{
    public class PrintCardRequest
    {

        public string CardHolderName { get; set; }
        public int ProductId { get; set; }
        public string Username { get; set; }
        public int branchid { get; set; }
        public MachineConfigResponse MachineConfigs { get; set; }
        public PrintConfiguration PrintConfigs { get; set; }
    }
}
