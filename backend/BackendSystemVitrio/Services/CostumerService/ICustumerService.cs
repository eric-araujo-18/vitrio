using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Wrappers;

namespace BackendSystemVitrio.Services.CustomerService
{
    // Área do cliente da vitrine: endereços salvos.
    // (Os pedidos do cliente continuam no OrderService.)
    public interface ICustomerService
    {
        Task<Response<List<AddressResponseDto>>> GetAddressesAsync(int userId);
        Task<Response<AddressResponseDto>> CreateAddressAsync(int userId, AddressInputDto dto);
        Task<Response<AddressResponseDto>> UpdateAddressAsync(int userId, int addressId, AddressInputDto dto);
        Task<Response<string>> DeleteAddressAsync(int userId, int addressId);
    }
}