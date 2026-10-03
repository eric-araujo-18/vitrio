namespace BackendSystemVitrio.Models
{
    // Endereço salvo na conta do cliente da vitrine.
    // O pedido NÃO aponta para esta tabela: ele guarda uma cópia do endereço
    // (Order.Shipping*), então editar ou apagar um endereço não muda pedidos antigos.
    public class CustomerAddress
    {
        public int Id { get; set; }

        public required int UserId { get; set; }
        public User? User { get; set; }

        // Apelido opcional: "Casa", "Trabalho"...
        public string? Label { get; set; }

        public required string Cep { get; set; }          // só dígitos (8)
        public required string State { get; set; }        // UF: "CE"
        public required string City { get; set; }         // município
        public string? Neighborhood { get; set; }         // bairro
        public required string Street { get; set; }       // rua
        public required string Number { get; set; }       // número ("S/N" se não tiver)
        public string? Complement { get; set; }

        // Endereço já selecionado no checkout
        public bool IsDefault { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }
    }
}