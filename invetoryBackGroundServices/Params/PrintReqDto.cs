namespace invetoryBackGroundServices.Params
{
    public class PrintReqDto
    {
        public required string cardHolderName { get; set; }
        public required string machineIp { get; set; }
        public required string userName { get; set; }
        public required string productName { get; set; }
        public required string token { get; set; }
        public required string branchName { get; set; }
        public int printedFace { get; set; } = 0;
    }
}
