/*
 * Jeron Okkers
 * ST10447759
 * PROG6221
 */ 
using System.ComponentModel.DataAnnotations;

namespace AzureRetailHub.Models
{
    public class CustomerDto
    {
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        [Required] public string FullName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }
}
//================================================================================================================================================================//
