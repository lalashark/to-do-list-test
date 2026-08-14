using Microsoft.EntityFrameworkCore;
using TodoList.Api.Models;

namespace TodoList.Api.Data
{
    public class TodoDbContext : DbContext
    {
        public TodoDbContext(DbContextOptions<TodoDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Todo> Todos => Set<Todo>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(254);
                
                // Case-insensitive configuration per database provider
                if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
                {
                    modelBuilder.HasPostgresExtension("citext");
                    entity.Property(e => e.Email).HasColumnType("citext");
                }
                else if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                {
                    entity.Property(e => e.Email).UseCollation("NOCASE");
                }

                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<Todo>(entity =>
            {
                entity.ToTable("Todos");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                
                entity.Property(e => e.Status)
                      .HasConversion<string>()
                      .IsRequired();
                
                entity.Property(e => e.Priority)
                      .HasConversion<string>()
                      .IsRequired();

                entity.HasOne(e => e.Owner)
                      .WithMany(u => u.Todos)
                      .HasForeignKey(e => e.OwnerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
