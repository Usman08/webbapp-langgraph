using Microsoft.EntityFrameworkCore;
using SalesInvoice.Domain.Entities;
using SalesInvoice.Domain.Enums;

namespace SalesInvoice.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductAlternative> ProductAlternatives => Set<ProductAlternative>();
    public DbSet<DiscountRule> DiscountRules => Set<DiscountRule>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<ProductRecommendation> ProductRecommendations => Set<ProductRecommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Customer
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(c => c.Name).IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.Type).HasConversion<string>();
            e.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        });

        // Product
        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.Sku).IsRequired();
            e.HasIndex(p => p.Sku).IsUnique();
            e.Property(p => p.UnitPrice).HasColumnType("numeric(12,2)");
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        });

        // ProductAlternative (composite PK)
        modelBuilder.Entity<ProductAlternative>(e =>
        {
            e.HasKey(pa => new { pa.ProductId, pa.AlternativeProductId });
            e.HasOne(pa => pa.Product)
                .WithMany(p => p.Alternatives)
                .HasForeignKey(pa => pa.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pa => pa.AlternativeProduct)
                .WithMany(p => p.AlternativeFor)
                .HasForeignKey(pa => pa.AlternativeProductId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_ProductAlternative_NotSelf",
                "\"ProductId\" <> \"AlternativeProductId\""));
        });

        // DiscountRule
        modelBuilder.Entity<DiscountRule>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(d => d.Key).IsUnique();
            e.Property(d => d.AppliesToType).HasConversion<string>().IsRequired(false);
            e.Property(d => d.Percentage).HasColumnType("numeric(5,2)");
            e.HasOne(d => d.AppliesToCustomer)
                .WithMany(c => c.DiscountRules)
                .HasForeignKey(d => d.AppliesToCustomerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Invoice
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(i => i.Subtotal).HasColumnType("numeric(12,2)");
            e.Property(i => i.DiscountPercentage).HasColumnType("numeric(5,2)");
            e.Property(i => i.DiscountAmount).HasColumnType("numeric(12,2)");
            e.Property(i => i.TaxPercentage).HasColumnType("numeric(5,2)");
            e.Property(i => i.TaxAmount).HasColumnType("numeric(12,2)");
            e.Property(i => i.Total).HasColumnType("numeric(12,2)");
            e.Property(i => i.Status).HasConversion<string>();
            e.Property(i => i.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId);
            e.HasOne(i => i.WorkflowRun)
                .WithOne(r => r.Invoice)
                .HasForeignKey<Invoice>(i => i.WorkflowRunId)
                .IsRequired(false);
        });

        // InvoiceLineItem
        modelBuilder.Entity<InvoiceLineItem>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(l => l.UnitPrice).HasColumnType("numeric(12,2)");
            e.Property(l => l.LineTotal).HasColumnType("numeric(12,2)");
            e.Property(l => l.StockStatus).HasConversion<string>();
            e.HasOne(l => l.Invoice)
                .WithMany(i => i.LineItems)
                .HasForeignKey(l => l.InvoiceId);
            e.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId);
        });

        // WorkflowRun
        modelBuilder.Entity<WorkflowRun>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(r => r.Status).HasConversion<string>();
            e.Property(r => r.StartedAt).HasDefaultValueSql("now()");
            e.HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .IsRequired(false);
        });

        // WorkflowStep
        modelBuilder.Entity<WorkflowStep>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.InputPayload).HasColumnType("jsonb");
            e.Property(s => s.OutputResult).HasColumnType("jsonb");
            e.Property(s => s.Timestamp).HasDefaultValueSql("now()");
            e.HasOne(s => s.WorkflowRun)
                .WithMany(r => r.Steps)
                .HasForeignKey(s => s.WorkflowRunId);
        });

        // ProductRecommendation
        modelBuilder.Entity<ProductRecommendation>(e =>
        {
            e.HasKey(pr => pr.Id);
            e.Property(pr => pr.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(pr => pr.WorkflowRun)
                .WithMany(r => r.Recommendations)
                .HasForeignKey(pr => pr.WorkflowRunId);
            e.HasOne(pr => pr.Product)
                .WithMany(p => p.Recommendations)
                .HasForeignKey(pr => pr.ProductId);
        });
    }
}
