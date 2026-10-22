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
        public DbSet<PaymentRefund> PaymentRefunds => Set<PaymentRefund>();
        public DbSet<DocumentApproval> DocumentApprovals => Set<DocumentApproval>();
        public DbSet<DocumentRevision> DocumentRevisions => Set<DocumentRevision>();
        public DbSet<DocumentActivity> DocumentActivities => Set<DocumentActivity>();
        public DbSet<DocumentEditHistory> DocumentEditHistories => Set<DocumentEditHistory>();
        public DbSet<JobPosting> JobPostings => Set<JobPosting>();
        public DbSet<JobPostingSkill> JobPostingSkills => Set<JobPostingSkill>();
        public DbSet<JobPostingScreeningQuestion> JobPostingScreeningQuestions => Set<JobPostingScreeningQuestion>();
        public DbSet<JobApplication> JobApplications => Set<JobApplication>();
        public DbSet<JobApplicationDocument> JobApplicationDocuments => Set<JobApplicationDocument>();
        public DbSet<Interview> Interviews => Set<Interview>();
        public DbSet<JobOffer> JobOffers => Set<JobOffer>();
        public DbSet<JobOfferDocument> JobOfferDocuments => Set<JobOfferDocument>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<PerformanceCase> PerformanceCases => Set<PerformanceCase>();
        public DbSet<PerformanceMeeting> PerformanceMeetings => Set<PerformanceMeeting>();
        public DbSet<PerformanceWarning> PerformanceWarnings => Set<PerformanceWarning>();
        public DbSet<ImprovementPlan> ImprovementPlans => Set<ImprovementPlan>();
        public DbSet<TerminationRequest> TerminationRequests => Set<TerminationRequest>();
        public DbSet<TerminationDocument> TerminationDocuments => Set<TerminationDocument>();
        public DbSet<ClearanceItem> ClearanceItems => Set<ClearanceItem>();
        public DbSet<TerminationStatusHistory> TerminationStatusHistories => Set<TerminationStatusHistory>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(user => user.Email).IsUnique();
                entity.HasIndex(user => user.PhoneNumber)
                    .IsUnique()
                    .HasFilter("[PhoneNumber] IS NOT NULL");
                entity.HasIndex(user => user.CnicNumber)
                    .IsUnique()
                    .HasFilter("[CnicNumber] IS NOT NULL");

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

                entity.Property(user => user.CnicNumber)
                    .HasMaxLength(20);

                entity.Property(user => user.Gender)
                    .HasConversion<string>()
                    .HasMaxLength(30);

                entity.Property(user => user.Address)
                    .HasMaxLength(500);

                entity.Property(user => user.Department)
                    .HasMaxLength(80);

                entity.Property(user => user.Designation)
                    .HasMaxLength(100);

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
                    .IsUnique()
                    .HasFilter("[DayOfWeek] IS NOT NULL");
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
                // One manual booking maps to exactly one Sales Order (and one Sales Order back to
                // one booking). The live database already enforces this filtered unique index; the
                // EF model now declares the same shape so future migrations cannot weaken it.
                entity.HasIndex(booking => booking.SalesOrderId)
                    .IsUnique()
                    .HasFilter("[SalesOrderId] IS NOT NULL");
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
                entity.HasOne(booking => booking.SalesOrder)
                    .WithMany()
                    .HasForeignKey(booking => booking.SalesOrderId)
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

            modelBuilder.Entity<PaymentRefund>(entity =>
            {
                entity.HasIndex(refund => refund.RefundNumber).IsUnique();
                entity.HasIndex(refund => refund.PaymentTransactionId);
                entity.HasIndex(refund => refund.PaymentId);
                entity.HasIndex(refund => refund.BookingId);
                entity.HasIndex(refund => refund.Status);
                entity.Property(refund => refund.RefundNumber).IsRequired().HasMaxLength(30);
                entity.Property(refund => refund.Amount).HasPrecision(12, 2);
                entity.Property(refund => refund.Reason).HasConversion<string>().HasMaxLength(30);
                entity.Property(refund => refund.ReasonDetails).HasMaxLength(1500);
                entity.Property(refund => refund.Method).HasConversion<string>().HasMaxLength(30);
                entity.Property(refund => refund.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(refund => refund.GatewayRefundId).HasMaxLength(100);
                entity.Property(refund => refund.GatewayRefundReference).HasMaxLength(100);
                entity.Property(refund => refund.FailureReason).HasMaxLength(500);
                entity.Property(refund => refund.IpAddress).HasMaxLength(128);
                entity.Property(refund => refund.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(refund => refund.PaymentTransaction).WithMany().HasForeignKey(refund => refund.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(refund => refund.Payment).WithMany().HasForeignKey(refund => refund.PaymentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(refund => refund.Booking).WithMany().HasForeignKey(refund => refund.BookingId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(refund => refund.Customer).WithMany().HasForeignKey(refund => refund.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(refund => refund.InitiatedByUser).WithMany().HasForeignKey(refund => refund.InitiatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(refund => refund.ProcessedByUser).WithMany().HasForeignKey(refund => refund.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
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
                entity.HasIndex(opportunity => opportunity.NextFollowUpAt);
                entity.Property(opportunity => opportunity.OpportunityNumber).IsRequired().HasMaxLength(30);
                entity.Property(opportunity => opportunity.Name).IsRequired().HasMaxLength(150);
                entity.Property(opportunity => opportunity.Type).HasConversion<string>().HasMaxLength(30);
                entity.Property(opportunity => opportunity.Stage).HasConversion<string>().HasMaxLength(30);
                entity.Property(opportunity => opportunity.ExpectedValue).HasPrecision(12, 2);
                entity.Property(opportunity => opportunity.CustomerRequirement).HasMaxLength(1500);
                entity.Property(opportunity => opportunity.ProposedSolution).HasMaxLength(1500);
                entity.Property(opportunity => opportunity.CompetitorInformation).HasMaxLength(1000);
                entity.Property(opportunity => opportunity.LossReason).HasMaxLength(500);
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
            modelBuilder.Entity<DocumentEditHistory>(entity =>
            {
                entity.HasIndex(x => new { x.DocumentType, x.DocumentId, x.ChangedAt });
                entity.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(30);
                entity.Property(x => x.DocumentNumber).IsRequired().HasMaxLength(30);
                entity.Property(x => x.FieldName).IsRequired().HasMaxLength(80);
                entity.Property(x => x.PreviousValue).HasMaxLength(500);
                entity.Property(x => x.UpdatedValue).HasMaxLength(500);
                entity.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
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

            modelBuilder.Entity<JobPosting>(entity =>
            {
                entity.HasIndex(posting => posting.ReferenceNumber).IsUnique();
                entity.HasIndex(posting => posting.Status);
                entity.HasIndex(posting => posting.ApplicationDeadline);
                entity.HasIndex(posting => posting.Department);
                entity.Property(posting => posting.ReferenceNumber).IsRequired().HasMaxLength(30);
                entity.Property(posting => posting.JobTitle).IsRequired().HasMaxLength(100);
                entity.Property(posting => posting.Department).IsRequired().HasMaxLength(80);
                entity.Property(posting => posting.EmploymentType).HasConversion<string>().HasMaxLength(20);
                entity.Property(posting => posting.WorkplaceType).HasConversion<string>().HasMaxLength(20);
                entity.Property(posting => posting.JobLocation).IsRequired().HasMaxLength(150);
                entity.Property(posting => posting.MinimumSalary).HasPrecision(12, 2);
                entity.Property(posting => posting.MaximumSalary).HasPrecision(12, 2);
                entity.Property(posting => posting.Currency).IsRequired().HasMaxLength(10);
                entity.Property(posting => posting.ExperienceRequired).IsRequired().HasMaxLength(80);
                entity.Property(posting => posting.MinimumEducation).IsRequired().HasMaxLength(120);
                entity.Property(posting => posting.JobDescription).IsRequired().HasMaxLength(5000);
                entity.Property(posting => posting.KeyResponsibilities).IsRequired().HasMaxLength(3000);
                entity.Property(posting => posting.Qualifications).IsRequired().HasMaxLength(3000);
                entity.Property(posting => posting.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(posting => posting.Benefits).HasMaxLength(2000);
                entity.Property(posting => posting.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(posting => posting.IsActive).HasDefaultValue(true);
                entity.HasOne(posting => posting.HiringManager).WithMany().HasForeignKey(posting => posting.HiringManagerUserId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(posting => posting.CreatedByUser).WithMany().HasForeignKey(posting => posting.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(posting => posting.Skills).WithOne(skill => skill.JobPosting).HasForeignKey(skill => skill.JobPostingId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(posting => posting.ScreeningQuestions).WithOne(question => question.JobPosting).HasForeignKey(question => question.JobPostingId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<JobPostingSkill>(entity =>
            {
                entity.HasIndex(skill => skill.JobPostingId);
                entity.Property(skill => skill.Skill).IsRequired().HasMaxLength(80);
            });

            modelBuilder.Entity<JobPostingScreeningQuestion>(entity =>
            {
                entity.HasIndex(question => question.JobPostingId);
                entity.Property(question => question.Question).IsRequired().HasMaxLength(500);
            });

            modelBuilder.Entity<JobApplication>(entity =>
            {
                entity.HasIndex(application => application.ApplicationNumber).IsUnique();
                entity.HasIndex(application => new { application.JobPostingId, application.Email })
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
                entity.HasIndex(application => new { application.JobPostingId, application.NationalIdNumber })
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
                entity.HasIndex(application => new { application.JobPostingId, application.Phone })
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
                entity.HasIndex(application => application.JobPostingId);
                entity.HasIndex(application => application.Status);
                entity.Property(application => application.ApplicationNumber).IsRequired().HasMaxLength(30);
                entity.Property(application => application.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(application => application.LastName).IsRequired().HasMaxLength(50);
                entity.Property(application => application.Email).IsRequired().HasMaxLength(254);
                entity.Property(application => application.Phone).IsRequired().HasMaxLength(30);
                entity.Property(application => application.NationalIdNumber).IsRequired().HasMaxLength(30);
                entity.Property(application => application.Gender).HasConversion<string>().HasMaxLength(30);
                entity.Property(application => application.City).IsRequired().HasMaxLength(80);
                entity.Property(application => application.Country).IsRequired().HasMaxLength(80);
                entity.Property(application => application.CurrentAddress).HasMaxLength(250);
                entity.Property(application => application.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(application => application.HighestEducation).IsRequired().HasMaxLength(120);
                entity.Property(application => application.CurrentEmployer).HasMaxLength(150);
                entity.Property(application => application.CurrentJobTitle).HasMaxLength(100);
                entity.Property(application => application.ExpectedSalary).HasPrecision(12, 2);
                entity.Property(application => application.NoticePeriod).HasMaxLength(40);
                entity.Property(application => application.CoverLetter).HasMaxLength(3000);
                entity.Property(application => application.LinkedInUrl).HasMaxLength(300);
                entity.Property(application => application.PortfolioUrl).HasMaxLength(300);
                entity.Property(application => application.CvFileName).IsRequired().HasMaxLength(200);
                entity.Property(application => application.CvOriginalFileName).HasMaxLength(255);
                entity.Property(application => application.CvContentType).HasMaxLength(100);
                entity.Property(application => application.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(application => application.IsActive).HasDefaultValue(true);
                entity.HasOne(application => application.JobPosting).WithMany(posting => posting.Applications).HasForeignKey(application => application.JobPostingId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(application => application.CreatedByUser).WithMany().HasForeignKey(application => application.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(application => application.Documents).WithOne(document => document.JobApplication).HasForeignKey(document => document.JobApplicationId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(application => application.Interviews).WithOne(interview => interview.JobApplication).HasForeignKey(interview => interview.JobApplicationId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<JobApplicationDocument>(entity =>
            {
                entity.HasIndex(document => document.JobApplicationId);
                entity.Property(document => document.StoredFileName).IsRequired().HasMaxLength(200);
                entity.Property(document => document.OriginalFileName).IsRequired().HasMaxLength(255);
                entity.Property(document => document.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(document => document.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<Interview>(entity =>
            {
                entity.HasIndex(interview => interview.JobApplicationId);
                entity.HasIndex(interview => interview.Status);
                entity.HasIndex(interview => interview.InterviewDate);
                entity.Property(interview => interview.InterviewType).HasConversion<string>().HasMaxLength(20);
                entity.Property(interview => interview.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(interview => interview.Outcome).HasConversion<string>().HasMaxLength(20);
                entity.Property(interview => interview.Interviewer).IsRequired().HasMaxLength(150);
                entity.Property(interview => interview.Location).HasMaxLength(250);
                entity.Property(interview => interview.Notes).HasMaxLength(2000);
                entity.Property(interview => interview.Feedback).HasMaxLength(2000);
                entity.Property(interview => interview.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(interview => interview.IsActive).HasDefaultValue(true);
                entity.HasOne(interview => interview.CreatedByUser).WithMany().HasForeignKey(interview => interview.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<JobOffer>(entity =>
            {
                entity.HasIndex(offer => offer.ReferenceNumber).IsUnique();
                // A candidate may only hold one in-flight offer at a time;
                // rejected / withdrawn / expired offers stay queryable but do
                // not block a replacement. Filtered indexes do not support
                // NOT IN, so the predicate lists the blocking statuses.
                entity.HasIndex(offer => offer.JobApplicationId)
                    .IsUnique()
                    .HasFilter("[IsActive] = 1 AND [Status] IN ('Draft','PendingAcceptance','Accepted','Hired')");
                entity.HasIndex(offer => offer.Status);
                entity.HasIndex(offer => offer.JoiningDate);
                entity.Property(offer => offer.ReferenceNumber).IsRequired().HasMaxLength(30);
                entity.Property(offer => offer.Designation).IsRequired().HasMaxLength(100);
                entity.Property(offer => offer.Department).IsRequired().HasMaxLength(80);
                entity.Property(offer => offer.EmploymentType).HasConversion<string>().HasMaxLength(20);
                entity.Property(offer => offer.WorkLocation).IsRequired().HasMaxLength(150);
                entity.Property(offer => offer.OfferedSalary).HasPrecision(12, 2);
                entity.Property(offer => offer.Currency).IsRequired().HasMaxLength(10);
                entity.Property(offer => offer.SalaryFrequency).HasConversion<string>().HasMaxLength(20);
                entity.Property(offer => offer.Benefits).HasMaxLength(2000);
                entity.Property(offer => offer.Terms).HasMaxLength(2000);
                entity.Property(offer => offer.Remarks).HasMaxLength(2000);
                entity.Property(offer => offer.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(offer => offer.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(offer => offer.IsActive).HasDefaultValue(true);
                entity.HasOne(offer => offer.JobApplication).WithMany(application => application.Offers).HasForeignKey(offer => offer.JobApplicationId).OnDelete(DeleteBehavior.Restrict);
                // Two optional links to Employees would form multiple cascade
                // paths with SET NULL, so the reporting-manager link is NoAction
                // while the hired-employee link nulls out.
                entity.HasOne(offer => offer.ReportingManager).WithMany().HasForeignKey(offer => offer.ReportingManagerId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(offer => offer.Employee).WithMany().HasForeignKey(offer => offer.EmployeeId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(offer => offer.CreatedByUser).WithMany().HasForeignKey(offer => offer.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(offer => offer.Documents).WithOne(document => document.JobOffer).HasForeignKey(document => document.JobOfferId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<JobOfferDocument>(entity =>
            {
                entity.HasIndex(document => document.JobOfferId);
                entity.Property(document => document.StoredFileName).IsRequired().HasMaxLength(200);
                entity.Property(document => document.OriginalFileName).IsRequired().HasMaxLength(255);
                entity.Property(document => document.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(document => document.Title).HasMaxLength(150);
                entity.Property(document => document.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasIndex(employee => employee.EmployeeNumber).IsUnique();
                entity.HasIndex(employee => employee.Email)
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
                entity.HasIndex(employee => employee.NationalIdNumber)
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
                entity.HasIndex(employee => employee.Department);
                entity.HasIndex(employee => employee.Designation);
                entity.HasIndex(employee => employee.EmploymentStatus);
                entity.Property(employee => employee.EmployeeNumber).IsRequired().HasMaxLength(20);
                entity.Property(employee => employee.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(employee => employee.LastName).IsRequired().HasMaxLength(50);
                entity.Property(employee => employee.ProfileImageUrl).HasMaxLength(300);
                entity.Property(employee => employee.Email).IsRequired().HasMaxLength(254);
                entity.Property(employee => employee.PhoneNumber).IsRequired().HasMaxLength(30);
                entity.Property(employee => employee.NationalIdNumber).IsRequired().HasMaxLength(30);
                entity.Property(employee => employee.Gender).HasConversion<string>().HasMaxLength(30);
                entity.Property(employee => employee.Department).IsRequired().HasMaxLength(80);
                entity.Property(employee => employee.Designation).IsRequired().HasMaxLength(100);
                entity.Property(employee => employee.EmploymentType).HasConversion<string>().HasMaxLength(20);
                entity.Property(employee => employee.EmploymentStatus).HasConversion<string>().HasMaxLength(20);
                entity.Property(employee => employee.EmploymentAddress).HasMaxLength(250);
                entity.Property(employee => employee.EmergencyContactName).HasMaxLength(100);
                entity.Property(employee => employee.EmergencyContactNumber).HasMaxLength(30);
                entity.Property(employee => employee.BasicSalary).HasPrecision(12, 2);
                entity.Property(employee => employee.Notes).HasMaxLength(2000);
                entity.Property(employee => employee.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(employee => employee.IsActive).HasDefaultValue(true);
                entity.HasOne(employee => employee.ReportingManager).WithMany().HasForeignKey(employee => employee.ReportingManagerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(employee => employee.CreatedByUser).WithMany().HasForeignKey(employee => employee.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PerformanceCase>(entity =>
            {
                entity.HasIndex(detail => detail.CaseNumber).IsUnique();
                entity.HasIndex(detail => detail.Status);
                entity.HasIndex(detail => detail.EmployeeId);
                entity.HasIndex(detail => detail.ReportedDate);
                entity.Property(detail => detail.CaseNumber).IsRequired().HasMaxLength(30);
                entity.Property(detail => detail.IssueType).HasConversion<string>().HasMaxLength(30);
                entity.Property(detail => detail.Title).IsRequired().HasMaxLength(150);
                entity.Property(detail => detail.Description).IsRequired().HasMaxLength(4000);
                entity.Property(detail => detail.Severity).HasConversion<string>().HasMaxLength(20);
                entity.Property(detail => detail.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(detail => detail.ResolutionNotes).HasMaxLength(4000);
                entity.Property(detail => detail.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(detail => detail.IsActive).HasDefaultValue(true);
                entity.HasOne(detail => detail.Employee).WithMany(employee => employee.PerformanceCases).HasForeignKey(detail => detail.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                // Two optional links to Users, so both stay NoAction to avoid
                // multiple cascade paths into the case record.
                entity.HasOne(detail => detail.AssignedHrUser).WithMany().HasForeignKey(detail => detail.AssignedHrUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(detail => detail.CreatedByUser).WithMany().HasForeignKey(detail => detail.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(detail => detail.Meetings).WithOne(meeting => meeting.PerformanceCase).HasForeignKey(meeting => meeting.PerformanceCaseId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(detail => detail.Warnings).WithOne(warning => warning.PerformanceCase).HasForeignKey(warning => warning.PerformanceCaseId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(detail => detail.ImprovementPlans).WithOne(plan => plan.PerformanceCase).HasForeignKey(plan => plan.PerformanceCaseId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PerformanceMeeting>(entity =>
            {
                entity.HasIndex(meeting => meeting.PerformanceCaseId);
                entity.HasIndex(meeting => meeting.MeetingDate);
                entity.HasIndex(meeting => meeting.Status);
                entity.Property(meeting => meeting.DiscussionNotes).IsRequired().HasMaxLength(3000);
                entity.Property(meeting => meeting.Outcome).HasMaxLength(2000);
                entity.Property(meeting => meeting.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(meeting => meeting.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(meeting => meeting.IsActive).HasDefaultValue(true);
                entity.HasOne(meeting => meeting.CreatedByUser).WithMany().HasForeignKey(meeting => meeting.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PerformanceWarning>(entity =>
            {
                entity.HasIndex(warning => warning.PerformanceCaseId);
                entity.HasIndex(warning => warning.IssueDate);
                entity.HasIndex(warning => warning.Status);
                entity.Property(warning => warning.Reason).IsRequired().HasMaxLength(3000);
                entity.Property(warning => warning.ResponseRemarks).HasMaxLength(3000);
                entity.Property(warning => warning.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(warning => warning.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(warning => warning.IsActive).HasDefaultValue(true);
                entity.HasOne(warning => warning.CreatedByUser).WithMany().HasForeignKey(warning => warning.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ImprovementPlan>(entity =>
            {
                entity.HasIndex(plan => plan.PerformanceCaseId);
                entity.HasIndex(plan => plan.Status);
                entity.HasIndex(plan => plan.ReviewDate);
                entity.Property(plan => plan.Objectives).IsRequired().HasMaxLength(3000);
                entity.Property(plan => plan.MeasurableExpectations).IsRequired().HasMaxLength(3000);
                entity.Property(plan => plan.ReviewNotes).HasMaxLength(3000);
                entity.Property(plan => plan.Outcome).HasMaxLength(2000);
                entity.Property(plan => plan.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(plan => plan.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(plan => plan.IsActive).HasDefaultValue(true);
                entity.HasOne(plan => plan.CreatedByUser).WithMany().HasForeignKey(plan => plan.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TerminationRequest>(entity =>
            {
                entity.HasIndex(request => request.ReferenceNumber).IsUnique();
                entity.HasIndex(request => request.Status);
                entity.HasIndex(request => request.EmployeeId);
                entity.HasIndex(request => request.RequestDate);
                entity.HasIndex(request => request.ProposedLastWorkingDay);
                entity.Property(request => request.ReferenceNumber).IsRequired().HasMaxLength(30);
                entity.Property(request => request.Reason).IsRequired().HasMaxLength(200);
                entity.Property(request => request.Explanation).IsRequired().HasMaxLength(4000);
                entity.Property(request => request.TerminationType).HasConversion<string>().HasMaxLength(30);
                entity.Property(request => request.NoticePeriod).HasMaxLength(50);
                entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(request => request.Remarks).HasMaxLength(3000);
                entity.Property(request => request.DecisionRemarks).HasMaxLength(3000);
                entity.Property(request => request.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(request => request.IsActive).HasDefaultValue(true);
                entity.HasOne(request => request.Employee).WithMany(employee => employee.TerminationRequests).HasForeignKey(request => request.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                // Two optional links to Users, so both stay NoAction to avoid
                // multiple cascade paths into the request record.
                entity.HasOne(request => request.DecidedByUser).WithMany().HasForeignKey(request => request.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(request => request.CreatedByUser).WithMany().HasForeignKey(request => request.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(request => request.Documents).WithOne(document => document.TerminationRequest).HasForeignKey(document => document.TerminationRequestId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(request => request.ClearanceItems).WithOne(item => item.TerminationRequest).HasForeignKey(item => item.TerminationRequestId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(request => request.StatusHistory).WithOne(history => history.TerminationRequest).HasForeignKey(history => history.TerminationRequestId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TerminationDocument>(entity =>
            {
                entity.HasIndex(document => document.TerminationRequestId);
                entity.HasIndex(document => document.IsLetter);
                entity.Property(document => document.StoredFileName).IsRequired().HasMaxLength(200);
                entity.Property(document => document.OriginalFileName).IsRequired().HasMaxLength(255);
                entity.Property(document => document.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(document => document.Title).HasMaxLength(150);
                entity.Property(document => document.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<ClearanceItem>(entity =>
            {
                entity.HasIndex(item => item.TerminationRequestId);
                entity.HasIndex(item => item.Category);
                entity.HasIndex(item => item.Status);
                entity.Property(item => item.Category).HasConversion<string>().HasMaxLength(30);
                entity.Property(item => item.ResponsiblePerson).HasMaxLength(120);
                entity.Property(item => item.ResponsibleDepartment).HasMaxLength(100);
                entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(item => item.OutstandingItems).HasMaxLength(2000);
                entity.Property(item => item.Remarks).HasMaxLength(2000);
                entity.Property(item => item.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(item => item.IsActive).HasDefaultValue(true);
                entity.HasOne(item => item.CreatedByUser).WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TerminationStatusHistory>(entity =>
            {
                entity.HasIndex(history => history.TerminationRequestId);
                entity.HasIndex(history => history.ChangedAt);
                entity.Property(history => history.FromStatus).HasConversion<string>().HasMaxLength(30);
                entity.Property(history => history.ToStatus).HasConversion<string>().HasMaxLength(30);
                entity.Property(history => history.Note).HasMaxLength(2000);
                entity.Property(history => history.ChangedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasOne(history => history.ChangedByUser).WithMany().HasForeignKey(history => history.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
