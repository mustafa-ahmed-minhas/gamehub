using GameHub.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
        public DbSet<Sport> Sports => Set<Sport>();
        public DbSet<Facility> Facilities => Set<Facility>();
        public DbSet<Court> Courts => Set<Court>();
        public DbSet<CourtPricing> CourtPricings => Set<CourtPricing>();
        public DbSet<CourtSchedule> CourtSchedules => Set<CourtSchedule>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
        public DbSet<CustomerMembership> CustomerMemberships => Set<CustomerMembership>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
        public DbSet<BookingInvoice> BookingInvoices => Set<BookingInvoice>();
        public DbSet<MembershipInvoice> MembershipInvoices => Set<MembershipInvoice>();
        public DbSet<PaymentReceipt> PaymentReceipts => Set<PaymentReceipt>();
        public DbSet<CustomerNotification> CustomerNotifications => Set<CustomerNotification>();
        public DbSet<BookingReminder> BookingReminders => Set<BookingReminder>();
        public DbSet<CustomerBookingRequest> CustomerBookingRequests => Set<CustomerBookingRequest>();
        public DbSet<Lead> Leads => Set<Lead>();
        public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();
        public DbSet<Opportunity> Opportunities => Set<Opportunity>();
        public DbSet<OpportunityActivity> OpportunityActivities => Set<OpportunityActivity>();
        public DbSet<SalesQuotation> SalesQuotations => Set<SalesQuotation>();
        public DbSet<SalesQuotationItem> SalesQuotationItems => Set<SalesQuotationItem>();
        public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
        public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
        public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
        public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
        public DbSet<DocumentApproval> DocumentApprovals => Set<DocumentApproval>();
        public DbSet<DocumentRevision> DocumentRevisions => Set<DocumentRevision>();
        public DbSet<DocumentActivity> DocumentActivities => Set<DocumentActivity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(user => user.Email).IsUnique();
                entity.HasIndex(user => user.PhoneNumber)
                    .IsUnique()
                    .HasFilter("[PhoneNumber] IS NOT NULL");

                entity.Property(user => user.FirstName)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(user => user.LastName)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(user => user.Email)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(user => user.PhoneNumber)
                    .HasMaxLength(20);

                entity.Property(user => user.PasswordHash)
                    .IsRequired();

                entity.Property(user => user.Role)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(user => user.IsActive)
                    .HasDefaultValue(true);

                entity.Property(user => user.ProfileImageUrl)
                    .HasMaxLength(300);

                entity.Property(user => user.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<SystemSettings>(entity =>
            {
                entity.Property(settings => settings.ArenaName)
                    .IsRequired()
                    .HasMaxLength(120);

                entity.Property(settings => settings.Phone)
                    .IsRequired()
                    .HasMaxLength(30);

                entity.Property(settings => settings.Email)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(settings => settings.Address)
                    .HasMaxLength(250);

                entity.Property(settings => settings.Logo)
                    .HasMaxLength(300);

                entity.Property(settings => settings.Currency)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(settings => settings.CurrencySymbol)
                    .HasMaxLength(8);

                entity.Property(settings => settings.TaxPercentage)
                    .HasPrecision(5, 2);

                entity.Property(settings => settings.BookingPrefix)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(settings => settings.InvoicePrefix)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(settings => settings.ReceiptPrefix)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(settings => settings.LeadPrefix)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("LEAD");

                entity.Property(settings => settings.OpportunityPrefix)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("OPP");

                entity.Property(settings => settings.QuotationPrefix)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("SQ");

                entity.Property(settings => settings.SalesOrderPrefix)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("SO");

                entity.Property(settings => settings.DefaultQuotationValidityDays)
                    .HasDefaultValue(7);

                entity.Property(settings => settings.Version)
                    .HasMaxLength(30);
                entity.Property(settings => settings.EnableCustomerBookingReminders).HasDefaultValue(true);
                entity.Property(settings => settings.FirstReminderHoursBefore).HasDefaultValue(24);
                entity.Property(settings => settings.SecondReminderHoursBefore).HasDefaultValue(3);
                entity.Property(settings => settings.AllowCustomerCancellation).HasDefaultValue(true);
                entity.Property(settings => settings.CancellationCutoffHours).HasDefaultValue(6);
                entity.Property(settings => settings.AllowCustomerRescheduling).HasDefaultValue(true);
                entity.Property(settings => settings.RescheduleCutoffHours).HasDefaultValue(6);
            });

            modelBuilder.Entity<Sport>(entity =>
            {
                entity.HasIndex(sport => sport.Code).IsUnique();
                entity.Property(sport => sport.Name).IsRequired().HasMaxLength(80);
                entity.Property(sport => sport.Code).IsRequired().HasMaxLength(20);
                entity.Property(sport => sport.Description).HasMaxLength(300);
                entity.Property(sport => sport.IconClass).HasMaxLength(80);
                entity.Property(sport => sport.ImageUrl).HasMaxLength(300);
                entity.Property(sport => sport.AccentColor).HasMaxLength(20);
            });

            modelBuilder.Entity<Facility>(entity =>
            {
                entity.HasIndex(facility => facility.Code).IsUnique();
                entity.Property(facility => facility.Name).IsRequired().HasMaxLength(100);
                entity.Property(facility => facility.Code).IsRequired().HasMaxLength(20);
                entity.Property(facility => facility.Description).HasMaxLength(300);
                entity.Property(facility => facility.Type).HasConversion<string>().HasMaxLength(20);
                entity.Property(facility => facility.LocationLabel).HasMaxLength(150);
            });

            modelBuilder.Entity<Court>(entity =>
            {
                entity.HasIndex(court => court.Code).IsUnique();
                entity.Property(court => court.Name).IsRequired().HasMaxLength(100);
                entity.Property(court => court.Code).IsRequired().HasMaxLength(30);
                entity.Property(court => court.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(court => court.Description).HasMaxLength(300);
                entity.Property(court => court.ImageUrl).HasMaxLength(300);
                entity.Property(court => court.Notes).HasMaxLength(500);
                entity.HasOne(court => court.Sport)
                    .WithMany(sport => sport.Courts)
                    .HasForeignKey(court => court.SportId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(court => court.Facility)
                    .WithMany(facility => facility.Courts)
                    .HasForeignKey(court => court.FacilityId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CourtPricing>(entity =>
            {
                entity.Property(pricing => pricing.Name).IsRequired().HasMaxLength(100);
                entity.Property(pricing => pricing.Price).HasPrecision(10, 2);
                entity.HasIndex(pricing => new { pricing.CourtId, pricing.DayOfWeek, pricing.StartTime, pricing.EndTime, pricing.DurationMinutes })
                    .IsUnique();
                entity.HasOne(pricing => pricing.Court)
                    .WithMany(court => court.Pricings)
                    .HasForeignKey(pricing => pricing.CourtId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CourtSchedule>(entity =>
            {
                entity.HasIndex(schedule => new { schedule.CourtId, schedule.DayOfWeek, schedule.IsActive })
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
                entity.HasOne(schedule => schedule.Court)
                    .WithMany(court => court.Schedules)
                    .HasForeignKey(schedule => schedule.CourtId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasIndex(customer => customer.CustomerCode).IsUnique();
                entity.HasIndex(customer => customer.Email)
                    .IsUnique()
                    .HasFilter("[Email] IS NOT NULL");
                entity.HasIndex(customer => customer.PrimaryPhone);
                entity.HasIndex(customer => customer.WhatsAppNumber);
                entity.HasIndex(customer => customer.NationalIdNumber)
                    .IsUnique()
                    .HasFilter("[NationalIdNumber] IS NOT NULL");

                entity.Property(customer => customer.CustomerCode).IsRequired().HasMaxLength(20);
                entity.Property(customer => customer.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(customer => customer.LastName).IsRequired().HasMaxLength(50);
                entity.Property(customer => customer.PreferredName).HasMaxLength(50);
                entity.Property(customer => customer.ProfileImageUrl).HasMaxLength(300);
                entity.Property(customer => customer.NationalIdNumber).HasMaxLength(30);
                entity.Property(customer => customer.Gender).HasConversion<string>().HasMaxLength(30);
                entity.Property(customer => customer.PrimaryPhone).IsRequired().HasMaxLength(20);
                entity.Property(customer => customer.SecondaryPhone).HasMaxLength(20);
                entity.Property(customer => customer.WhatsAppNumber).HasMaxLength(20);
                entity.Property(customer => customer.Email).HasMaxLength(150);
                entity.Property(customer => customer.PasswordHash).HasMaxLength(500);
                entity.Property(customer => customer.HasOnlineAccount).HasDefaultValue(false);
                entity.Property(customer => customer.EmailVerified).HasDefaultValue(false);
                entity.Property(customer => customer.EmailVerificationToken).HasMaxLength(500);
                entity.Property(customer => customer.PasswordResetToken).HasMaxLength(500);
                entity.Property(customer => customer.FailedLoginAttempts).HasDefaultValue(0);
                entity.Property(customer => customer.AddressLine1).HasMaxLength(200);
                entity.Property(customer => customer.AddressLine2).HasMaxLength(200);
                entity.Property(customer => customer.City).HasMaxLength(80);
                entity.Property(customer => customer.StateProvince).HasMaxLength(80);
                entity.Property(customer => customer.PostalCode).HasMaxLength(20);
                entity.Property(customer => customer.Country).IsRequired().HasMaxLength(80);
                entity.Property(customer => customer.EmergencyContactName).HasMaxLength(100);
                entity.Property(customer => customer.EmergencyContactPhone).HasMaxLength(20);
                entity.Property(customer => customer.EmergencyContactRelation).HasMaxLength(50);
                entity.Property(customer => customer.CustomerType).HasConversion<string>().HasMaxLength(30);
                entity.Property(customer => customer.CustomerSource).HasConversion<string>().HasMaxLength(30);
                entity.Property(customer => customer.OrganizationName).HasMaxLength(150);
                entity.Property(customer => customer.Occupation).HasMaxLength(100);
                entity.Property(customer => customer.PreferredSportCodes).HasMaxLength(300);
                entity.Property(customer => customer.PreferredContactMethod).HasConversion<string>().HasMaxLength(30);
                entity.Property(customer => customer.MembershipNumber).HasMaxLength(50);
                entity.Property(customer => customer.CreditLimit).HasPrecision(12, 2);
                entity.Property(customer => customer.OutstandingBalance).HasPrecision(12, 2).HasDefaultValue(0);
                entity.Property(customer => customer.BlacklistReason).HasMaxLength(500);
                entity.Property(customer => customer.InternalNotes).HasMaxLength(2000);
                entity.Property(customer => customer.CustomerTags).HasMaxLength(500);
                entity.Property(customer => customer.IsActive).HasDefaultValue(true);
                entity.Property(customer => customer.Country).HasDefaultValue("Pakistan");
                entity.Property(customer => customer.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<MembershipPlan>(entity =>
            {
                entity.HasIndex(plan => plan.Code).IsUnique();
                entity.Property(plan => plan.Name).IsRequired().HasMaxLength(100);
                entity.Property(plan => plan.Code).IsRequired().HasMaxLength(20);
                entity.Property(plan => plan.Description).HasMaxLength(500);
                entity.Property(plan => plan.PlanType).HasConversion<string>().HasMaxLength(30);
                entity.Property(plan => plan.JoiningFee).HasPrecision(12, 2);
                entity.Property(plan => plan.RenewalFee).HasPrecision(12, 2);
                entity.Property(plan => plan.DiscountPercentage).HasPrecision(5, 2);
                entity.Property(plan => plan.AllowedSportCodes).HasMaxLength(300);
                entity.Property(plan => plan.Benefits).HasMaxLength(1000);
                entity.Property(plan => plan.IsActive).HasDefaultValue(true);
                entity.Property(plan => plan.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<CustomerMembership>(entity =>
            {
                entity.HasIndex(membership => membership.MembershipNumber).IsUnique();
                entity.HasIndex(membership => membership.CustomerId);
                entity.HasIndex(membership => membership.MembershipPlanId);
                entity.HasIndex(membership => membership.Status);
                entity.HasIndex(membership => membership.ExpiryDate);
                entity.HasIndex(membership => membership.SupersededByMembershipId);
                entity.HasIndex(membership => membership.PaymentTransactionId)
                    .IsUnique()
                    .HasFilter("[PaymentTransactionId] IS NOT NULL");
                entity.Property(membership => membership.MembershipNumber).IsRequired().HasMaxLength(30);
                entity.Property(membership => membership.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(membership => membership.JoiningFee).HasPrecision(12, 2);
                entity.Property(membership => membership.RenewalFee).HasPrecision(12, 2);
                entity.Property(membership => membership.DiscountPercentage).HasPrecision(5, 2);
                entity.Property(membership => membership.CancellationReason).HasMaxLength(500);
                entity.Property(membership => membership.Notes).HasMaxLength(1000);
                entity.Property(membership => membership.IsActive).HasDefaultValue(true);
                entity.Property(membership => membership.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(membership => membership.Customer)
                    .WithMany()
                    .HasForeignKey(membership => membership.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(membership => membership.MembershipPlan)
                    .WithMany(plan => plan.CustomerMemberships)
                    .HasForeignKey(membership => membership.MembershipPlanId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(membership => membership.PaymentTransaction)
                    .WithMany()
                    .HasForeignKey(membership => membership.PaymentTransactionId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(membership => membership.SupersededByMembership)
                    .WithMany()
                    .HasForeignKey(membership => membership.SupersededByMembershipId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasIndex(booking => booking.BookingNumber).IsUnique();
                entity.HasIndex(booking => booking.CustomerId);
                entity.HasIndex(booking => booking.CourtId);
                entity.HasIndex(booking => booking.BookingDate);
                entity.HasIndex(booking => booking.Status);
                entity.HasIndex(booking => booking.PaymentStatus);
                entity.HasIndex(booking => new { booking.CourtId, booking.BookingDate, booking.StartTime, booking.EndTime });
                entity.HasIndex(booking => new { booking.CourtId, booking.BookingDate, booking.IsActive, booking.Status, booking.StartTime, booking.EndTime })
                    .HasFilter("[IsActive] = 1");

                entity.Property(booking => booking.BookingNumber).IsRequired().HasMaxLength(30);
                entity.Property(booking => booking.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(booking => booking.PaymentStatus).HasConversion<string>().HasMaxLength(30);
                entity.Property(booking => booking.Source).HasConversion<string>().HasMaxLength(30);
                entity.Property(booking => booking.BaseAmount).HasPrecision(12, 2);
                entity.Property(booking => booking.MembershipDiscountAmount).HasPrecision(12, 2);
                entity.Property(booking => booking.ManualDiscountAmount).HasPrecision(12, 2);
                entity.Property(booking => booking.TaxAmount).HasPrecision(12, 2);
                entity.Property(booking => booking.TotalAmount).HasPrecision(12, 2);
                entity.Property(booking => booking.PaidAmount).HasPrecision(12, 2);
                entity.Property(booking => booking.BalanceAmount).HasPrecision(12, 2);
                entity.HasIndex(booking => booking.PaymentTransactionId)
                    .IsUnique()
                    .HasFilter("[PaymentTransactionId] IS NOT NULL");
                entity.Property(booking => booking.WebsiteCheckoutToken).HasMaxLength(80);
                entity.Property(booking => booking.PaymentReference).HasMaxLength(100);
                entity.Property(booking => booking.CustomerNotes).HasMaxLength(1000);
                entity.Property(booking => booking.InternalNotes).HasMaxLength(1500);
                entity.Property(booking => booking.CancellationReason).HasMaxLength(500);
                entity.Property(booking => booking.RescheduleReason).HasMaxLength(500);
                entity.Property(booking => booking.SpecialRequest).HasMaxLength(500);
                entity.Property(booking => booking.ReferenceNumber).HasMaxLength(50);
                entity.Property(booking => booking.IsActive).HasDefaultValue(true);
                entity.Property(booking => booking.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(booking => booking.Customer)
                    .WithMany()
                    .HasForeignKey(booking => booking.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(booking => booking.Sport)
                    .WithMany()
                    .HasForeignKey(booking => booking.SportId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(booking => booking.Facility)
                    .WithMany()
                    .HasForeignKey(booking => booking.FacilityId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(booking => booking.Court)
                    .WithMany()
                    .HasForeignKey(booking => booking.CourtId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(booking => booking.CustomerMembership)
                    .WithMany()
                    .HasForeignKey(booking => booking.CustomerMembershipId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(booking => booking.PaymentTransaction)
                    .WithOne(payment => payment.Booking)
                    .HasForeignKey<Booking>(booking => booking.PaymentTransactionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.HasIndex(payment => payment.TransactionNumber).IsUnique();
                entity.HasIndex(payment => payment.GatewayTransactionId).IsUnique();
                entity.HasIndex(payment => payment.IdempotencyKey)
                    .IsUnique()
                    .HasFilter("[IdempotencyKey] IS NOT NULL");
                entity.Property(payment => payment.TransactionNumber).IsRequired().HasMaxLength(30);
                entity.Property(payment => payment.CheckoutToken).IsRequired().HasMaxLength(80);
                entity.Property(payment => payment.Gateway).HasConversion<string>().HasMaxLength(30);
                entity.Property(payment => payment.PaymentMethod).HasConversion<string>().HasMaxLength(30);
                entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(payment => payment.GatewayTransactionId).IsRequired().HasMaxLength(100);
                entity.Property(payment => payment.GatewayReference).HasMaxLength(100);
                entity.Property(payment => payment.Amount).HasPrecision(12, 2);
                entity.Property(payment => payment.Currency).IsRequired().HasMaxLength(20);
                entity.Property(payment => payment.CurrencySymbol).HasMaxLength(8);
                entity.Property(payment => payment.FailureReason).HasMaxLength(300);
                entity.Property(payment => payment.CustomerSafeMessage).HasMaxLength(300);
                entity.Property(payment => payment.IdempotencyKey).HasMaxLength(120);
                entity.Property(payment => payment.ClientIpHash).HasMaxLength(128);
                entity.Property(payment => payment.UserAgentSummary).HasMaxLength(250);
                entity.Property(payment => payment.CardBrand).HasMaxLength(30);
                entity.Property(payment => payment.CardLastFour).HasMaxLength(4);
                entity.HasOne(payment => payment.Customer)
                    .WithMany()
                    .HasForeignKey(payment => payment.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BookingInvoice>(entity =>
            {
                entity.HasIndex(invoice => invoice.InvoiceNumber).IsUnique();
                entity.HasIndex(invoice => invoice.BookingId).IsUnique();
                entity.Property(invoice => invoice.InvoiceNumber).IsRequired().HasMaxLength(30);
                entity.Property(invoice => invoice.Subtotal).HasPrecision(12, 2);
                entity.Property(invoice => invoice.DiscountAmount).HasPrecision(12, 2);
                entity.Property(invoice => invoice.TaxAmount).HasPrecision(12, 2);
                entity.Property(invoice => invoice.TotalAmount).HasPrecision(12, 2);
                entity.Property(invoice => invoice.PaidAmount).HasPrecision(12, 2);
                entity.Property(invoice => invoice.BalanceAmount).HasPrecision(12, 2);
                entity.HasOne(invoice => invoice.Booking).WithMany().HasForeignKey(invoice => invoice.BookingId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(invoice => invoice.Customer).WithMany().HasForeignKey(invoice => invoice.CustomerId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MembershipInvoice>(entity =>
            {
                entity.HasIndex(invoice => invoice.InvoiceNumber).IsUnique();
                entity.HasIndex(invoice => invoice.CustomerMembershipId).IsUnique();
                entity.HasIndex(invoice => invoice.CheckoutToken).IsUnique();
                entity.HasIndex(invoice => invoice.CustomerId);
                entity.Property(invoice => invoice.InvoiceNumber).IsRequired().HasMaxLength(30);
                entity.Property(invoice => invoice.CheckoutToken).IsRequired().HasMaxLength(80);
                entity.Property(invoice => invoice.PlanName).IsRequired().HasMaxLength(100);
                entity.Property(invoice => invoice.PlanCode).IsRequired().HasMaxLength(20);
                entity.Property(invoice => invoice.PlanDescription).HasMaxLength(80);
                entity.Property(invoice => invoice.Operation).HasMaxLength(20);
                entity.Property(invoice => invoice.PreviousPlanName).HasMaxLength(100);
                entity.Property(invoice => invoice.PreviousPlanCode).HasMaxLength(20);
                entity.Property(invoice => invoice.Currency).IsRequired().HasMaxLength(20);
                entity.Property(invoice => invoice.CurrencySymbol).HasMaxLength(8);
                entity.Property(invoice => invoice.ArenaName).IsRequired().HasMaxLength(120);
                entity.Property(invoice => invoice.ArenaPhone).HasMaxLength(30);
                entity.Property(invoice => invoice.ArenaEmail).HasMaxLength(150);
                entity.Property(invoice => invoice.ArenaAddress).HasMaxLength(250);
                entity.Property(invoice => invoice.CustomerName).IsRequired().HasMaxLength(120);
                entity.Property(invoice => invoice.CustomerEmail).HasMaxLength(150);
                entity.Property(invoice => invoice.CustomerPhone).HasMaxLength(20);
                entity.Property(invoice => invoice.MembershipNumber).IsRequired().HasMaxLength(30);
                entity.Property(invoice => invoice.Subtotal).HasPrecision(12, 2);
                entity.Property(invoice => invoice.TaxAmount).HasPrecision(12, 2);
                entity.Property(invoice => invoice.TotalAmount).HasPrecision(12, 2);
                entity.Property(invoice => invoice.PaidAmount).HasPrecision(12, 2);
                entity.Property(invoice => invoice.BalanceAmount).HasPrecision(12, 2);
                entity.HasOne(invoice => invoice.CustomerMembership).WithMany().HasForeignKey(invoice => invoice.CustomerMembershipId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(invoice => invoice.Customer).WithMany().HasForeignKey(invoice => invoice.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(invoice => invoice.PaymentTransaction).WithMany().HasForeignKey(invoice => invoice.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PaymentReceipt>(entity =>
            {
                entity.HasIndex(receipt => receipt.ReceiptNumber).IsUnique();
                entity.HasIndex(receipt => receipt.PaymentTransactionId).IsUnique();
                entity.Property(receipt => receipt.ReceiptNumber).IsRequired().HasMaxLength(30);
                entity.Property(receipt => receipt.PaymentMethod).HasConversion<string>().HasMaxLength(30);
                entity.Property(receipt => receipt.AmountPaid).HasPrecision(12, 2);
                entity.HasOne(receipt => receipt.PaymentTransaction).WithMany().HasForeignKey(receipt => receipt.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(receipt => receipt.Booking).WithMany().HasForeignKey(receipt => receipt.BookingId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(receipt => receipt.Customer).WithMany().HasForeignKey(receipt => receipt.CustomerId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasIndex(payment => payment.PaymentNumber).IsUnique();
                entity.HasIndex(payment => new { payment.InvoiceId, payment.PaymentDate });
                entity.Property(payment => payment.PaymentNumber).IsRequired().HasMaxLength(30);
                entity.Property(payment => payment.Amount).HasPrecision(12, 2);
                entity.Property(payment => payment.PaymentMethod).HasConversion<string>().HasMaxLength(30);
                entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(payment => payment.Currency).IsRequired().HasMaxLength(20);
                entity.Property(payment => payment.CurrencySymbol).HasMaxLength(8);
                entity.Property(payment => payment.ReferenceNumber).HasMaxLength(120);
                entity.Property(payment => payment.Notes).HasMaxLength(1500);
                entity.HasOne(payment => payment.Invoice).WithMany().HasForeignKey(payment => payment.InvoiceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(payment => payment.Customer).WithMany().HasForeignKey(payment => payment.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(payment => payment.ReceivedByUser).WithMany().HasForeignKey(payment => payment.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(payment => payment.CreatedByUser).WithMany().HasForeignKey(payment => payment.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PaymentAllocation>(entity =>
            {
                entity.HasIndex(allocation => new { allocation.PaymentId, allocation.InvoiceId }).IsUnique();
                entity.Property(allocation => allocation.Amount).HasPrecision(12, 2);
                entity.HasOne(allocation => allocation.Payment).WithMany(payment => payment.Allocations).HasForeignKey(allocation => allocation.PaymentId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(allocation => allocation.Invoice).WithMany(invoice => invoice.PaymentAllocations).HasForeignKey(allocation => allocation.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CustomerNotification>(entity =>
            {
                entity.HasIndex(notification => new { notification.CustomerId, notification.IsRead });
                entity.Property(notification => notification.Type).HasConversion<string>().HasMaxLength(40);
                entity.Property(notification => notification.Title).IsRequired().HasMaxLength(120);
                entity.Property(notification => notification.Message).IsRequired().HasMaxLength(500);
                entity.Property(notification => notification.ActionUrl).HasMaxLength(300);
                entity.HasOne(notification => notification.Customer).WithMany().HasForeignKey(notification => notification.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(notification => notification.Booking).WithMany().HasForeignKey(notification => notification.BookingId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<BookingReminder>(entity =>
            {
                entity.HasIndex(reminder => new { reminder.BookingId, reminder.ReminderType }).IsUnique();
                entity.Property(reminder => reminder.ReminderType).HasConversion<string>().HasMaxLength(40);
                entity.HasOne(reminder => reminder.Booking).WithMany().HasForeignKey(reminder => reminder.BookingId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(reminder => reminder.Customer).WithMany().HasForeignKey(reminder => reminder.CustomerId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CustomerBookingRequest>(entity =>
            {
                entity.HasIndex(request => new { request.BookingId, request.RequestType, request.Status });
                entity.Property(request => request.RequestType).HasConversion<string>().HasMaxLength(30);
                entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(request => request.Reason).IsRequired().HasMaxLength(500);
                entity.Property(request => request.AdminResponse).HasMaxLength(500);
                entity.HasOne(request => request.Booking).WithMany().HasForeignKey(request => request.BookingId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(request => request.Customer).WithMany().HasForeignKey(request => request.CustomerId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Lead>(entity =>
            {
                entity.HasIndex(lead => lead.LeadNumber).IsUnique();
                entity.HasIndex(lead => lead.Status);
                entity.HasIndex(lead => lead.Source);
                entity.HasIndex(lead => lead.AssignedToUserId);
                entity.HasIndex(lead => lead.NextFollowUpAt);
                entity.HasIndex(lead => lead.CustomerId);
                entity.Property(lead => lead.LeadNumber).IsRequired().HasMaxLength(30);
                entity.Property(lead => lead.CustomerName).IsRequired().HasMaxLength(120);
                entity.Property(lead => lead.Email).HasMaxLength(150);
                entity.Property(lead => lead.Phone).HasMaxLength(20);
                entity.Property(lead => lead.WhatsAppNumber).HasMaxLength(20);
                entity.Property(lead => lead.OrganizationName).HasMaxLength(150);
                entity.Property(lead => lead.Source).HasConversion<string>().HasMaxLength(30);
                entity.Property(lead => lead.ServiceInterest).HasConversion<string>().HasMaxLength(40);
                entity.Property(lead => lead.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(lead => lead.Priority).HasConversion<string>().HasMaxLength(20);
                entity.Property(lead => lead.Temperature).HasConversion<string>().HasMaxLength(20);
                entity.Property(lead => lead.InquirySubject).HasMaxLength(150);
                entity.Property(lead => lead.InquiryDetails).HasMaxLength(2000);
                entity.Property(lead => lead.QualificationNotes).HasMaxLength(1500);
                entity.Property(lead => lead.DisqualificationReason).HasMaxLength(500);
                entity.Property(lead => lead.InternalNotes).HasMaxLength(2000);
                entity.Property(lead => lead.Tags).HasMaxLength(500);
                entity.Property(lead => lead.PublicReferenceNumber).HasMaxLength(40);
                entity.Property(lead => lead.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(lead => lead.IsActive).HasDefaultValue(true);
                entity.HasOne(lead => lead.Customer).WithMany().HasForeignKey(lead => lead.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(lead => lead.SubmittedByCustomer).WithMany().HasForeignKey(lead => lead.SubmittedByCustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(lead => lead.Sport).WithMany().HasForeignKey(lead => lead.SportId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(lead => lead.Facility).WithMany().HasForeignKey(lead => lead.FacilityId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(lead => lead.Court).WithMany().HasForeignKey(lead => lead.CourtId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(lead => lead.MembershipPlan).WithMany().HasForeignKey(lead => lead.MembershipPlanId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(lead => lead.AssignedToUser).WithMany().HasForeignKey(lead => lead.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LeadActivity>(entity =>
            {
                entity.HasIndex(activity => new { activity.LeadId, activity.FollowUpDueAt });
                entity.Property(activity => activity.ActivityType).HasConversion<string>().HasMaxLength(30);
                entity.Property(activity => activity.Subject).IsRequired().HasMaxLength(150);
                entity.Property(activity => activity.Description).HasMaxLength(1500);
                entity.Property(activity => activity.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(activity => activity.Lead).WithMany(lead => lead.Activities).HasForeignKey(activity => activity.LeadId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(activity => activity.CreatedByUser).WithMany().HasForeignKey(activity => activity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Opportunity>(entity =>
            {
                entity.HasIndex(opportunity => opportunity.OpportunityNumber).IsUnique();
                entity.HasIndex(opportunity => opportunity.LeadId).IsUnique();
                entity.HasIndex(opportunity => opportunity.Stage);
                entity.HasIndex(opportunity => opportunity.AssignedToUserId);
                entity.HasIndex(opportunity => opportunity.ExpectedCloseDate);
                entity.Property(opportunity => opportunity.OpportunityNumber).IsRequired().HasMaxLength(30);
                entity.Property(opportunity => opportunity.Name).IsRequired().HasMaxLength(150);
                entity.Property(opportunity => opportunity.Description).HasMaxLength(1000);
                entity.Property(opportunity => opportunity.Type).HasConversion<string>().HasMaxLength(30);
                entity.Property(opportunity => opportunity.Stage).HasConversion<string>().HasMaxLength(30);
                entity.Property(opportunity => opportunity.ExpectedValue).HasPrecision(12, 2);
                entity.Property(opportunity => opportunity.CustomerRequirement).HasMaxLength(1500);
                entity.Property(opportunity => opportunity.ProposedSolution).HasMaxLength(1500);
                entity.Property(opportunity => opportunity.CompetitorInformation).HasMaxLength(1000);
                entity.Property(opportunity => opportunity.LossReason).HasMaxLength(500);
                entity.Property(opportunity => opportunity.InternalNotes).HasMaxLength(2000);
                entity.Property(opportunity => opportunity.Tags).HasMaxLength(500);
                entity.Property(opportunity => opportunity.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(opportunity => opportunity.IsActive).HasDefaultValue(true);
                entity.HasOne(opportunity => opportunity.Lead).WithOne(lead => lead.ConvertedOpportunity).HasForeignKey<Opportunity>(opportunity => opportunity.LeadId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(opportunity => opportunity.Customer).WithMany().HasForeignKey(opportunity => opportunity.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(opportunity => opportunity.Sport).WithMany().HasForeignKey(opportunity => opportunity.SportId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(opportunity => opportunity.Facility).WithMany().HasForeignKey(opportunity => opportunity.FacilityId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(opportunity => opportunity.Court).WithMany().HasForeignKey(opportunity => opportunity.CourtId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(opportunity => opportunity.MembershipPlan).WithMany().HasForeignKey(opportunity => opportunity.MembershipPlanId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(opportunity => opportunity.AssignedToUser).WithMany().HasForeignKey(opportunity => opportunity.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OpportunityActivity>(entity =>
            {
                entity.HasIndex(activity => new { activity.OpportunityId, activity.FollowUpDueAt });
                entity.Property(activity => activity.ActivityType).HasConversion<string>().HasMaxLength(30);
                entity.Property(activity => activity.Subject).IsRequired().HasMaxLength(150);
                entity.Property(activity => activity.Description).HasMaxLength(1500);
                entity.Property(activity => activity.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(activity => activity.Opportunity).WithMany(opportunity => opportunity.Activities).HasForeignKey(activity => activity.OpportunityId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(activity => activity.CreatedByUser).WithMany().HasForeignKey(activity => activity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SalesQuotation>(entity =>
            {
                entity.HasIndex(quotation => quotation.QuotationNumber).IsUnique();
                entity.HasIndex(quotation => quotation.OpportunityId);
                entity.HasIndex(quotation => quotation.CustomerId);
                entity.HasIndex(quotation => quotation.Status);
                entity.HasIndex(quotation => quotation.QuotationDate);
                entity.HasIndex(quotation => quotation.ValidUntil);
                entity.Property(quotation => quotation.QuotationNumber).IsRequired().HasMaxLength(30);
                entity.Property(quotation => quotation.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(quotation => quotation.Subject).HasMaxLength(180);
                entity.Property(quotation => quotation.Introduction).HasMaxLength(1500);
                entity.Property(quotation => quotation.TermsAndConditions).HasMaxLength(2000);
                entity.Property(quotation => quotation.CustomerNotes).HasMaxLength(1500);
                entity.Property(quotation => quotation.InternalNotes).HasMaxLength(2000);
                entity.Property(quotation => quotation.CustomerReference).HasMaxLength(80);
                entity.Property(quotation => quotation.Subtotal).HasPrecision(12, 2);
                entity.Property(quotation => quotation.LineDiscountTotal).HasPrecision(12, 2);
                entity.Property(quotation => quotation.ManualDiscountAmount).HasPrecision(12, 2);
                entity.Property(quotation => quotation.DiscountTotal).HasPrecision(12, 2);
                entity.Property(quotation => quotation.TaxTotal).HasPrecision(12, 2);
                entity.Property(quotation => quotation.GrandTotal).HasPrecision(12, 2);
                entity.Property(quotation => quotation.Currency).IsRequired().HasMaxLength(20);
                entity.Property(quotation => quotation.CurrencySymbol).HasMaxLength(8);
                entity.Property(quotation => quotation.RejectionReason).HasMaxLength(500);
                entity.Property(quotation => quotation.CancellationReason).HasMaxLength(500);
                entity.Property(quotation => quotation.ApprovalStatus).HasConversion<string>().HasMaxLength(30);
                entity.Property(quotation => quotation.LastRevisionReason).HasMaxLength(1000);
                entity.Property(quotation => quotation.RowVersion).IsRowVersion();
                entity.Property(quotation => quotation.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(quotation => quotation.IsActive).HasDefaultValue(true);
                entity.HasOne(quotation => quotation.Opportunity).WithMany().HasForeignKey(quotation => quotation.OpportunityId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(quotation => quotation.Customer).WithMany().HasForeignKey(quotation => quotation.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(quotation => quotation.AssignedToUser).WithMany().HasForeignKey(quotation => quotation.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
                entity.HasMany(quotation => quotation.Items).WithOne(item => item.SalesQuotation).HasForeignKey(item => item.SalesQuotationId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalesQuotationItem>(entity =>
            {
                entity.HasIndex(item => item.SalesQuotationId);
                entity.Property(item => item.ItemType).HasConversion<string>().HasMaxLength(30);
                entity.Property(item => item.Description).IsRequired().HasMaxLength(300);
                entity.Property(item => item.UnitOfMeasure).IsRequired().HasMaxLength(30);
                entity.Property(item => item.Quantity).HasPrecision(12, 2);
                entity.Property(item => item.UnitPrice).HasPrecision(12, 2);
                entity.Property(item => item.DiscountPercentage).HasPrecision(5, 2);
                entity.Property(item => item.DiscountAmount).HasPrecision(12, 2);
                entity.Property(item => item.TaxPercentage).HasPrecision(5, 2);
                entity.Property(item => item.TaxAmount).HasPrecision(12, 2);
                entity.Property(item => item.LineSubtotal).HasPrecision(12, 2);
                entity.Property(item => item.LineTotal).HasPrecision(12, 2);
                entity.Property(item => item.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(item => item.Sport).WithMany().HasForeignKey(item => item.SportId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(item => item.Facility).WithMany().HasForeignKey(item => item.FacilityId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(item => item.Court).WithMany().HasForeignKey(item => item.CourtId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(item => item.MembershipPlan).WithMany().HasForeignKey(item => item.MembershipPlanId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SalesOrder>(entity =>
            {
                entity.HasIndex(order => order.SalesOrderNumber).IsUnique();
                entity.HasIndex(order => order.SalesQuotationId)
                    .IsUnique()
                    .HasFilter("[SalesQuotationId] IS NOT NULL AND [IsActive] = 1");
                entity.HasIndex(order => order.Status);
                entity.HasIndex(order => order.CustomerId);
                entity.HasIndex(order => order.OpportunityId);
                entity.HasIndex(order => order.OrderDate);
                entity.Property(order => order.SalesOrderNumber).IsRequired().HasMaxLength(30);
                entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(order => order.CustomerPurchaseOrderNumber).HasMaxLength(80);
                entity.Property(order => order.CustomerReference).HasMaxLength(80);
                entity.Property(order => order.Subject).HasMaxLength(180);
                entity.Property(order => order.TermsAndConditions).HasMaxLength(2000);
                entity.Property(order => order.CustomerNotes).HasMaxLength(1500);
                entity.Property(order => order.InternalNotes).HasMaxLength(2000);
                entity.Property(order => order.Subtotal).HasPrecision(12, 2);
                entity.Property(order => order.DiscountTotal).HasPrecision(12, 2);
                entity.Property(order => order.TaxTotal).HasPrecision(12, 2);
                entity.Property(order => order.GrandTotal).HasPrecision(12, 2);
                entity.Property(order => order.Currency).IsRequired().HasMaxLength(20);
                entity.Property(order => order.CurrencySymbol).HasMaxLength(8);
                entity.Property(order => order.CancellationReason).HasMaxLength(500);
                entity.Property(order => order.SourceType).IsRequired().HasMaxLength(20);
                entity.Property(order => order.ApprovalStatus).HasConversion<string>().HasMaxLength(30);
                entity.Property(order => order.RejectionReason).HasMaxLength(1500);
                entity.Property(order => order.LastRevisionReason).HasMaxLength(1000);
                entity.Property(order => order.RowVersion).IsRowVersion();
                entity.Property(order => order.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(order => order.IsActive).HasDefaultValue(true);
                entity.HasOne(order => order.SalesQuotation).WithMany().HasForeignKey(order => order.SalesQuotationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(order => order.Opportunity).WithMany().HasForeignKey(order => order.OpportunityId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(order => order.Customer).WithMany().HasForeignKey(order => order.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(order => order.AssignedToUser).WithMany().HasForeignKey(order => order.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
                entity.HasMany(order => order.Items).WithOne(item => item.SalesOrder).HasForeignKey(item => item.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DocumentApproval>(entity =>
            {
                entity.HasIndex(x => new { x.DocumentType, x.DocumentId, x.RevisionNumber });
                entity.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.Action).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.DocumentNumber).IsRequired().HasMaxLength(30);
                entity.Property(x => x.PreviousStatus).HasMaxLength(40);
                entity.Property(x => x.NewStatus).HasMaxLength(40);
                entity.Property(x => x.Comments).HasMaxLength(1500);
                entity.Property(x => x.RejectionReason).HasMaxLength(1500);
                entity.Property(x => x.Currency).HasMaxLength(20);
                entity.Property(x => x.IpAddress).HasMaxLength(64);
                entity.Property(x => x.FinancialTotal).HasPrecision(12, 2);
                entity.HasOne(x => x.SubmittedByUser).WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.ActionByUser).WithMany().HasForeignKey(x => x.ActionByUserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<DocumentRevision>(entity =>
            {
                entity.HasIndex(x => new { x.DocumentType, x.DocumentId, x.RevisionNumber }).IsUnique();
                entity.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.DocumentNumber).IsRequired().HasMaxLength(30);
                entity.Property(x => x.SnapshotJson).IsRequired();
                entity.Property(x => x.RevisionReason).IsRequired().HasMaxLength(1000);
                entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<DocumentActivity>(entity =>
            {
                entity.HasIndex(x => new { x.DocumentType, x.DocumentId, x.PerformedAt });
                entity.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.ActivityType).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.Description).IsRequired().HasMaxLength(1000);
                entity.HasOne(x => x.PerformedByUser).WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SalesOrderItem>(entity =>
            {
                entity.HasIndex(item => item.SalesOrderId);
                entity.Property(item => item.ItemType).HasConversion<string>().HasMaxLength(30);
                entity.Property(item => item.Description).IsRequired().HasMaxLength(300);
                entity.Property(item => item.UnitOfMeasure).IsRequired().HasMaxLength(30);
                entity.Property(item => item.Quantity).HasPrecision(12, 2);
                entity.Property(item => item.UnitPrice).HasPrecision(12, 2);
                entity.Property(item => item.DiscountPercentage).HasPrecision(5, 2);
                entity.Property(item => item.DiscountAmount).HasPrecision(12, 2);
                entity.Property(item => item.TaxPercentage).HasPrecision(5, 2);
                entity.Property(item => item.TaxAmount).HasPrecision(12, 2);
                entity.Property(item => item.LineSubtotal).HasPrecision(12, 2);
                entity.Property(item => item.LineTotal).HasPrecision(12, 2);
                entity.Property(item => item.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(item => item.Sport).WithMany().HasForeignKey(item => item.SportId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(item => item.Facility).WithMany().HasForeignKey(item => item.FacilityId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(item => item.Court).WithMany().HasForeignKey(item => item.CourtId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(item => item.MembershipPlan).WithMany().HasForeignKey(item => item.MembershipPlanId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SalesInvoice>(entity =>
            {
                entity.HasIndex(x => x.InvoiceNumber).IsUnique();
                entity.HasIndex(x => x.SalesOrderId).IsUnique().HasFilter("[SalesOrderId] IS NOT NULL AND [IsActive] = 1");
                entity.HasIndex(x => x.Status);
                entity.HasIndex(x => x.DueDate);
                entity.HasIndex(x => x.CustomerId);
                entity.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(30);
                entity.Property(x => x.Currency).IsRequired().HasMaxLength(20);
                entity.Property(x => x.CurrencySymbol).HasMaxLength(8);
                entity.Property(x => x.Subject).HasMaxLength(180);
                entity.Property(x => x.CustomerPurchaseOrderNumber).HasMaxLength(80);
                entity.Property(x => x.CustomerReference).HasMaxLength(80);
                entity.Property(x => x.BillingAddress).HasMaxLength(1000);
                entity.Property(x => x.TaxRegistrationNumber).HasMaxLength(80);
                entity.Property(x => x.PaymentTerms).HasMaxLength(500);
                entity.Property(x => x.TermsAndConditions).HasMaxLength(2000);
                entity.Property(x => x.CustomerNotes).HasMaxLength(1500);
                entity.Property(x => x.InternalNotes).HasMaxLength(2000);
                entity.Property(x => x.RejectionReason).HasMaxLength(1500);
                entity.Property(x => x.CancellationReason).HasMaxLength(1500);
                entity.Property(x => x.VoidReason).HasMaxLength(1500);
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                foreach (var p in new[] { "Subtotal", "DiscountTotal", "TaxTotal", "AdjustmentAmount", "GrandTotal", "PaidAmount", "OutstandingAmount" }) entity.Property(p).HasPrecision(12, 2);
                entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.SalesQuotation).WithMany().HasForeignKey(x => x.SalesQuotationId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(x => x.Items).WithOne(x => x.SalesInvoice).HasForeignKey(x => x.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<SalesInvoiceItem>(entity =>
            {
                entity.HasIndex(x => x.SalesInvoiceId);
                entity.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.Description).IsRequired().HasMaxLength(300);
                entity.Property(x => x.UnitOfMeasure).IsRequired().HasMaxLength(30);
                foreach (var p in new[] { "Quantity", "UnitPrice", "DiscountAmount", "TaxAmount", "LineSubtotal", "LineTotal" }) entity.Property(p).HasPrecision(12, 2);
                entity.Property(x => x.DiscountPercentage).HasPrecision(5, 2); entity.Property(x => x.TaxPercentage).HasPrecision(5, 2);
                entity.HasOne(x => x.Sport).WithMany().HasForeignKey(x => x.SportId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.MembershipPlan).WithMany().HasForeignKey(x => x.MembershipPlanId).OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
