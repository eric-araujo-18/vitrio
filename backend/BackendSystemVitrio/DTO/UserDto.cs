namespace BackendSystemVitrio.DTO
{
    // Nunca inclua PasswordHash/PasswordSalt aqui — este DTO é o que
    // trafega pro frontend.
    public class UserDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }
        public string? Phone { get; set; }
        public string? Cpf { get; set; }
        public required string Role { get; set; }
    }
}