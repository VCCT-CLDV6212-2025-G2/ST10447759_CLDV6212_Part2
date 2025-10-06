/*
 * Jeron Okkers
 * ST10447759
 * PROG6221
 */ 
using System.ComponentModel.DataAnnotations;

namespace AzureRetailHub.Models
{
    public class ProductDto
    {
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        [Required] public string Name { get; set; } = "";
        public string? Description { get; set; }
        [Required] public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
    }
}
//================================================================================================================================================================//
