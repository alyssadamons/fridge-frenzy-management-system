using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Areas.Identity.ViewModels
{
    public class FridgeStatsViewModel
    {
        public int TotalFridges { get; set; }
        public int UnderWarranty { get; set; }
        public int TotalServices { get; set; }
        public int RecentServices { get; set; }
    }
}