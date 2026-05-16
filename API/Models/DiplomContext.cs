// 📁 API/Models/DiplomContext.cs
using API.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace API.Models;

public partial class DiplomContext : DbContext
{
    public DiplomContext()
    {
    }

    public DiplomContext(DbContextOptions<DiplomContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Cart> Carts { get; set; }
    public virtual DbSet<CartItem> CartItems { get; set; }
    public virtual DbSet<Favorite> Favorites { get; set; }
    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<OrderItem> OrderItems { get; set; }
    public virtual DbSet<OrderStatus> OrderStatuses { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<ProductCategory> ProductCategories { get; set; }
    public virtual DbSet<Review> Reviews { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<EmailVerificationCode> EmailVerificationCodes { get; set; }

    // 🔹 🔹 🔹 НОВОЕ: Таблица истории движений склада
    public virtual DbSet<StockMovement> StockMovements { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=diplom;Username=postgres;Password=1234");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cart_pkey");
            entity.ToTable("cart");
            entity.HasIndex(e => e.UserId, "cart_user_id_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne(d => d.User).WithOne(p => p.Cart)
                .HasForeignKey<Cart>(d => d.UserId)
                .HasConstraintName("cart_user_id_fkey");
        });

        modelBuilder.Entity<EmailVerificationCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("email_verification_codes");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.Code).IsRequired().HasMaxLength(6).HasColumnName("code");
            entity.Property(e => e.ExpiresAt).IsRequired().HasColumnName("expires_at");
            entity.Property(e => e.IsUsed).HasDefaultValue(false).HasColumnName("is_used");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.HasIndex(e => e.Email).HasDatabaseName("idx_email_verification_codes_email");
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("idx_email_verification_codes_code");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cart_items_pkey");
            entity.ToTable("cart_items");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AddedAt).HasDefaultValueSql("now()").HasColumnName("added_at");
            entity.Property(e => e.CartId).HasColumnName("cart_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("cart_items_cart_id_fkey");
            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("cart_items_product_id_fkey");
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("favorites_pkey");
            entity.ToTable("favorites");
            entity.HasIndex(e => new { e.UserId, e.ProductId }, "favorites_user_id_product_id_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AddedAt).HasDefaultValueSql("now()").HasColumnName("added_at");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne(d => d.Product).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("favorites_product_id_fkey");
            entity.HasOne(d => d.User).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("favorites_user_id_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orders_pkey");
            entity.ToTable("orders");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.TotalAmount).HasPrecision(10, 2).HasColumnName("total_amount");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            // 🔹 🔹 🔹 НОВОЕ: Поле CashierId
            entity.Property(e => e.CashierId).HasColumnName("CashierId");



            // 🔹 Существующая связь: заказ → клиент (User)
            entity.HasOne(d => d.User)
                .WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_user_id_fkey");

            // 🔹 🔹 🔹 НОВАЯ связь: заказ → кассир (Cashier)
            entity.HasOne(d => d.Cashier)
                .WithMany()  // 🔹 Не добавляем навигацию User.Orders, чтобы не конфликтовать с клиентом
                .HasForeignKey(d => d.CashierId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_cashier_id_fkey");

            // 🔹 Существующая связь: заказ → статус
            entity.HasOne(d => d.Status)
                .WithMany(p => p.Orders)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_status_id_fkey");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_items_pkey");
            entity.ToTable("order_items");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PriceAtTime).HasPrecision(10, 2).HasColumnName("price_at_time");
            entity.Property(e => e.ProductBrand).HasMaxLength(100).HasColumnName("product_brand");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ProductName).HasMaxLength(255).HasColumnName("product_name");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("order_items_order_id_fkey");
            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("order_items_product_id_fkey");
        });

        modelBuilder.Entity<OrderStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_statuses_pkey");
            entity.ToTable("order_statuses");
            entity.HasIndex(e => e.Name, "order_statuses_name_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("products_pkey");
            entity.ToTable("products");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Attributes).HasColumnType("jsonb").HasColumnName("attributes");
            entity.Property(e => e.Brand).HasMaxLength(100).HasColumnName("brand");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Image).HasMaxLength(100).HasColumnName("image");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
            entity.Property(e => e.NewPrice).HasPrecision(10, 2).HasColumnName("new_price");
            entity.Property(e => e.Price).HasPrecision(10, 2).HasColumnName("price");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            // 🔹 Добавлены поля склада
            entity.Property(e => e.StockQuantity).HasDefaultValue(0).HasColumnName("stock_quantity");
            entity.Property(e => e.MinStockThreshold).HasDefaultValue(5).HasColumnName("min_stock_threshold");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("products_category_id_fkey");
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_categories_pkey");
            entity.ToTable("product_categories");
            entity.HasIndex(e => e.Slug, "product_categories_slug_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
            entity.Property(e => e.Order).HasDefaultValue(0).HasColumnName("order");
            entity.Property(e => e.Slug).HasMaxLength(100).HasColumnName("slug");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("reviews_pkey");
            entity.ToTable("reviews");
            entity.HasIndex(e => new { e.UserId, e.ProductId }, "reviews_user_id_product_id_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne(d => d.Product).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("reviews_product_id_fkey");
            entity.HasOne(d => d.User).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("reviews_user_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");
            entity.ToTable("roles");
            entity.HasIndex(e => e.Name, "roles_name_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name).HasMaxLength(50).HasColumnName("name");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");
            entity.ToTable("users");
            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();
            entity.HasIndex(e => e.Login, "users_login_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Email).HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.Login).HasMaxLength(100).HasColumnName("login");
            entity.Property(e => e.PasswordHash).HasMaxLength(255).HasColumnName("password_hash");
            entity.Property(e => e.Phone).HasMaxLength(20).HasColumnName("phone");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("users_role_id_fkey");
        });

        // 🔹 🔹 🔹 НОВАЯ конфигурация для StockMovement
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("stock_movements_pkey");
            entity.ToTable("stock_movements");

            // 🔹 Индексы для ускорения поиска
            entity.HasIndex(e => e.ProductId).HasDatabaseName("idx_stock_movements_product_id");
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_stock_movements_user_id");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_stock_movements_created_at");

            // 🔹 Свойства с snake_case колонками
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.QuantityChange).HasColumnName("quantity_change");
            entity.Property(e => e.MovementType).HasMaxLength(20).HasColumnName("movement_type");
            entity.Property(e => e.Comment).HasMaxLength(500).HasColumnName("comment");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            // 🔹 Связь с Product (CASCADE: при удалении товара удаляется история)
            entity.HasOne(d => d.Product)
                .WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("stock_movements_product_id_fkey");

            // 🔹 Связь с User (RESTRICT: нельзя удалить пользователя с историей операций)
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("stock_movements_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    private readonly IDatabaseHealthCheck _healthCheck;

    public DiplomContext(DbContextOptions<DiplomContext> options, IDatabaseHealthCheck healthCheck)
        : base(options)
    {
        _healthCheck = healthCheck;
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}