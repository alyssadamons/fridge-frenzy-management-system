using E_Commerce.Models;
using E_Commerce.Areas.Dashboard.Models;

namespace E_Commerce.Areas.Identity.ViewModels
{
    public class CustomerFridgesViewModel
    {
        public List<FridgeRegistration> Fridges { get; set; } = new List<FridgeRegistration>();
        public Dictionary<int, List<Appointment>> FridgeServiceHistory { get; set; } = new Dictionary<int, List<Appointment>>();
        public int TotalFridges { get; set; }
        public int TotalServices { get; set; }
    }
}