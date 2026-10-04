using BackendSystemVitrio.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> User { get; set; }
        public DbSet<Store> Store { get; set; }
        public DbSet<Product> Product { get; set; }
        public DbSet<Category> Category { get; set; }
        public DbSet<ProductImage> ProductImage { get; set; }
        public DbSet<ProductVariant> ProductVariant { get; set; }
        public DbSet<CustomerAddress> CustomerAddress { get; set; }
        public DbSet<Plan> Plan { get; set; }
        public DbSet<Subscription> Subscription { get; set; }
        public DbSet<PaymentWebhookEvent> PaymentWebhookEvent { get; set; }
        public DbSet<RefreshToken> RefreshToken { get; set; }
        public DbSet<Order> Order { get; set; }
        public DbSet<OrderItem> OrderItem { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== User =====
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Cpf)
                .IsUnique(); // vários NULL (clientes sem CPF) não conflitam no PostgreSQL

            // ===== Store =====
            modelBuilder.Entity<Store>()
                .HasIndex(s => s.Cnpj)
                .IsUnique();

            modelBuilder.Entity<Store>()
                .HasIndex(s => s.Name)
                .IsUnique();

            modelBuilder.Entity<Store>()
                .HasIndex(s => s.Slug)
                .IsUnique();

            modelBuilder.Entity<Store>()
                .HasOne(s => s.User)
                .WithMany(u => u.Stores)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== Category =====
            // Slug único por loja, ignorando categorias excluídas (soft delete),
            // pra permitir recriar uma categoria com o mesmo nome depois.
            modelBuilder.Entity<Category>()
                .HasIndex(c => new { c.StoreId, c.Slug })
                .IsUnique()
                .HasFilter("\"DeletionDate\" IS NULL");

            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== Product =====
            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.StoreId, p.Slug })
                .IsUnique()
                .HasFilter("\"DeletionDate\" IS NULL");

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Product>()
                .Property(p => p.PromotionalPrice)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.ColorGroupId);

            modelBuilder.Entity<Product>()
                .Property(p => p.ColorName)
                .HasMaxLength(40);

            modelBuilder.Entity<Product>()
                .Property(p => p.ColorHex)
                .HasMaxLength(7);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // ===== Order -> cliente logado (opcional) =====
            // Pedido feito sem login fica com CustomerUserId nulo.
            // Se a conta for apagada, o pedido continua (com os dados copiados no pedido).
            modelBuilder.Entity<Order>()
                .HasOne(o => o.CustomerUser)
                .WithMany()
                .HasForeignKey(o => o.CustomerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.CustomerUserId);

            // ===== Plan =====
            modelBuilder.Entity<Plan>(plan =>
            {
                plan.HasIndex(p => p.Code).IsUnique();
                plan.Property(p => p.Code).HasMaxLength(30);
                plan.Property(p => p.Name).HasMaxLength(60);
                plan.Property(p => p.Description).HasMaxLength(300);
                plan.Property(p => p.PriceMonthly).HasPrecision(10, 2);

                // Planos iniciais. Para mudar preço/limite: altere aqui e gere uma migration.
                plan.HasData(
                    new Plan
                    {
                        Id = Models.Plan.FreeId,
                        Code = "free",
                        Name = "Grátis",
                        Description = "Para começar a vender.",
                        PriceMonthly = 0m,
                        MaxStores = 1,
                        MaxProductsPerStore = 30,
                        AllowsOnlinePayment = false,
                        SortOrder = 0,
                    },
                    new Plan
                    {
                        Id = 2,
                        Code = "basic",
                        Name = "Essencial",
                        Description = "Para a loja que já vende todo dia.",
                        PriceMonthly = 29.90m,
                        MaxStores = 1,
                        MaxProductsPerStore = 300,
                        AllowsOnlinePayment = true,
                        SortOrder = 1,
                    },
                    new Plan
                    {
                        Id = 3,
                        Code = "pro",
                        Name = "Profissional",
                        Description = "Para quem tem mais de uma loja.",
                        PriceMonthly = 59.90m,
                        MaxStores = 3,
                        MaxProductsPerStore = null,
                        AllowsOnlinePayment = true,
                        SortOrder = 2,
                    });
            });

            // ===== Subscription =====
            modelBuilder.Entity<Subscription>(sub =>
            {
                sub.HasIndex(s => s.UserId).IsUnique(); // uma assinatura por lojista
                sub.HasIndex(s => s.GatewaySubscriptionId);
                sub.Property(s => s.GatewaySubscriptionId).HasMaxLength(100);

                sub.HasOne(s => s.User)
                    .WithMany()
                    .HasForeignKey(s => s.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                sub.HasOne(s => s.Plan)
                    .WithMany()
                    .HasForeignKey(s => s.PlanId)
                    .OnDelete(DeleteBehavior.Restrict);

                sub.HasOne(s => s.PendingPlan)
                    .WithMany()
                    .HasForeignKey(s => s.PendingPlanId)
                    .OnDelete(DeleteBehavior.Restrict);

                sub.HasOne(s => s.ScheduledPlan)
                    .WithMany()
                    .HasForeignKey(s => s.ScheduledPlanId)
                    .OnDelete(DeleteBehavior.Restrict);

                sub.HasIndex(s => s.PendingGatewaySubscriptionId);
                sub.Property(s => s.PendingGatewaySubscriptionId).HasMaxLength(100);
            });

            // ===== PaymentWebhookEvent =====
            modelBuilder.Entity<PaymentWebhookEvent>(evt =>
            {
                evt.HasIndex(e => new { e.Provider, e.NotificationId }).IsUnique();
                evt.Property(e => e.Provider).HasMaxLength(30);
                evt.Property(e => e.NotificationId).HasMaxLength(100);
                evt.Property(e => e.Type).HasMaxLength(60);
                evt.Property(e => e.Action).HasMaxLength(60);
                evt.Property(e => e.DataId).HasMaxLength(100);
                evt.Property(e => e.Error).HasMaxLength(2000);
            });

            // ===== Endereço de entrega no pedido =====
            modelBuilder.Entity<Order>(order =>
            {
                order.Property(o => o.ShippingCep).HasMaxLength(8);
                order.Property(o => o.ShippingState).HasMaxLength(2);
                order.Property(o => o.ShippingCity).HasMaxLength(100);
                order.Property(o => o.ShippingNeighborhood).HasMaxLength(100);
                order.Property(o => o.ShippingStreet).HasMaxLength(150);
                order.Property(o => o.ShippingNumber).HasMaxLength(20);
                order.Property(o => o.ShippingComplement).HasMaxLength(100);
            });

            // ===== CustomerAddress =====
            // Apagar a conta apaga os endereços dela (os pedidos têm a própria cópia).
            modelBuilder.Entity<CustomerAddress>(address =>
            {
                address.HasOne(a => a.User)
                    .WithMany()
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                address.HasIndex(a => a.UserId);

                address.Property(a => a.Label).HasMaxLength(40);
                address.Property(a => a.Cep).HasMaxLength(8);
                address.Property(a => a.State).HasMaxLength(2);
                address.Property(a => a.City).HasMaxLength(100);
                address.Property(a => a.Neighborhood).HasMaxLength(100);
                address.Property(a => a.Street).HasMaxLength(150);
                address.Property(a => a.Number).HasMaxLength(20);
                address.Property(a => a.Complement).HasMaxLength(100);
            });

            // ===== ProductVariant =====
            // Um tamanho não pode se repetir no mesmo produto.
            modelBuilder.Entity<ProductVariant>()
                .HasIndex(v => new { v.ProductId, v.Size })
                .IsUnique();

            modelBuilder.Entity<ProductVariant>()
                .Property(v => v.Size)
                .HasMaxLength(20);

            modelBuilder.Entity<ProductVariant>()
                .HasOne(v => v.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== RefreshToken =====
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.Token)
                .IsUnique();

            // ===== Order =====
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.Code)
                .IsUnique();

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.StoreId, o.CreationDate });

            modelBuilder.Entity<Order>()
                .Property(o => o.Total)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Store)
                .WithMany()
                .HasForeignKey(o => o.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .Property(i => i.UnitPrice)
                .HasPrecision(12, 2);

            modelBuilder.Entity<OrderItem>()
                .HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Se o produto for apagado de verdade, o item do pedido continua
            // existindo (com nome/preço salvos), só perde o vínculo.
            modelBuilder.Entity<OrderItem>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            // Mesma ideia para o tamanho: se a variação for apagada,
            // o item mantém o texto do tamanho (Size), só perde o vínculo.
            modelBuilder.Entity<OrderItem>()
                .HasOne(i => i.Variant)
                .WithMany()
                .HasForeignKey(i => i.VariantId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrderItem>()
                .Property(i => i.Size)
                .HasMaxLength(20);

            modelBuilder.Entity<OrderItem>()
                .Property(i => i.Color)
                .HasMaxLength(40);
        }
    }
}