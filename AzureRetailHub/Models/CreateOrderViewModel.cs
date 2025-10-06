/*
 * Jeron Okkers
 * ST10447759
 * PROG6221
 */ 
namespace AzureRetailHub.Models
{
    public class CreateOrderViewModel
    {
        public OrderDto Order { get; set; } = new OrderDto();
        public IEnumerable<CustomerDto> AvailableCustomers { get; set; } = new List<CustomerDto>();
        public IEnumerable<ProductDto> AvailableProducts { get; set; } = new List<ProductDto>();
    }
}
//================================================================================================================================================================//
