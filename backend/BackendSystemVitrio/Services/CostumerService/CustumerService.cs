using System.Linq.Expressions;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.DTO;
using BackendSystemVitrio.Helpers;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.CustomerService
{
    public class CustomerService : ICustomerService
    {
        private const int MaxAddressesPerUser = 10;
        private const int MaxLabelLength = 40;

        private readonly AppDbContext _context;

        public CustomerService(AppDbContext context)
        {
            _context = context;
        }

        // Padrão primeiro, depois os mais recentes.
        public async Task<Response<List<AddressResponseDto>>> GetAddressesAsync(int userId)
        {
            try
            {
                var addresses = await _context.CustomerAddress
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.CreationDate)
                    .Select(ToDtoExpression)
                    .ToListAsync();

                return Response<List<AddressResponseDto>>.Ok(addresses);
            }
            catch (Exception ex)
            {
                return Response<List<AddressResponseDto>>.Fail($"Erro ao buscar endereços: {ex.Message}");
            }
        }

        public async Task<Response<AddressResponseDto>> CreateAddressAsync(int userId, AddressInputDto dto)
        {
            try
            {
                var error = AddressHelper.Normalize(dto, out var data) ?? ValidateLabel(dto.Label);
                if (error is not null)
                    return Response<AddressResponseDto>.Fail(error);

                var count = await _context.CustomerAddress.CountAsync(a => a.UserId == userId);
                if (count >= MaxAddressesPerUser)
                    return Response<AddressResponseDto>.Fail($"Você pode salvar no máximo {MaxAddressesPerUser} endereços.");

                // O primeiro endereço já vira o padrão.
                var isDefault = dto.IsDefault || count == 0;
                if (isDefault)
                    await ClearDefaultAsync(userId);

                var address = new CustomerAddress
                {
                    UserId = userId,
                    Label = CleanLabel(dto.Label),
                    Cep = data!.Cep,
                    State = data.State,
                    City = data.City,
                    Neighborhood = data.Neighborhood,
                    Street = data.Street,
                    Number = data.Number,
                    Complement = data.Complement,
                    IsDefault = isDefault,
                };

                _context.CustomerAddress.Add(address);
                await _context.SaveChangesAsync();

                return Response<AddressResponseDto>.Ok(ToDto(address), "Endereço salvo.");
            }
            catch (Exception ex)
            {
                return Response<AddressResponseDto>.Fail($"Erro ao salvar endereço: {ex.Message}");
            }
        }

        public async Task<Response<AddressResponseDto>> UpdateAddressAsync(int userId, int addressId, AddressInputDto dto)
        {
            try
            {
                // Filtra pelo dono: um cliente nunca edita o endereço de outro.
                var address = await _context.CustomerAddress
                    .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);
                if (address is null)
                    return Response<AddressResponseDto>.Fail("Endereço não encontrado.");

                var error = AddressHelper.Normalize(dto, out var data) ?? ValidateLabel(dto.Label);
                if (error is not null)
                    return Response<AddressResponseDto>.Fail(error);

                // Desmarcar o padrão não é permitido direto: o cliente escolhe outro como padrão.
                if (dto.IsDefault && !address.IsDefault)
                {
                    await ClearDefaultAsync(userId);
                    address.IsDefault = true;
                }

                address.Label = CleanLabel(dto.Label);
                address.Cep = data!.Cep;
                address.State = data.State;
                address.City = data.City;
                address.Neighborhood = data.Neighborhood;
                address.Street = data.Street;
                address.Number = data.Number;
                address.Complement = data.Complement;
                address.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Response<AddressResponseDto>.Ok(ToDto(address), "Endereço atualizado.");
            }
            catch (Exception ex)
            {
                return Response<AddressResponseDto>.Fail($"Erro ao atualizar endereço: {ex.Message}");
            }
        }

        public async Task<Response<string>> DeleteAddressAsync(int userId, int addressId)
        {
            try
            {
                var address = await _context.CustomerAddress
                    .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);
                if (address is null)
                    return Response<string>.Fail("Endereço não encontrado.");

                _context.CustomerAddress.Remove(address);

                // Se era o padrão, o mais recente que sobrar vira o novo padrão.
                if (address.IsDefault)
                {
                    var next = await _context.CustomerAddress
                        .Where(a => a.UserId == userId && a.Id != addressId)
                        .OrderByDescending(a => a.CreationDate)
                        .FirstOrDefaultAsync();
                    if (next is not null)
                        next.IsDefault = true;
                }

                await _context.SaveChangesAsync();

                // Pedidos antigos não mudam: eles têm a própria cópia do endereço.
                return Response<string>.Ok("Endereço removido.", "Endereço removido.");
            }
            catch (Exception ex)
            {
                return Response<string>.Fail($"Erro ao remover endereço: {ex.Message}");
            }
        }

        // ===== Helpers =====

        private Task ClearDefaultAsync(int userId)
            => _context.CustomerAddress
                .Where(a => a.UserId == userId && a.IsDefault)
                .ExecuteUpdateAsync(set => set.SetProperty(a => a.IsDefault, false));

        private static string? ValidateLabel(string? label)
            => label is not null && label.Trim().Length > MaxLabelLength
                ? $"O apelido do endereço pode ter no máximo {MaxLabelLength} caracteres."
                : null;

        private static string? CleanLabel(string? label)
            => string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        private static AddressResponseDto ToDto(CustomerAddress a) => ToDtoExpression.Compile()(a);

        private static readonly Expression<Func<CustomerAddress, AddressResponseDto>> ToDtoExpression = a => new AddressResponseDto
        {
            Id = a.Id,
            Label = a.Label,
            Cep = a.Cep,
            State = a.State,
            City = a.City,
            Neighborhood = a.Neighborhood,
            Street = a.Street,
            Number = a.Number,
            Complement = a.Complement,
            IsDefault = a.IsDefault,
        };
    }
}