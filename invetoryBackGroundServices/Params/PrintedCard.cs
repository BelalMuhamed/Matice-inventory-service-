using INI.AUB.Inventory.System.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace INI.AUB.Inventory.System.API.DTO.Card
{
    public class PrintedCard
    {
        public string? ClearPan { get; set; }
        public string? CardHolderName { get; set; }
        public int BranchId { get; set; }
        public string? BranchName { get; set; }
        public string? Username { get; set; }
        public CardStatus? Status { get; set; }
        public string? Message { get; set; }  
        public int ProductId { get; set; }
    }
}
