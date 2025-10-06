/*
 * Jeron Okkers
 * ST10447759
 * PROG6221
 */ 
using System.ComponentModel.DataAnnotations;

namespace AzureRetailHub.Models
{
    public class OrderDto
    {
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        [Required] public string CustomerId { get; set; } = "";
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string? ItemsJson { get; set; }
        public string? Status { get; set; } = "New";
    }
}
//================================================================================================================================================================//
