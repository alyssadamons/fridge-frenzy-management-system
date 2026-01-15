using E_Commerce.Models;
using System.Collections.Generic;

namespace E_Commerce.ViewModels
{
    public class ProductViewModel
    {
        public List<Product> Products { get; set; } = new List<Product>();
        public List<Category> Categories { get; set; } = new List<Category>();

        // New properties for filtering and sorting
        public int? SelectedCategoryId { get; set; }
        public string SortBy { get; set; } = "name";
        public string PriceRange { get; set; } = "all";

        // Properties for displaying filter information
        public int TotalProducts => Products?.Count ?? 0;
        public string CurrentSortDisplay => GetSortDisplayName();
        public string CurrentPriceRangeDisplay => GetPriceRangeDisplayName();

        // Helper methods for display
        private string GetSortDisplayName()
        {
            return SortBy switch
            {
                "name" => "Name (A-Z)",
                "price_low" => "Price (Low to High)",
                "price_high" => "Price (High to Low)",
                _ => "Name (A-Z)"
            };
        }

        private string GetPriceRangeDisplayName()
        {
            return PriceRange switch
            {
                "all" => "All Prices",
                "under1000" => "Under R1,000",
                "1000-5000" => "R1,000 - R5,000",
                "5000-10000" => "R5,000 - R10,000",
                "over10000" => "Over R10,000",
                _ => "All Prices"
            };
        }

        // Method to check if any filters are active
        public bool HasActiveFilters =>
            SelectedCategoryId.HasValue ||
            (PriceRange != "all" && !string.IsNullOrEmpty(PriceRange)) ||
            (SortBy != "name" && !string.IsNullOrEmpty(SortBy));

        // Method to get current category name
        public string GetCurrentCategoryName()
        {
            if (!SelectedCategoryId.HasValue) return "All Categories";
            var category = Categories?.FirstOrDefault(c => c.Id == SelectedCategoryId.Value);
            return category?.Name ?? "All Categories";
        }
    }
}