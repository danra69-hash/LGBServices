using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<CustomerPackage> CustomerPackages { get; set; }
    public DbSet<AccountHolder> AccountHolders { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<JobRequest> JobRequests { get; set; }
    public DbSet<JobRequestUnit> JobRequestUnits { get; set; }
    public DbSet<JobRequestUnitAssignee> JobRequestUnitAssignees { get; set; }
    public DbSet<CompletedService> CompletedServices { get; set; }
    public DbSet<MOIForm> MOIForms { get; set; }
    public DbSet<MOAForm> MOAForms { get; set; }
    public DbSet<PackageScheduleItem> PackageScheduleItems { get; set; }
    public DbSet<DivisionGroup> DivisionGroups { get; set; }
    public DbSet<DivisionGroupRecommender> DivisionGroupRecommenders { get; set; }
    public DbSet<FormTemplate> FormTemplates { get; set; }
    public DbSet<WorkflowTemplate> WorkflowTemplates { get; set; }
    public DbSet<WorkflowStepTemplate> WorkflowStepTemplates { get; set; }
    public DbSet<WorkflowInstance> WorkflowInstances { get; set; }
    public DbSet<WorkflowStepInstance> WorkflowStepInstances { get; set; }
    public DbSet<ServiceJobForm> ServiceJobForms { get; set; }
    public DbSet<BillingParty> BillingParties { get; set; }
    public DbSet<JobItemDocument> JobItemDocuments { get; set; }
    public DbSet<SignatoryCustomerAccess> SignatoryCustomerAccess { get; set; }
    public DbSet<AppNotification> AppNotifications { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.UserId);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Name).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Mobile).HasMaxLength(20);
            entity.Property(u => u.Role).HasMaxLength(50).HasDefaultValue("User");
            entity.Property(u => u.JobTitle).HasMaxLength(100);
            entity.Property(u => u.IsVerified).HasDefaultValue(false);
            entity.Property(u => u.CanApproveMoiIntake).HasDefaultValue(false);
            entity.Property(u => u.CanApproveMoi).HasDefaultValue(false);
            entity.Property(u => u.CanApproveMoa).HasDefaultValue(false);
            entity.Property(u => u.IsInternalSignatory).HasDefaultValue(false);
            entity.HasOne(u => u.Customer)
                .WithMany()
                .HasForeignKey(u => u.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.CustomerId);
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(256);
            entity.Property(c => c.Phone).HasMaxLength(50);
            entity.Property(c => c.Company).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Status).HasMaxLength(50);
            entity.Property(c => c.DivisionGroupCode).HasMaxLength(50);
            entity.Property(c => c.MoiFormTemplateCode).HasMaxLength(50);
            entity.Property(c => c.MoaFormTemplateCode).HasMaxLength(50);
            entity.Property(c => c.MoaWorkflowTemplateCode).HasMaxLength(50);
            entity.Property(c => c.MoiApprovalMode).HasMaxLength(50).HasDefaultValue(MoiApprovalModes.AllRequired);
            entity.Property(c => c.InvoiceByPartyIdsJson).HasDefaultValue("[]");
            entity.Property(c => c.ChargeToPartyIdsJson).HasDefaultValue("[]");
            entity.HasMany(c => c.AccountHolders)
                .WithOne(h => h.Customer)
                .HasForeignKey(h => h.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(c => c.Packages)
                .WithOne(p => p.Customer)
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerPackage>(entity =>
        {
            entity.ToTable("CustomerPackages");
            entity.HasKey(p => p.CustomerPackageId);
            entity.Property(p => p.PackageName).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Validity).HasMaxLength(50);
            entity.Property(p => p.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<PackageScheduleItem>(entity =>
        {
            entity.HasKey(s => s.PackageScheduleItemId);
            entity.Property(s => s.ItemType).HasMaxLength(50);
            entity.Property(s => s.Title).HasMaxLength(200).IsRequired();
            entity.Property(s => s.Status).HasMaxLength(50);
            entity.HasOne(s => s.Customer)
                .WithMany()
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.CustomerPackage)
                .WithMany()
                .HasForeignKey(s => s.CustomerPackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountHolder>(entity =>
        {
            entity.HasKey(h => h.AccountHolderId);
            entity.Property(h => h.Name).HasMaxLength(200).IsRequired();
            entity.Property(h => h.Email).HasMaxLength(256);
            entity.Property(h => h.Phone).HasMaxLength(50);
            entity.HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SignatoryCustomerAccess>(entity =>
        {
            entity.HasKey(a => a.SignatoryCustomerAccessId);
            entity.HasIndex(a => new { a.UserId, a.CustomerId }).IsUnique();
            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Customer)
                .WithMany()
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.ProductId);
            entity.Property(p => p.PackageName).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Unit).HasMaxLength(50);
        });

        modelBuilder.Entity<JobRequest>(entity =>
        {
            entity.HasKey(j => j.JobRequestId);
            entity.Property(j => j.Customer).HasMaxLength(200);
            entity.Property(j => j.TaskType).HasMaxLength(50);
            entity.Property(j => j.Service).HasMaxLength(200);
            entity.Property(j => j.Status).HasMaxLength(50);
            entity.HasOne(j => j.CustomerRecord)
                .WithMany()
                .HasForeignKey(j => j.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(j => j.CustomerPackage)
                .WithMany()
                .HasForeignKey(j => j.CustomerPackageId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(j => j.Units)
                .WithOne(u => u.JobRequest)
                .HasForeignKey(u => u.JobRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobRequestUnit>(entity =>
        {
            entity.HasKey(u => u.JobRequestUnitId);
            entity.Property(u => u.Status).HasMaxLength(50);
            entity.Property(u => u.AssignedUserName).HasMaxLength(100);
            entity.HasIndex(u => new { u.JobRequestId, u.UnitNumber }).IsUnique();
            entity.HasMany(u => u.Assignees)
                .WithOne(a => a.Unit)
                .HasForeignKey(a => a.JobRequestUnitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobRequestUnitAssignee>(entity =>
        {
            entity.HasKey(a => a.JobRequestUnitAssigneeId);
            entity.HasIndex(a => new { a.JobRequestUnitId, a.UserId }).IsUnique();
            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompletedService>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Customer).HasMaxLength(200);
            entity.Property(s => s.Service).HasMaxLength(200);
            entity.Property(s => s.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<MOIForm>(entity =>
        {
            entity.HasKey(f => f.MOIFormId);
            entity.HasOne(f => f.JobRequest)
                .WithMany()
                .HasForeignKey(f => f.JobRequestId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(f => f.JobRequestUnit)
                .WithMany()
                .HasForeignKey(f => f.JobRequestUnitId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MOAForm>(entity =>
        {
            entity.HasKey(f => f.MOAFormId);
            entity.HasOne(f => f.JobRequest)
                .WithMany()
                .HasForeignKey(f => f.JobRequestId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(f => f.JobRequestUnit)
                .WithMany()
                .HasForeignKey(f => f.JobRequestUnitId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(f => f.MOIForm)
                .WithMany()
                .HasForeignKey(f => f.MOIFormId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DivisionGroup>(entity =>
        {
            entity.HasKey(g => g.DivisionGroupId);
            entity.HasIndex(g => g.Code).IsUnique();
            entity.Property(g => g.Code).HasMaxLength(50).IsRequired();
            entity.Property(g => g.Name).HasMaxLength(200).IsRequired();
            entity.Property(g => g.MoaWorkflowTemplateCode).HasMaxLength(50);
        });

        modelBuilder.Entity<DivisionGroupRecommender>(entity =>
        {
            entity.HasKey(r => r.DivisionGroupRecommenderId);
            entity.Property(r => r.DisplayName).HasMaxLength(200);
            entity.HasOne(r => r.DivisionGroup)
                .WithMany(g => g.Recommenders)
                .HasForeignKey(r => r.DivisionGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FormTemplate>(entity =>
        {
            entity.HasKey(t => t.FormTemplateId);
            entity.HasIndex(t => new { t.FormType, t.Code }).IsUnique();
            entity.Property(t => t.FormType).HasMaxLength(10);
            entity.Property(t => t.Code).HasMaxLength(50);
            entity.Property(t => t.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<WorkflowTemplate>(entity =>
        {
            entity.HasKey(t => t.WorkflowTemplateId);
            entity.HasIndex(t => t.Code).IsUnique();
            entity.Property(t => t.Code).HasMaxLength(50);
            entity.Property(t => t.WorkflowType).HasMaxLength(10);
        });

        modelBuilder.Entity<WorkflowStepTemplate>(entity =>
        {
            entity.HasKey(s => s.WorkflowStepTemplateId);
            entity.Property(s => s.StepKey).HasMaxLength(50);
            entity.Property(s => s.DisplayName).HasMaxLength(200);
            entity.Property(s => s.ConditionType).HasMaxLength(50);
            entity.Property(s => s.AssigneeType).HasMaxLength(50);
            entity.HasOne(s => s.WorkflowTemplate)
                .WithMany(t => t.Steps)
                .HasForeignKey(s => s.WorkflowTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.HasKey(i => i.WorkflowInstanceId);
            entity.Property(i => i.FormType).HasMaxLength(10);
            entity.Property(i => i.Status).HasMaxLength(50);
            entity.HasOne(i => i.WorkflowTemplate)
                .WithMany()
                .HasForeignKey(i => i.WorkflowTemplateId);
            entity.HasOne(i => i.MoiForm)
                .WithMany()
                .HasForeignKey(i => i.MoiFormId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.MoaForm)
                .WithMany()
                .HasForeignKey(i => i.MoaFormId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceJobForm>(entity =>
        {
            entity.HasKey(f => f.ServiceJobFormId);
            entity.HasIndex(f => f.JobRequestId).IsUnique();
            entity.Property(f => f.Status).HasMaxLength(50);
            entity.HasOne(f => f.JobRequest)
                .WithMany()
                .HasForeignKey(f => f.JobRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobItemDocument>(entity =>
        {
            entity.HasKey(d => d.JobItemDocumentId);
            entity.Property(d => d.Folder).HasMaxLength(50);
            entity.Property(d => d.FileName).HasMaxLength(500);
            entity.Property(d => d.StorageKey).HasMaxLength(500);
            entity.Property(d => d.ContentType).HasMaxLength(200);
            entity.Property(d => d.UploadedByName).HasMaxLength(200);
            entity.HasOne(d => d.JobRequest)
                .WithMany()
                .HasForeignKey(d => d.JobRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowStepInstance>(entity =>
        {
            entity.HasKey(s => s.WorkflowStepInstanceId);
            entity.Property(s => s.StepKey).HasMaxLength(50);
            entity.Property(s => s.DisplayName).HasMaxLength(200);
            entity.Property(s => s.Status).HasMaxLength(50);
            entity.HasOne(s => s.WorkflowInstance)
                .WithMany(i => i.Steps)
                .HasForeignKey(s => s.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppNotification>(entity =>
        {
            entity.HasKey(n => n.AppNotificationId);
            entity.Property(n => n.EventType).HasMaxLength(50);
            entity.Property(n => n.Title).HasMaxLength(200);
            entity.HasIndex(n => new { n.UserId, n.IsRead });
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(i => i.InvoiceId);
            entity.Property(i => i.InvoiceNumber).HasMaxLength(50);
            entity.Property(i => i.Currency).HasMaxLength(10);
            entity.Property(i => i.Status).HasMaxLength(50);
            entity.HasIndex(i => i.InvoiceNumber).IsUnique();
        });
    }
}
