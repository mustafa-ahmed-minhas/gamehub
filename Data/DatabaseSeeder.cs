using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (!await dbContext.Users.AnyAsync())
            {
                // One development test user per application role (UserRole). Every
                // account shares the same password so each role can be exercised
                // end-to-end. The SuperAdmin keeps the original admin@gamehub.pk.
                const string seedPassword = "Admin@123";
                var seedUsers = new[]
                {
                    new { First = "Mustafa", Last = "Ahmed",    Email = "admin@gamehub.pk",            Phone = "03001234000", Role = UserRole.SuperAdmin },
                    new { First = "Ayesha",  Last = "Siddiqui", Email = "admin@gamehub.com",           Phone = "03001234001", Role = UserRole.Admin },
                    new { First = "Hamza",   Last = "Tariq",    Email = "booking.manager@gamehub.com", Phone = "03001234002", Role = UserRole.BookingManager },
                    new { First = "Usman",   Last = "Ghani",    Email = "court.manager@gamehub.com",   Phone = "03001234003", Role = UserRole.CourtManager },
                    new { First = "Fatima",  Last = "Noor",     Email = "finance.manager@gamehub.com", Phone = "03001234004", Role = UserRole.FinanceManager },
                    new { First = "Kamran",  Last = "Aslam",    Email = "staff.manager@gamehub.com",   Phone = "03001234005", Role = UserRole.StaffManager },
                    new { First = "Sana",    Last = "Iqbal",    Email = "receptionist@gamehub.com",    Phone = "03001234006", Role = UserRole.Receptionist },
                    new { First = "Tariq",   Last = "Jameel",   Email = "viewer@gamehub.com",          Phone = "03001234007", Role = UserRole.Viewer }
                };

                foreach (var seedUser in seedUsers)
                {
                    var user = new User
                    {
                        FirstName = seedUser.First,
                        LastName = seedUser.Last,
                        Email = seedUser.Email,
                        PhoneNumber = seedUser.Phone,
                        Role = seedUser.Role,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    user.PasswordHash = PasswordHelper.HashPassword(user, seedPassword);
                    dbContext.Users.Add(user);
                }

                await dbContext.SaveChangesAsync();
            }

            if (!await dbContext.SystemSettings.AnyAsync())
            {
                dbContext.SystemSettings.Add(new SystemSettings
                {
                    ArenaName = "GameHub Arena",
                    Phone = "+92 300 1234567",
                    Email = "info@gamehub.pk",
                    Address = "123 Sports Avenue, Lahore",
                    Logo = null,
                    OpeningTime = new TimeOnly(6, 0),
                    ClosingTime = new TimeOnly(23, 59),
                    SlotDurationMinutes = 60,
                    BufferMinutes = 10,
                    AdvanceBookingDays = 14,
                    AllowWalkIn = true,
                    AllowOnlineBooking = true,
                    Currency = "PKR",
                    CurrencySymbol = "Rs",
                    TaxPercentage = 0,
                    BookingPrefix = "BK",
                    InvoicePrefix = "INV",
                    ReceiptPrefix = "RCPT",
                    LeadPrefix = "LEAD",
                    OpportunityPrefix = "OPP",
                    QuotationPrefix = "SQ",
                    SalesOrderPrefix = "SO",
                    DefaultQuotationValidityDays = 7,
                    SessionTimeoutMinutes = 480,
                    PasswordMinLength = 8,
                    RequireStrongPassword = true,
                    EnableCustomerBookingReminders = true,
                    FirstReminderHoursBefore = 24,
                    SecondReminderHoursBefore = 3,
                    AllowCustomerCancellation = true,
                    CancellationCutoffHours = 6,
                    AllowCustomerRescheduling = true,
                    RescheduleCutoffHours = 6,
                    Version = "1.0.0",
                    BuildDate = DateTime.UtcNow
                });

                await dbContext.SaveChangesAsync();
            }

            await SeedArenaSetupAsync(dbContext);
            await SeedCustomersAsync(dbContext);
            await SeedMembershipsAsync(dbContext);
            await SeedBookingsAsync(dbContext);
            await SeedCrmAsync(dbContext);
            await SeedSalesAsync(dbContext);
            await SeedInvoiceDemoAsync(dbContext);
            await SeedHrAsync(dbContext);
        }

        private static async Task SeedInvoiceDemoAsync(ApplicationDbContext db)
        {
            if (await db.SalesInvoices.AnyAsync()) return;

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var settings = await db.SystemSettings.FirstAsync();
            var owner = await db.Users.FirstAsync();
            var customers = await db.Customers.Where(x => x.IsActive && !x.IsBlacklisted).OrderBy(x => x.Id).ToListAsync();
            var extraCustomers = new[]
            {
                ("Ayesha", "Malik", CustomerType.VIP, CustomerSource.Referral, "Ayesha Malik Fitness Club"),
                ("Hassan", "Raza", CustomerType.Individual, CustomerSource.Website, (string?)null),
                ("Nida", "Farooq", CustomerType.Corporate, CustomerSource.Corporate, "Vertex Technologies"),
                ("Omar", "Saeed", CustomerType.Student, CustomerSource.SocialMedia, (string?)null),
                ("Sara", "Ilyas", CustomerType.Family, CustomerSource.PhoneCall, "Ilyas Family Sports Group")
            };
            var extraIndex = 0;
            while (customers.Count < 10 && extraIndex < extraCustomers.Length)
            {
                var d = extraCustomers[extraIndex++];
                var customer = new Customer
                {
                    CustomerCode = $"CUST-DEMO-{customers.Count + 1:00}", FirstName = d.Item1, LastName = d.Item2,
                    CustomerType = d.Item3, CustomerSource = d.Item4, OrganizationName = d.Item5,
                    PrimaryPhone = $"030{customers.Count + 1}-555{1200 + customers.Count}", Email = $"{d.Item1.ToLowerInvariant()}.{d.Item2.ToLowerInvariant()}@example.com",
                    AddressLine1 = "Sports District, Lahore", City = "Lahore", Country = "Pakistan", IsMember = d.Item3 is CustomerType.VIP or CustomerType.Student,
                    MembershipNumber = d.Item3 is CustomerType.VIP or CustomerType.Student ? $"MEM-DEMO-{customers.Count + 1:000}" : null,
                    LoyaltyPoints = 400 + customers.Count * 170, AllowCredit = d.Item3 == CustomerType.Corporate, CreditLimit = d.Item3 == CustomerType.Corporate ? 250000 : null,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp, IsActive = true, CreatedAt = now.AddDays(-90 + customers.Count * 4), LastVisitAt = now.AddDays(-customers.Count * 3)
                };
                db.Customers.Add(customer); customers.Add(customer);
            }
            await db.SaveChangesAsync();

            var items = new[]
            {
                ("Football Ground Booking", SalesItemType.CourtBooking, "Session", 8500m), ("Padel Court Booking", SalesItemType.CourtBooking, "Hour", 4200m),
                ("Cricket Ground Booking", SalesItemType.CourtBooking, "Session", 7600m), ("Badminton Court Booking", SalesItemType.CourtBooking, "Hour", 2800m),
                ("VIP Membership", SalesItemType.Membership, "Membership", 35000m), ("Personal Coaching Session", SalesItemType.Coaching, "Session", 6000m),
                ("Tournament Registration", SalesItemType.Tournament, "Entry", 12500m), ("Equipment Rental", SalesItemType.Other, "Package", 1800m)
            };

            var orders = await db.SalesOrders.Include(x => x.Items).Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
            for (var o = orders.Count; o < 6; o++)
            {
                var customer = customers[o % customers.Count];
                var order = new SalesOrder
                {
                    SalesOrderNumber = $"SO-DEMO-{o + 1:000}", CustomerId = customer.Id, AssignedToUserId = owner.Id, SourceType = "Direct",
                    OrderDate = today.AddDays(-35 + o * 4), ServiceStartDate = today.AddDays(-15 + o * 3), ServiceEndDate = today.AddDays(-14 + o * 3),
                    Status = SalesOrderStatus.ReadyForInvoice, RevisionNumber = 1, ApprovedAt = now.AddDays(-20 + o), ApprovedByUserId = owner.Id,
                    ConfirmedAt = now.AddDays(-18 + o), FulfilledAt = now.AddDays(-16 + o), Subject = $"{customer.FullName} Arena Service Order",
                    TermsAndConditions = "Service delivery is subject to confirmed court availability.", Currency = settings.Currency, CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                    CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-30 + o), IsActive = true
                };
                AddOrderLine(order, items[o % items.Length], 1 + (o % 2), o % 3 == 0 ? 5m : 0m, 16m, now.AddDays(-15 + o));
                AddOrderLine(order, items[(o + 2) % items.Length], 1, 0m, 16m, now.AddDays(-14 + o));
                CalculateOrder(order); db.SalesOrders.Add(order); orders.Add(order);
            }
            await db.SaveChangesAsync();

            var statuses = new[] { InvoiceStatus.Draft, InvoiceStatus.SubmittedForApproval, InvoiceStatus.Approved, InvoiceStatus.Rejected, InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, InvoiceStatus.Overdue, InvoiceStatus.Cancelled, InvoiceStatus.Draft, InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid, InvoiceStatus.Approved, InvoiceStatus.Rejected, InvoiceStatus.Paid, InvoiceStatus.Overdue, InvoiceStatus.SubmittedForApproval, InvoiceStatus.Issued, InvoiceStatus.Draft, InvoiceStatus.Cancelled, InvoiceStatus.Approved, InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, InvoiceStatus.Overdue, InvoiceStatus.Rejected, InvoiceStatus.Issued, InvoiceStatus.Draft, InvoiceStatus.SubmittedForApproval, InvoiceStatus.Approved, InvoiceStatus.Issued };
            for (var index = 0; index < statuses.Length; index++)
            {
                var sourceOrder = index < orders.Count ? orders[index] : null;
                var customer = sourceOrder?.CustomerId > 0 ? customers.First(x => x.Id == sourceOrder.CustomerId) : customers[index % customers.Count];
                var status = statuses[index];
                var invoiceDate = index % 5 == 0 ? today : today.AddDays(-3 - index * 2);
                var dueDate = status == InvoiceStatus.Overdue ? today.AddDays(-2 - index % 7) : today.AddDays(7 + index % 18);
                var invoice = new SalesInvoice
                {
                    InvoiceNumber = $"{(string.IsNullOrWhiteSpace(settings.InvoicePrefix) ? "INV" : settings.InvoicePrefix)}-DEMO-{index + 1:000}",
                    SourceType = sourceOrder == null ? InvoiceSourceType.Direct : InvoiceSourceType.SalesOrder, SalesOrderId = sourceOrder?.Id, SalesQuotationId = sourceOrder?.SalesQuotationId,
                    CustomerId = customer.Id, OpportunityId = sourceOrder?.OpportunityId, AssignedToUserId = owner.Id, InvoiceDate = invoiceDate, DueDate = dueDate,
                    Subject = sourceOrder == null ? $"{customer.FullName} Arena Services" : sourceOrder.Subject, CustomerPurchaseOrderNumber = customer.CustomerType == CustomerType.Corporate ? $"PO-{2026 + index}-{120 + index}" : null,
                    CustomerReference = $"GH-REF-{index + 1:000}", BillingAddress = customer.AddressLine1, PaymentTerms = index % 3 == 0 ? "Due on receipt" : "Net 14 days",
                    TermsAndConditions = "Thank you for choosing GameHub Arena. Services are subject to confirmed schedule availability.", CustomerNotes = "Please contact the arena desk for schedule adjustments.",
                    InternalNotes = index % 4 == 0 ? "Priority customer account." : null, Currency = settings.Currency, CurrencySymbol = settings.CurrencySymbol ?? "Rs", Status = status,
                    RevisionNumber = status is InvoiceStatus.Rejected or InvoiceStatus.SubmittedForApproval ? 2 : 1, IsLocked = status is not (InvoiceStatus.Draft or InvoiceStatus.Rejected),
                    CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-2 - index * 2), UpdatedAt = now.AddDays(-index), IsActive = true
                };
                var count = 2 + index % 5;
                for (var line = 0; line < count; line++) AddInvoiceLine(invoice, items[(index + line) % items.Length], 1 + (line % 3), line == 0 && index % 3 == 0 ? 10m : 0m, 16m, invoiceDate.AddDays(line));
                CalculateInvoice(invoice);
                if (status == InvoiceStatus.PartiallyPaid) { invoice.PaidAmount = Math.Round(invoice.GrandTotal * .45m, 2); invoice.OutstandingAmount = invoice.GrandTotal - invoice.PaidAmount; }
                if (status == InvoiceStatus.Paid) { invoice.PaidAmount = invoice.GrandTotal; invoice.OutstandingAmount = 0; }
                if (status is InvoiceStatus.Approved or InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid or InvoiceStatus.Overdue) { invoice.ApprovedAt = now.AddDays(-index - 1); invoice.ApprovedByUserId = owner.Id; }
                if (status is InvoiceStatus.Rejected) { invoice.RejectedAt = now.AddDays(-index); invoice.RejectedByUserId = owner.Id; invoice.RejectionReason = "Please confirm the revised service dates and commercial discount before approval."; }
                if (status is InvoiceStatus.SubmittedForApproval) invoice.SubmittedAt = now.AddDays(-index);
                if (status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid or InvoiceStatus.Overdue) invoice.IssuedAt = now.AddDays(-index - 1);
                if (status == InvoiceStatus.Cancelled) { invoice.CancelledAt = now.AddDays(-index); invoice.CancellationReason = "Customer requested cancellation before invoice issue."; }
                db.SalesInvoices.Add(invoice);
            }
            await db.SaveChangesAsync();
            foreach (var invoice in await db.SalesInvoices.Where(x => x.InvoiceNumber.Contains("-DEMO-")).ToListAsync())
            {
                db.DocumentActivities.Add(new DocumentActivity { DocumentType = DocumentType.SalesInvoice, DocumentId = invoice.Id, ActivityType = ApprovalAction.Created, Description = $"Demo invoice {invoice.InvoiceNumber} created for UI evaluation.", PerformedByUserId = owner.Id, PerformedAt = invoice.CreatedAt });
                if (invoice.Status is not InvoiceStatus.Draft) db.DocumentApprovals.Add(new DocumentApproval { DocumentType = DocumentType.SalesInvoice, DocumentId = invoice.Id, DocumentNumber = invoice.InvoiceNumber, RevisionNumber = invoice.RevisionNumber, Action = invoice.Status == InvoiceStatus.Rejected ? ApprovalAction.Rejected : ApprovalAction.Approved, PreviousStatus = "SubmittedForApproval", NewStatus = invoice.Status.ToString(), SubmittedByUserId = owner.Id, ActionByUserId = owner.Id, SubmittedAt = invoice.SubmittedAt ?? invoice.CreatedAt, ActionAt = invoice.UpdatedAt ?? invoice.CreatedAt, Comments = "Development demo workflow entry.", RejectionReason = invoice.RejectionReason, FinancialTotal = invoice.GrandTotal, Currency = invoice.Currency, IsFinal = true, CreatedAt = invoice.CreatedAt });
            }
            await db.SaveChangesAsync();
        }

        private static void AddOrderLine(SalesOrder order, (string Name, SalesItemType Type, string Unit, decimal Price) item, decimal quantity, decimal discount, decimal tax, DateTime createdAt)
        {
            var subtotal = quantity * item.Price; var discountAmount = Math.Round(subtotal * discount / 100m, 2); var taxAmount = Math.Round((subtotal - discountAmount) * tax / 100m, 2);
            order.Items.Add(new SalesOrderItem { LineNumber = order.Items.Count + 1, DisplayOrder = order.Items.Count + 1, ItemType = item.Type, Description = item.Name, Quantity = quantity, UnitOfMeasure = item.Unit, UnitPrice = item.Price, DiscountPercentage = discount, DiscountAmount = discountAmount, TaxPercentage = tax, TaxAmount = taxAmount, LineSubtotal = subtotal, LineTotal = subtotal - discountAmount + taxAmount, ServiceDate = DateOnly.FromDateTime(createdAt), CreatedAt = createdAt });
        }
        private static void CalculateOrder(SalesOrder order) { order.Subtotal = order.Items.Sum(x => x.LineSubtotal); order.DiscountTotal = order.Items.Sum(x => x.DiscountAmount); order.TaxTotal = order.Items.Sum(x => x.TaxAmount); order.GrandTotal = order.Subtotal - order.DiscountTotal + order.TaxTotal; }
        private static void AddInvoiceLine(SalesInvoice invoice, (string Name, SalesItemType Type, string Unit, decimal Price) item, decimal quantity, decimal discount, decimal tax, DateOnly date)
        {
            var subtotal = quantity * item.Price; var discountAmount = Math.Round(subtotal * discount / 100m, 2); var taxAmount = Math.Round((subtotal - discountAmount) * tax / 100m, 2);
            invoice.Items.Add(new SalesInvoiceItem { LineNumber = invoice.Items.Count + 1, DisplayOrder = invoice.Items.Count + 1, ItemType = item.Type, Description = item.Name, Quantity = quantity, UnitOfMeasure = item.Unit, UnitPrice = item.Price, DiscountPercentage = discount, DiscountAmount = discountAmount, TaxPercentage = tax, TaxAmount = taxAmount, LineSubtotal = subtotal, LineTotal = subtotal - discountAmount + taxAmount, ServiceDate = date, CreatedAt = DateTime.UtcNow });
        }
        private static void CalculateInvoice(SalesInvoice invoice) { invoice.Subtotal = invoice.Items.Sum(x => x.LineSubtotal); invoice.DiscountTotal = invoice.Items.Sum(x => x.DiscountAmount); invoice.TaxTotal = invoice.Items.Sum(x => x.TaxAmount); invoice.GrandTotal = invoice.Subtotal - invoice.DiscountTotal + invoice.TaxTotal + invoice.AdjustmentAmount; invoice.OutstandingAmount = invoice.GrandTotal - invoice.PaidAmount; }

        private static async Task SeedHrAsync(ApplicationDbContext dbContext)
        {
            if (await dbContext.JobPostings.AnyAsync()) return;

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var users = await dbContext.Users.AsNoTracking().Take(3).OrderBy(user => user.Id).ToListAsync();
            var hiringManagerId = users.ElementAtOrDefault(0)?.Id;

            var coachPosting = new JobPosting
            {
                ReferenceNumber = "JOB-000001",
                JobTitle = "Senior Table Tennis Coach",
                Department = "Sports & Recreation",
                EmploymentType = EmploymentType.FullTime,
                WorkplaceType = WorkplaceType.OnSite,
                JobLocation = "Lahore, Pakistan",
                Vacancies = 2,
                MinimumSalary = 180000m,
                MaximumSalary = 260000m,
                Currency = "PKR",
                ExperienceRequired = "Senior (5-8 years)",
                MinimumEducation = "Bachelor's Degree",
                JobDescription = "We are looking for an experienced table tennis coach to lead our competitive training program. The coach will design training plans, prepare athletes for national tournaments, and mentor junior coaching staff while maintaining a safe and motivating training environment.",
                KeyResponsibilities = "Design and run group and one-on-one training sessions. Prepare athletes for provincial and national tournaments. Monitor player progress and adjust training plans. Mentor assistant coaches and supervise court usage.",
                Qualifications = "Minimum 5 years of coaching experience with a recognized table tennis federation. Proven track record of developing competitive players. Strong communication and leadership skills.",
                ApplicationDeadline = today.AddDays(30),
                Status = JobPostingStatus.Open,
                HiringManagerUserId = hiringManagerId,
                Benefits = "Health insurance, gym membership, performance bonus, and paid tournament travel.",
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = hiringManagerId
            };
            coachPosting.Skills.Add(new JobPostingSkill { Skill = "Table Tennis Coaching" });
            coachPosting.Skills.Add(new JobPostingSkill { Skill = "Player Development" });
            coachPosting.Skills.Add(new JobPostingSkill { Skill = "Team Leadership" });
            coachPosting.ScreeningQuestions.Add(new JobPostingScreeningQuestion { Question = "How many years have you coached competitive table tennis players?" });

            var accountantPosting = new JobPosting
            {
                ReferenceNumber = "JOB-000002",
                JobTitle = "Accounts Executive",
                Department = "Finance",
                EmploymentType = EmploymentType.FullTime,
                WorkplaceType = WorkplaceType.Hybrid,
                JobLocation = "Islamabad, Pakistan",
                Vacancies = 1,
                MinimumSalary = 120000m,
                MaximumSalary = 170000m,
                Currency = "PKR",
                ExperienceRequired = "Mid Level (3-5 years)",
                MinimumEducation = "Bachelor's Degree",
                JobDescription = "The Accounts Executive will manage day-to-day bookkeeping, sales invoicing, and reconciliation across the arena's sports and fitness operations. The role partners closely with the finance manager to keep month-end close accurate and on time.",
                KeyResponsibilities = "Post daily sales and membership receipts. Reconcile bank and payment gateway statements. Prepare sales invoices and follow up on receivables. Support month-end close and audit schedules.",
                Qualifications = "Bachelor's degree in finance or accounting. 3+ years of accounts experience, ideally in retail or services. Strong Excel skills and working knowledge of ERP systems.",
                ApplicationDeadline = today.AddDays(21),
                Status = JobPostingStatus.Open,
                HiringManagerUserId = users.ElementAtOrDefault(1)?.Id,
                Benefits = "Health insurance and annual performance bonus.",
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = hiringManagerId
            };
            accountantPosting.Skills.Add(new JobPostingSkill { Skill = "Bookkeeping" });
            accountantPosting.Skills.Add(new JobPostingSkill { Skill = "Reconciliation" });
            accountantPosting.Skills.Add(new JobPostingSkill { Skill = "Excel" });

            var frontDeskPosting = new JobPosting
            {
                ReferenceNumber = "JOB-000003",
                JobTitle = "Front Desk Receptionist",
                Department = "Operations",
                EmploymentType = EmploymentType.PartTime,
                WorkplaceType = WorkplaceType.OnSite,
                JobLocation = "Karachi, Pakistan",
                Vacancies = 3,
                Currency = "PKR",
                ExperienceRequired = "Entry Level (0-1 years)",
                MinimumEducation = "Intermediate / A Levels",
                JobDescription = "Be the friendly first point of contact for members and guests. The receptionist handles bookings, check-ins, payments, and general inquiries while keeping the front office organized and welcoming.",
                KeyResponsibilities = "Welcome guests and manage court check-ins. Handle phone and walk-in inquiries. Process daily payments and issue receipts. Keep reception and waiting areas tidy.",
                Qualifications = "Fluent in English and Urdu. Customer-service mindset and basic computer skills. Flexible availability including evenings and weekends.",
                ApplicationDeadline = today.AddDays(14),
                Status = JobPostingStatus.Draft,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = hiringManagerId
            };
            frontDeskPosting.Skills.Add(new JobPostingSkill { Skill = "Customer Service" });
            frontDeskPosting.Skills.Add(new JobPostingSkill { Skill = "Communication" });

            dbContext.JobPostings.AddRange(coachPosting, accountantPosting, frontDeskPosting);
            await dbContext.SaveChangesAsync();
        }

        private static async Task SeedSalesAsync(ApplicationDbContext dbContext)
        {
            if (await dbContext.SalesQuotations.AnyAsync()) return;
            var settings = await dbContext.SystemSettings.FirstOrDefaultAsync();
            var owner = await dbContext.Users.FirstOrDefaultAsync();
            var customer = await dbContext.Customers.FirstOrDefaultAsync(x => x.IsActive && !x.IsBlacklisted);
            if (settings == null || owner == null || customer == null) return;

            var opportunity = await dbContext.Opportunities.Include(x => x.Customer).FirstOrDefaultAsync(x => x.IsActive && x.Stage != OpportunityStage.Lost && x.CustomerId != null && x.Customer != null && x.Customer.IsActive && !x.Customer.IsBlacklisted);
            if (opportunity == null)
            {
                var lead = new Lead
                {
                    LeadNumber = "LEAD-009001",
                    CustomerId = customer.Id,
                    CustomerName = customer.FullName,
                    Email = customer.Email,
                    Phone = customer.PrimaryPhone,
                    Source = LeadSource.ExistingCustomer,
                    ServiceInterest = LeadServiceInterest.CorporateBooking,
                    Status = LeadStatus.Converted,
                    Priority = LeadPriority.High,
                    AssignedToUserId = owner.Id,
                    QualifiedAt = DateTime.UtcNow.AddDays(-4),
                    ConvertedAt = DateTime.UtcNow.AddDays(-3),
                    QualificationNotes = "Development sales seed opportunity.",
                    CreatedByUserId = owner.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    IsConverted = true,
                    IsActive = true
                };
                dbContext.Leads.Add(lead);
                await dbContext.SaveChangesAsync();
                opportunity = new Opportunity
                {
                    OpportunityNumber = "OPP-009001",
                    LeadId = lead.Id,
                    CustomerId = customer.Id,
                    Name = "Corporate Sports Package",
                    Type = OpportunityType.CorporatePackage,
                    Stage = OpportunityStage.ProposalPreparation,
                    ExpectedValue = 120000,
                    ProbabilityPercentage = 40,
                    ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                    CustomerRequirement = "Monthly corporate sports package with premium court access.",
                    ProposedSolution = "Court bundle, support desk and priority scheduling.",
                    AssignedToUserId = owner.Id,
                    CreatedByUserId = owner.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    IsActive = true
                };
                dbContext.Opportunities.Add(opportunity);
                await dbContext.SaveChangesAsync();
                lead.ConvertedOpportunityId = opportunity.Id;
                await dbContext.SaveChangesAsync();
            }

            var prefix = string.IsNullOrWhiteSpace(settings.QuotationPrefix) ? "SQ" : settings.QuotationPrefix.Trim().ToUpperInvariant();
            var quotation = new SalesQuotation
            {
                QuotationNumber = $"{prefix}-000001",
                OpportunityId = opportunity.Id,
                CustomerId = opportunity.CustomerId!.Value,
                AssignedToUserId = opportunity.AssignedToUserId ?? owner.Id,
                QuotationDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Math.Max(settings.DefaultQuotationValidityDays, 7))),
                Status = SalesQuotationStatus.Draft,
                Subject = $"{opportunity.Name} Proposal",
                Introduction = opportunity.CustomerRequirement,
                TermsAndConditions = "This quotation is valid until the stated date and subject to final availability.",
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CreatedByUserId = owner.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            quotation.Items.Add(new SalesQuotationItem
            {
                LineNumber = 1,
                ItemType = SalesItemType.CorporatePackage,
                Description = opportunity.ProposedSolution ?? opportunity.Name,
                Quantity = 1,
                UnitOfMeasure = "Package",
                UnitPrice = opportunity.ExpectedValue,
                DiscountPercentage = 0,
                TaxPercentage = settings.TaxPercentage,
                DisplayOrder = 1,
                CreatedAt = DateTime.UtcNow
            });
            foreach (var item in quotation.Items)
            {
                item.LineSubtotal = Math.Round(item.Quantity * item.UnitPrice, 2);
                item.DiscountAmount = Math.Round(item.LineSubtotal * item.DiscountPercentage / 100m, 2);
                item.TaxAmount = Math.Round((item.LineSubtotal - item.DiscountAmount) * item.TaxPercentage / 100m, 2);
                item.LineTotal = item.LineSubtotal - item.DiscountAmount + item.TaxAmount;
            }
            quotation.Subtotal = quotation.Items.Sum(x => x.LineSubtotal);
            quotation.LineDiscountTotal = quotation.Items.Sum(x => x.DiscountAmount);
            quotation.DiscountTotal = quotation.LineDiscountTotal + quotation.ManualDiscountAmount;
            quotation.TaxTotal = quotation.Items.Sum(x => x.TaxAmount);
            quotation.GrandTotal = quotation.Subtotal - quotation.DiscountTotal + quotation.TaxTotal;
            dbContext.SalesQuotations.Add(quotation);
            opportunity.QuotationCreated = true;
            await dbContext.SaveChangesAsync();
            opportunity.QuotationId = quotation.Id;
            dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = opportunity.Id, ActivityType = OpportunityActivityType.Proposal, Subject = "Quotation created", Description = $"Development quotation {quotation.QuotationNumber} created.", ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = owner.Id, IsCompleted = true });
            await dbContext.SaveChangesAsync();
        }

        private static async Task SeedCrmAsync(ApplicationDbContext dbContext)
        {
            if (await dbContext.Leads.AnyAsync())
            {
                return;
            }

            var owner = await dbContext.Users.FirstOrDefaultAsync();
            var sports = await dbContext.Sports.ToListAsync();
            var customers = await dbContext.Customers.OrderBy(x => x.Id).Take(10).ToListAsync();
            if (owner == null) return;

            var now = DateTime.UtcNow;
            var leads = new List<Lead>
            {
                new() { LeadNumber = "LEAD-000001", CustomerId = customers.ElementAtOrDefault(0)?.Id, CustomerName = customers.ElementAtOrDefault(0)?.FullName ?? "Ayaan Khan", Email = customers.ElementAtOrDefault(0)?.Email, Phone = customers.ElementAtOrDefault(0)?.PrimaryPhone, Source = LeadSource.ExistingCustomer, ServiceInterest = LeadServiceInterest.Membership, Status = LeadStatus.Contacted, Priority = LeadPriority.Medium, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddDays(1), InquirySubject = "Premium membership upgrade", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-3), IsActive = true },
                new() { LeadNumber = "LEAD-000002", CustomerId = customers.ElementAtOrDefault(1)?.Id, CustomerName = customers.ElementAtOrDefault(1)?.FullName ?? "Zara Malik", Email = customers.ElementAtOrDefault(1)?.Email, Phone = customers.ElementAtOrDefault(1)?.PrimaryPhone, Source = LeadSource.ExistingCustomer, ServiceInterest = LeadServiceInterest.CourtBooking, Status = LeadStatus.Qualified, Priority = LeadPriority.High, AssignedToUserId = owner.Id, QualifiedAt = now.AddDays(-1), QualificationNotes = "Wants recurring weekend padel slots.", InquirySubject = "Weekend padel package", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-9), IsActive = true },
                new() { LeadNumber = "LEAD-000003", CustomerId = customers.ElementAtOrDefault(2)?.Id, CustomerName = customers.ElementAtOrDefault(2)?.FullName ?? "Bilal Raza", Email = customers.ElementAtOrDefault(2)?.Email, Phone = customers.ElementAtOrDefault(2)?.PrimaryPhone, OrganizationName = customers.ElementAtOrDefault(2)?.OrganizationName, Source = LeadSource.ExistingCustomer, ServiceInterest = LeadServiceInterest.CorporateBooking, Status = LeadStatus.Qualified, Priority = LeadPriority.High, AssignedToUserId = owner.Id, QualifiedAt = now.AddDays(-2), QualificationNotes = "Corporate event package likely.", InquirySubject = "Corporate sports family day", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-10), IsActive = true },
                new() { LeadNumber = "LEAD-000004", CustomerName = "Saad Farooq", Email = "saad.farooq@gamehub.com", Phone = "03005550101", Source = LeadSource.WebsiteInquiry, ServiceInterest = LeadServiceInterest.CourtBooking, SportId = sports.FirstOrDefault()?.Id, Status = LeadStatus.New, Priority = LeadPriority.High, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddHours(6), InquirySubject = "Evening padel booking", InquiryDetails = "Looking for recurring evening court availability.", IsWebsiteInquiry = true, PublicReferenceNumber = "INQ-100001", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-1), IsActive = true },
                new() { LeadNumber = "LEAD-000005", CustomerName = "Mehwish Ali", Email = "mehwish.ali@lahoretechhub.com", Phone = "03005550103", WhatsAppNumber = "03005550103", OrganizationName = "Lahore Tech Hub", Source = LeadSource.Corporate, ServiceInterest = LeadServiceInterest.CorporateBooking, Status = LeadStatus.Contacted, Priority = LeadPriority.Urgent, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddHours(8), InquirySubject = "Corporate sports evening", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-7), IsActive = true },
                new() { LeadNumber = "LEAD-000006", CustomerId = customers.ElementAtOrDefault(3)?.Id, CustomerName = customers.ElementAtOrDefault(3)?.FullName ?? "Hina Shah", Email = customers.ElementAtOrDefault(3)?.Email, Phone = customers.ElementAtOrDefault(3)?.PrimaryPhone, Source = LeadSource.ExistingCustomer, ServiceInterest = LeadServiceInterest.Coaching, Status = LeadStatus.FollowUp, Priority = LeadPriority.Medium, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddHours(-4), InquirySubject = "Badminton coaching enquiry", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-4), IsActive = true },
                new() { LeadNumber = "LEAD-000007", CustomerName = "Hamza Noor", Email = "hamza.noor@gamehub.com", Phone = "03005550107", Source = LeadSource.Phone, ServiceInterest = LeadServiceInterest.Tournament, Status = LeadStatus.FollowUp, Priority = LeadPriority.High, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddDays(2), InquirySubject = "Weekend tournament", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-5), IsActive = true },
                new() { LeadNumber = "LEAD-000008", CustomerName = "Nimra Sheikh", Email = "nimra.sheikh@gamehub.com", Phone = "03005550108", Source = LeadSource.SocialMedia, ServiceInterest = LeadServiceInterest.Coaching, Status = LeadStatus.Disqualified, Priority = LeadPriority.Low, AssignedToUserId = owner.Id, DisqualifiedAt = now.AddDays(-1), DisqualificationReason = "Outside current coaching scope.", InquirySubject = "Tennis coaching", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-8), IsActive = true },
                new() { LeadNumber = "LEAD-000009", CustomerName = "Usman Tariq", Email = "usman.tariq@skylinebuilders.com", Phone = "03005550109", OrganizationName = "Skyline Builders", Source = LeadSource.WebsiteInquiry, ServiceInterest = LeadServiceInterest.GeneralInquiry, Status = LeadStatus.New, Priority = LeadPriority.Medium, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddHours(12), InquirySubject = "Arena corporate membership", IsWebsiteInquiry = true, PublicReferenceNumber = "INQ-100002", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-2), IsActive = true },
                new() { LeadNumber = "LEAD-000010", CustomerId = customers.ElementAtOrDefault(8)?.Id, CustomerName = customers.ElementAtOrDefault(8)?.FullName ?? "Hassan Ali", Email = customers.ElementAtOrDefault(8)?.Email, Phone = customers.ElementAtOrDefault(8)?.PrimaryPhone, Source = LeadSource.ExistingCustomer, ServiceInterest = LeadServiceInterest.Membership, Status = LeadStatus.New, Priority = LeadPriority.Medium, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddDays(3), InquirySubject = "Student membership enquiry", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-6), IsActive = true }
            };

            dbContext.Leads.AddRange(leads);
            await dbContext.SaveChangesAsync();

            foreach (var lead in leads)
            {
                dbContext.LeadActivities.Add(new LeadActivity { LeadId = lead.Id, ActivityType = LeadActivityType.Note, Subject = "Lead created", Description = "Development CRM seed activity.", ActivityDate = lead.CreatedAt, CreatedAt = lead.CreatedAt, CreatedByUserId = owner.Id, IsCompleted = true });
                if (lead.NextFollowUpAt.HasValue)
                {
                    dbContext.LeadActivities.Add(new LeadActivity { LeadId = lead.Id, ActivityType = LeadActivityType.FollowUp, Subject = "Follow up with customer", ActivityDate = now, FollowUpDueAt = lead.NextFollowUpAt, CreatedAt = now, CreatedByUserId = owner.Id, IsCompleted = false });
                }
            }

            var qualified = leads.Where(x => x.Status == LeadStatus.Qualified).ToList();
            if (qualified.Count >= 2)
            {
                var first = qualified[0];
                var second = qualified[1];
                var opportunities = new List<Opportunity>
                {
                    new() { OpportunityNumber = "OPP-000001", LeadId = first.Id, CustomerId = first.CustomerId, Name = "Corporate Team Sports Package", Type = OpportunityType.CorporatePackage, Stage = OpportunityStage.ProposalPreparation, ExpectedValue = 180000, ProbabilityPercentage = 40, ExpectedCloseDate = DateOnly.FromDateTime(now.AddDays(18)), CustomerRequirement = "Monthly corporate sports booking.", ProposedSolution = "Padel and football package with priority slots.", AssignedToUserId = owner.Id, CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-1), IsActive = true },
                    new() { OpportunityNumber = "OPP-000002", LeadId = second.Id, CustomerId = second.CustomerId, Name = "Family Day Event Package", Type = OpportunityType.Event, Stage = OpportunityStage.Negotiation, ExpectedValue = 95000, ProbabilityPercentage = 80, ExpectedCloseDate = DateOnly.FromDateTime(now.AddDays(7)), CustomerRequirement = "One-day family sports event.", ProposedSolution = "Court bundle plus support desk.", AssignedToUserId = owner.Id, CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-2), IsActive = true }
                };
                dbContext.Opportunities.AddRange(opportunities);
                first.IsConverted = true; first.Status = LeadStatus.Converted; first.ConvertedAt = now.AddDays(-1);
                second.IsConverted = true; second.Status = LeadStatus.Converted; second.ConvertedAt = now.AddDays(-2);
                await dbContext.SaveChangesAsync();
                first.ConvertedOpportunityId = opportunities[0].Id;
                second.ConvertedOpportunityId = opportunities[1].Id;
                foreach (var opp in opportunities)
                {
                    dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = opp.Id, ActivityType = OpportunityActivityType.Note, Subject = "Opportunity created", ActivityDate = opp.CreatedAt, CreatedAt = opp.CreatedAt, CreatedByUserId = owner.Id, IsCompleted = true });
                }
                await dbContext.SaveChangesAsync();
            }
        }

        private static async Task SeedBookingsAsync(ApplicationDbContext dbContext)
        {
            if (await dbContext.Bookings.AnyAsync())
            {
                return;
            }

            var userId = await dbContext.Users.Select(user => user.Id).FirstOrDefaultAsync();
            var settings = await dbContext.SystemSettings.FirstOrDefaultAsync();
            var customers = await dbContext.Customers.Where(customer => customer.IsActive && !customer.IsBlacklisted).OrderBy(customer => customer.Id).Take(4).ToListAsync();
            var courts = await dbContext.Courts.Include(court => court.Sport).Include(court => court.Facility).Where(court => court.IsActive && court.Status == CourtStatus.Available).OrderBy(court => court.Id).Take(4).ToListAsync();
            if (customers.Count < 2 || courts.Count < 2)
            {
                return;
            }

            var prefix = string.IsNullOrWhiteSpace(settings?.BookingPrefix) ? "BKG" : settings.BookingPrefix.Trim().ToUpperInvariant();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var seedItems = new[]
            {
                new { Date = today, Start = new TimeOnly(8, 0), Status = BookingStatus.Confirmed, Customer = customers[0], Court = courts[0] },
                new { Date = today, Start = new TimeOnly(10, 0), Status = BookingStatus.Pending, Customer = customers[1], Court = courts[1] },
                new { Date = today.AddDays(-1), Start = new TimeOnly(16, 0), Status = BookingStatus.Completed, Customer = customers[0], Court = courts[1] },
                new { Date = today.AddDays(1), Start = new TimeOnly(18, 0), Status = BookingStatus.Confirmed, Customer = customers[2 % customers.Count], Court = courts[0] },
                new { Date = today.AddDays(2), Start = new TimeOnly(12, 0), Status = BookingStatus.Cancelled, Customer = customers[3 % customers.Count], Court = courts[1] }
            };

            var counter = 1;
            foreach (var item in seedItems)
            {
                var pricing = await dbContext.CourtPricings.Where(price => price.CourtId == item.Court.Id && price.IsActive && price.StartTime <= item.Start && price.EndTime >= item.Start.AddMinutes(60)).OrderBy(price => price.Price).FirstOrDefaultAsync();
                var baseAmount = pricing?.Price ?? 3000;
                var tax = Math.Round(baseAmount * (settings?.TaxPercentage ?? 0) / 100, 2);
                var total = baseAmount + tax;
                dbContext.Bookings.Add(new Booking
                {
                    BookingNumber = $"{prefix}-{counter:000000}",
                    CustomerId = item.Customer.Id,
                    SportId = item.Court.SportId,
                    FacilityId = item.Court.FacilityId,
                    CourtId = item.Court.Id,
                    BookingDate = item.Date,
                    StartTime = item.Start,
                    EndTime = item.Start.AddMinutes(60),
                    DurationMinutes = 60,
                    PlayerCount = Math.Min(4, Math.Max(1, item.Court.Capacity)),
                    Status = item.Status,
                    PaymentStatus = item.Status == BookingStatus.Cancelled ? PaymentStatus.Unpaid : item.Status == BookingStatus.Completed ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid,
                    Source = counter % 2 == 0 ? BookingSource.Phone : BookingSource.Reception,
                    BaseAmount = baseAmount,
                    TaxAmount = tax,
                    TotalAmount = total,
                    PaidAmount = item.Status == BookingStatus.Completed ? total : total / 2,
                    BalanceAmount = item.Status == BookingStatus.Completed ? 0 : total / 2,
                    IsPeakRate = pricing?.IsPeakRate ?? false,
                    IsMemberBooking = item.Customer.IsMember,
                    IsActive = item.Status != BookingStatus.Cancelled,
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow.AddDays(-counter),
                    ConfirmedAt = item.Status is BookingStatus.Confirmed or BookingStatus.Completed ? DateTime.UtcNow.AddDays(-counter).AddHours(1) : null,
                    CompletedAt = item.Status == BookingStatus.Completed ? DateTime.UtcNow.AddDays(-1).AddHours(2) : null,
                    CancelledAt = item.Status == BookingStatus.Cancelled ? DateTime.UtcNow.AddDays(-1) : null,
                    CustomerNotes = "Development sample booking."
                });
                counter++;
            }

            await dbContext.SaveChangesAsync();
        }

        private static async Task SeedMembershipsAsync(ApplicationDbContext dbContext)
        {
            if (!await dbContext.MembershipPlans.AnyAsync())
            {
                dbContext.MembershipPlans.AddRange(
                    new MembershipPlan { Name = "Basic", Code = "BASIC", Description = "Entry level arena membership.", PlanType = MembershipPlanType.Basic, DurationMonths = 1, JoiningFee = 3000, RenewalFee = 2500, DiscountPercentage = 5, IncludedBookingHours = 4, PriorityBookingDays = 2, AllowPeakHours = false, AllowOffPeakHours = true, AllowedSportCodes = "ALL", Benefits = "Off-peak booking access, member support", DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new MembershipPlan { Name = "Premium", Code = "PREMIUM", Description = "Premium access for frequent players.", PlanType = MembershipPlanType.Premium, DurationMonths = 3, JoiningFee = 12000, RenewalFee = 10000, DiscountPercentage = 12, IncludedBookingHours = 16, PriorityBookingDays = 7, AllowPeakHours = true, AllowOffPeakHours = true, AllowedSportCodes = "ALL", Benefits = "Peak access, priority booking, lounge benefits", DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new MembershipPlan { Name = "Student", Code = "STUDENT", Description = "Discounted student membership.", PlanType = MembershipPlanType.Student, DurationMonths = 1, JoiningFee = 2000, RenewalFee = 1800, DiscountPercentage = 15, IncludedBookingHours = 6, PriorityBookingDays = 3, AllowPeakHours = false, AllowOffPeakHours = true, AllowedSportCodes = "BDM,PAD", Benefits = "Student rates, off-peak sports access", DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new MembershipPlan { Name = "Family", Code = "FAMILY", Description = "Shared family sports access.", PlanType = MembershipPlanType.Family, DurationMonths = 6, JoiningFee = 22000, RenewalFee = 19000, DiscountPercentage = 10, IncludedBookingHours = 30, PriorityBookingDays = 10, AllowPeakHours = true, AllowOffPeakHours = true, AllowedSportCodes = "ALL", Benefits = "Family booking benefits, priority access", DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow });
                await dbContext.SaveChangesAsync();
            }

            if (!await dbContext.CustomerMemberships.AnyAsync())
            {
                var premium = await dbContext.MembershipPlans.FirstOrDefaultAsync(plan => plan.Code == "PREMIUM");
                var student = await dbContext.MembershipPlans.FirstOrDefaultAsync(plan => plan.Code == "STUDENT");
                var customers = await dbContext.Customers.OrderBy(customer => customer.Id).Take(4).ToListAsync();
                if (premium != null && student != null && customers.Count >= 2)
                {
                    var first = customers[0];
                    var second = customers[1];
                    var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20));
                    var expiry = start.AddMonths(premium.DurationMonths);
                    dbContext.CustomerMemberships.Add(new CustomerMembership { MembershipNumber = "MEM-000001", CustomerId = first.Id, MembershipPlanId = premium.Id, StartDate = start, ExpiryDate = expiry, Status = MembershipStatus.Active, JoiningFee = premium.JoiningFee, RenewalFee = premium.RenewalFee, DiscountPercentage = premium.DiscountPercentage, IncludedBookingHours = premium.IncludedBookingHours, UsedBookingHours = 4, AutoRenew = true, IsActive = true, CreatedAt = DateTime.UtcNow });
                    first.IsMember = true; first.MembershipNumber = "MEM-000001"; first.MembershipStartDate = start; first.MembershipExpiryDate = expiry;

                    start = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2));
                    expiry = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));
                    dbContext.CustomerMemberships.Add(new CustomerMembership { MembershipNumber = "MEM-000002", CustomerId = second.Id, MembershipPlanId = student.Id, StartDate = start, ExpiryDate = expiry, Status = MembershipStatus.Expired, JoiningFee = student.JoiningFee, RenewalFee = student.RenewalFee, DiscountPercentage = student.DiscountPercentage, IncludedBookingHours = student.IncludedBookingHours, UsedBookingHours = 6, AutoRenew = false, IsActive = false, CreatedAt = DateTime.UtcNow });
                    second.IsMember = false; second.MembershipNumber = "MEM-000002"; second.MembershipStartDate = start; second.MembershipExpiryDate = expiry;
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        private static async Task SeedCustomersAsync(ApplicationDbContext dbContext)
        {
            if (await dbContext.Customers.AnyAsync())
            {
                return;
            }

            dbContext.Customers.AddRange(
                new Customer
                {
                    CustomerCode = "CUS-000001",
                    FirstName = "Ayaan",
                    LastName = "Khan",
                    PreferredName = "Ayaan",
                    Gender = Gender.Male,
                    PrimaryPhone = "03001234567",
                    WhatsAppNumber = "03001234567",
                    Email = "ayaan.khan@gamehub.com",
                    NationalIdNumber = "3520212345671",
                    City = "Lahore",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Individual,
                    CustomerSource = CustomerSource.Referral,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "PAD,BDM",
                    ReceiveBookingReminders = true,
                    LoyaltyPoints = 180,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-45)
                },
                new Customer
                {
                    CustomerCode = "CUS-000002",
                    FirstName = "Zara",
                    LastName = "Malik",
                    Gender = Gender.Female,
                    PrimaryPhone = "03011234567",
                    Email = "zara.malik@gamehub.com",
                    NationalIdNumber = "6110187654321",
                    City = "Islamabad",
                    Country = "Pakistan",
                    CustomerType = CustomerType.VIP,
                    CustomerSource = CustomerSource.SocialMedia,
                    PreferredContactMethod = PreferredContactMethod.Email,
                    PreferredSportCodes = "PAD",
                    IsMember = true,
                    MembershipNumber = "GH-MEM-1001",
                    MembershipStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)),
                    MembershipExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(10)),
                    AllowCredit = true,
                    CreditLimit = 50000,
                    LoyaltyPoints = 1250,
                    ReceiveBookingReminders = true,
                    ReceiveMarketingMessages = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-80)
                },
                new Customer
                {
                    CustomerCode = "CUS-000003",
                    FirstName = "Bilal",
                    LastName = "Raza",
                    Gender = Gender.Male,
                    PrimaryPhone = "03021234567",
                    Email = "bilal.raza@northstarsports.com",
                    NationalIdNumber = "4210176543219",
                    City = "Karachi",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Corporate,
                    CustomerSource = CustomerSource.Corporate,
                    OrganizationName = "Northstar Sports Club",
                    Occupation = "Sports Coordinator",
                    PreferredContactMethod = PreferredContactMethod.Phone,
                    PreferredSportCodes = "FTB,CRK",
                    AllowCredit = true,
                    CreditLimit = 100000,
                    OutstandingBalance = 12000,
                    LoyaltyPoints = 500,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-110)
                },
                new Customer
                {
                    CustomerCode = "CUS-000004",
                    FirstName = "Hina",
                    LastName = "Shah",
                    Gender = Gender.Female,
                    PrimaryPhone = "03031234567",
                    Email = "hina.shah@gamehub.com",
                    NationalIdNumber = "3520198765432",
                    City = "Lahore",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Student,
                    CustomerSource = CustomerSource.WalkIn,
                    PreferredContactMethod = PreferredContactMethod.SMS,
                    PreferredSportCodes = "BDM",
                    IsMember = true,
                    MembershipNumber = "GH-MEM-1002",
                    MembershipStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-8)),
                    MembershipExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                    LoyaltyPoints = 60,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-140)
                },
                new Customer
                {
                    CustomerCode = "CUS-000005",
                    FirstName = "Danish",
                    LastName = "Qureshi",
                    Gender = Gender.Male,
                    PrimaryPhone = "03041234567",
                    Email = "danish.qureshi@gamehub.com",
                    NationalIdNumber = "3310165432198",
                    City = "Faisalabad",
                    Country = "Pakistan",
                    CustomerType = CustomerType.WalkIn,
                    CustomerSource = CustomerSource.PhoneCall,
                    PreferredContactMethod = PreferredContactMethod.Phone,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-25)
                },
                new Customer
                {
                    CustomerCode = "CUS-000006",
                    FirstName = "Fatima",
                    LastName = "Sheikh",
                    PreferredName = "Fatima",
                    Gender = Gender.Female,
                    PrimaryPhone = "03051234567",
                    WhatsAppNumber = "03051234567",
                    Email = "fatima.sheikh@gamehub.com",
                    NationalIdNumber = "3740181234567",
                    AddressLine1 = "House 5, Bahria Town Phase 4",
                    City = "Rawalpindi",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Family,
                    CustomerSource = CustomerSource.Referral,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "PAD,FTB",
                    LoyaltyPoints = 320,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-70)
                },
                new Customer
                {
                    CustomerCode = "CUS-000007",
                    FirstName = "Usman",
                    LastName = "Chaudhry",
                    Gender = Gender.Male,
                    PrimaryPhone = "03061234567",
                    Email = "usman.chaudhry@vertextech.com",
                    NationalIdNumber = "3520176543210",
                    AddressLine1 = "Office 302, Arfa Software Technology Park",
                    City = "Lahore",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Corporate,
                    CustomerSource = CustomerSource.Website,
                    OrganizationName = "Vertex Technologies",
                    Occupation = "Procurement Manager",
                    PreferredContactMethod = PreferredContactMethod.Email,
                    PreferredSportCodes = "FTB,BDM",
                    AllowCredit = true,
                    CreditLimit = 150000,
                    OutstandingBalance = 0,
                    LoyaltyPoints = 700,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-60)
                },
                new Customer
                {
                    CustomerCode = "CUS-000008",
                    FirstName = "Ayesha",
                    LastName = "Nawaz",
                    PreferredName = "Ayesha",
                    Gender = Gender.Female,
                    PrimaryPhone = "03071234567",
                    WhatsAppNumber = "03071234567",
                    Email = "ayesha.nawaz@gamehub.com",
                    NationalIdNumber = "1730165432109",
                    AddressLine1 = "Flat 3-B, University Road",
                    City = "Peshawar",
                    Country = "Pakistan",
                    CustomerType = CustomerType.VIP,
                    CustomerSource = CustomerSource.Referral,
                    PreferredContactMethod = PreferredContactMethod.Email,
                    PreferredSportCodes = "BDM,CRK",
                    AllowCredit = true,
                    CreditLimit = 40000,
                    LoyaltyPoints = 980,
                    ReceiveBookingReminders = true,
                    ReceiveMarketingMessages = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-50)
                },
                new Customer
                {
                    CustomerCode = "CUS-000009",
                    FirstName = "Hassan",
                    LastName = "Ali",
                    Gender = Gender.Male,
                    PrimaryPhone = "03081234567",
                    WhatsAppNumber = "03081234567",
                    Email = "hassan.ali@gamehub.com",
                    NationalIdNumber = "3630154321098",
                    AddressLine1 = "House 18, Gulgasht Colony",
                    City = "Multan",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Student,
                    CustomerSource = CustomerSource.WalkIn,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "PAD",
                    LoyaltyPoints = 140,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-35)
                },
                new Customer
                {
                    CustomerCode = "CUS-000010",
                    FirstName = "Sana",
                    LastName = "Yousaf",
                    PreferredName = "Sana",
                    Gender = Gender.Female,
                    PrimaryPhone = "03091234567",
                    WhatsAppNumber = "03091234567",
                    Email = "sana.yousaf@gamehub.com",
                    NationalIdNumber = "5440143210987",
                    AddressLine1 = "House 22, Jinnah Road",
                    City = "Quetta",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Family,
                    CustomerSource = CustomerSource.SocialMedia,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "FTB,BDM",
                    LoyaltyPoints = 260,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                });

            await dbContext.SaveChangesAsync();
        }

        private static async Task SeedArenaSetupAsync(ApplicationDbContext dbContext)
        {
            if (!await dbContext.Sports.AnyAsync())
            {
                dbContext.Sports.AddRange(
                    new Sport { Name = "Padel", Code = "PAD", Description = "Premium padel courts.", IconClass = "bi-dribbble", AccentColor = "#d6a950", DefaultDurationMinutes = 60, DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Sport { Name = "Badminton", Code = "BDM", Description = "Indoor badminton courts.", IconClass = "bi-feather", AccentColor = "#2ba061", DefaultDurationMinutes = 60, DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Sport { Name = "Football", Code = "FTB", Description = "Football turf bookings.", IconClass = "bi-trophy", AccentColor = "#4f7aa8", DefaultDurationMinutes = 90, DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Sport { Name = "Cricket", Code = "CRK", Description = "Cricket nets and practice lanes.", IconClass = "bi-bullseye", AccentColor = "#c95245", DefaultDurationMinutes = 60, DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow });
                await dbContext.SaveChangesAsync();
            }

            if (!await dbContext.Facilities.AnyAsync())
            {
                dbContext.Facilities.AddRange(
                    new Facility { Name = "Indoor Arena", Code = "IND", Type = FacilityType.Indoor, LocationLabel = "Main Building", DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Facility { Name = "Outdoor Arena", Code = "OUT", Type = FacilityType.Outdoor, LocationLabel = "Outdoor Zone", DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Facility { Name = "Block A", Code = "BLK-A", Type = FacilityType.Block, LocationLabel = "North Wing", DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Facility { Name = "Block B", Code = "BLK-B", Type = FacilityType.Block, LocationLabel = "South Wing", DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow });
                await dbContext.SaveChangesAsync();
            }

            if (!await dbContext.Courts.AnyAsync())
            {
                var sports = await dbContext.Sports.ToDictionaryAsync(item => item.Code);
                var facilities = await dbContext.Facilities.ToDictionaryAsync(item => item.Code);
                dbContext.Courts.AddRange(
                    new Court { Name = "Padel Court 01", Code = "PAD-01", SportId = sports["PAD"].Id, FacilityId = facilities["OUT"].Id, Status = CourtStatus.Available, Capacity = 4, DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Court { Name = "Padel Court 02", Code = "PAD-02", SportId = sports["PAD"].Id, FacilityId = facilities["OUT"].Id, Status = CourtStatus.Available, Capacity = 4, DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Court { Name = "Badminton Court 01", Code = "BDM-01", SportId = sports["BDM"].Id, FacilityId = facilities["IND"].Id, Status = CourtStatus.Available, Capacity = 4, DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Court { Name = "Football Turf 01", Code = "FTB-01", SportId = sports["FTB"].Id, FacilityId = facilities["OUT"].Id, Status = CourtStatus.Maintenance, Capacity = 14, DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new Court { Name = "Cricket Nets 01", Code = "CRK-01", SportId = sports["CRK"].Id, FacilityId = facilities["BLK-A"].Id, Status = CourtStatus.Available, Capacity = 6, DisplayOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow });
                await dbContext.SaveChangesAsync();
            }

            if (!await dbContext.CourtPricings.AnyAsync())
            {
                var courts = await dbContext.Courts.ToListAsync();
                foreach (var court in courts.Take(4))
                {
                    dbContext.CourtPricings.Add(new CourtPricing { CourtId = court.Id, Name = "Standard Weekday Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), DurationMinutes = 60, Price = 3000, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow });
                    dbContext.CourtPricings.Add(new CourtPricing { CourtId = court.Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 60, Price = 4500, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow });
                }
                await dbContext.SaveChangesAsync();
            }

            if (!await dbContext.CourtSchedules.AnyAsync())
            {
                var settings = await dbContext.SystemSettings.FirstOrDefaultAsync();
                var courts = await dbContext.Courts.ToListAsync();
                foreach (var court in courts)
                {
                    foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
                    {
                        dbContext.CourtSchedules.Add(new CourtSchedule
                        {
                            CourtId = court.Id,
                            DayOfWeek = day,
                            OpeningTime = settings?.OpeningTime ?? new TimeOnly(6, 0),
                            ClosingTime = settings?.ClosingTime ?? new TimeOnly(23, 59),
                            SlotDurationMinutes = settings?.SlotDurationMinutes ?? 60,
                            BufferMinutes = settings?.BufferMinutes ?? 10,
                            IsClosed = false,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                await dbContext.SaveChangesAsync();
            }
        }

        public static async Task SeedMasterDataAndCustomersAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await SeedSportsAsync(dbContext);
            await SeedFacilitiesAsync(dbContext);
            await SeedCourtsAsync(dbContext);
            await SeedCourtPricingsAsync(dbContext);
            await SeedCourtSchedulesAsync(dbContext);
            await SeedCustomerRecordsAsync(dbContext);
            await HrDemoDataSeeder.SeedAsync(dbContext, scope.ServiceProvider);
        }

        private static async Task SeedSportsAsync(ApplicationDbContext db)
        {
            var sports = new[]
            {
                new Sport { Name = "Cricket", Code = "CRK", Description = "Cricket nets and practice lanes.", IconClass = "bi-bullseye", AccentColor = "#c95245", DefaultDurationMinutes = 60, DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Sport { Name = "Football", Code = "FTB", Description = "Full-size and small-sided football turf.", IconClass = "bi-trophy", AccentColor = "#4f7aa8", DefaultDurationMinutes = 90, DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Sport { Name = "Badminton", Code = "BDM", Description = "Indoor badminton courts.", IconClass = "bi-feather", AccentColor = "#2ba061", DefaultDurationMinutes = 60, DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Sport { Name = "Padel", Code = "PAD", Description = "Glass-wall padel courts.", IconClass = "bi-dribbble", AccentColor = "#d6a950", DefaultDurationMinutes = 60, DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Sport { Name = "Tennis", Code = "TNS", Description = "Hard and clay tennis courts.", IconClass = "bi-tennisball", AccentColor = "#6c757d", DefaultDurationMinutes = 90, DisplayOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Sport { Name = "Squash", Code = "SQU", Description = "Indoor squash courts.", IconClass = "bi-squash", AccentColor = "#6f42c1", DefaultDurationMinutes = 45, DisplayOrder = 6, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Sport { Name = "Table Tennis", Code = "TTN", Description = "Multi-table table tennis zone.", IconClass = "bi-table", AccentColor = "#20c997", DefaultDurationMinutes = 30, DisplayOrder = 7, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Sport { Name = "Basketball", Code = "BKB", Description = "Outdoor basketball court.", IconClass = "bi-basketball", AccentColor = "#0d6efd", DefaultDurationMinutes = 60, DisplayOrder = 8, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Sport { Name = "Volleyball", Code = "VOL", Description = "Sand and indoor volleyball.", IconClass = "bi-volleyball", AccentColor = "#fd7e14", DefaultDurationMinutes = 60, DisplayOrder = 9, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Sport { Name = "Swimming", Code = "SWM", Description = "Indoor swimming lanes.", IconClass = "bi-swimming", AccentColor = "#0dcaf0", DefaultDurationMinutes = 60, DisplayOrder = 10, IsActive = true, CreatedAt = DateTime.UtcNow }
            };

            foreach (var sport in sports)
            {
                if (!await db.Sports.AnyAsync(s => s.Code == sport.Code))
                {
                    db.Sports.Add(sport);
                }
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedFacilitiesAsync(ApplicationDbContext db)
        {
            var facilities = new[]
            {
                new Facility { Name = "Main Indoor Arena", Code = "IND-A", Type = FacilityType.Indoor, LocationLabel = "Main Building", DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Facility { Name = "Outdoor Sports Complex", Code = "OUT-B", Type = FacilityType.Outdoor, LocationLabel = "East Grounds", DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Facility { Name = "North Block", Code = "BLK-N", Type = FacilityType.Block, LocationLabel = "North Wing", DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Facility { Name = "South Block", Code = "BLK-S", Type = FacilityType.Block, LocationLabel = "South Wing", DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Facility { Name = "East Pavilion", Code = "EAS-P", Type = FacilityType.Mixed, LocationLabel = "Pavilion Block", DisplayOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Facility { Name = "West Complex", Code = "WES-C", Type = FacilityType.Outdoor, LocationLabel = "West Grounds", DisplayOrder = 6, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Facility { Name = "Central Fitness Hub", Code = "CTR-H", Type = FacilityType.Indoor, LocationLabel = "Central Block", DisplayOrder = 7, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Facility { Name = "Premium Clubhouse", Code = "PRM-C", Type = FacilityType.Mixed, LocationLabel = "Clubhouse", DisplayOrder = 8, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Facility { Name = "Riverside Grounds", Code = "RIV-G", Type = FacilityType.Outdoor, LocationLabel = "Riverside", DisplayOrder = 9, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Facility { Name = "Summit Arena", Code = "SUM-A", Type = FacilityType.Indoor, LocationLabel = "Summit Tower", DisplayOrder = 10, IsActive = true, CreatedAt = DateTime.UtcNow }
            };

            foreach (var facility in facilities)
            {
                if (!await db.Facilities.AnyAsync(f => f.Code == facility.Code))
                {
                    db.Facilities.Add(facility);
                }
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedCourtsAsync(ApplicationDbContext db)
        {
            var sports = await db.Sports.ToDictionaryAsync(s => s.Code);
            var facilities = await db.Facilities.ToDictionaryAsync(f => f.Code);

            var courts = new[]
            {
                new Court { Name = "Cricket Nets North 01", Code = "CRK-N1", SportId = sports["CRK"].Id, FacilityId = facilities["BLK-N"].Id, Status = CourtStatus.Available, Capacity = 6, DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Cricket Nets North 02", Code = "CRK-N2", SportId = sports["CRK"].Id, FacilityId = facilities["BLK-N"].Id, Status = CourtStatus.Available, Capacity = 6, DisplayOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Football Turf East 01", Code = "FTB-E1", SportId = sports["FTB"].Id, FacilityId = facilities["OUT-B"].Id, Status = CourtStatus.Available, Capacity = 14, DisplayOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Football Turf East 02", Code = "FTB-E2", SportId = sports["FTB"].Id, FacilityId = facilities["OUT-B"].Id, Status = CourtStatus.Available, Capacity = 10, DisplayOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Badminton Court Indoor 01", Code = "BDM-I1", SportId = sports["BDM"].Id, FacilityId = facilities["IND-A"].Id, Status = CourtStatus.Available, Capacity = 4, DisplayOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Badminton Court Indoor 02", Code = "BDM-I2", SportId = sports["BDM"].Id, FacilityId = facilities["IND-A"].Id, Status = CourtStatus.Available, Capacity = 4, DisplayOrder = 6, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Badminton Court Indoor 03", Code = "BDM-I3", SportId = sports["BDM"].Id, FacilityId = facilities["IND-A"].Id, Status = CourtStatus.Available, Capacity = 4, DisplayOrder = 7, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Padel Court Glass 01", Code = "PAD-G1", SportId = sports["PAD"].Id, FacilityId = facilities["OUT-B"].Id, Status = CourtStatus.Available, Capacity = 4, DisplayOrder = 8, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Padel Court Glass 02", Code = "PAD-G2", SportId = sports["PAD"].Id, FacilityId = facilities["OUT-B"].Id, Status = CourtStatus.Available, Capacity = 4, DisplayOrder = 9, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Tennis Court Hard 01", Code = "TNS-H1", SportId = sports["TNS"].Id, FacilityId = facilities["SUM-A"].Id, Status = CourtStatus.Available, Capacity = 4, DisplayOrder = 10, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Tennis Court Clay 01", Code = "TNS-C1", SportId = sports["TNS"].Id, FacilityId = facilities["SUM-A"].Id, Status = CourtStatus.Maintenance, Capacity = 4, DisplayOrder = 11, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Squash Court Glass 01", Code = "SQU-G1", SportId = sports["SQU"].Id, FacilityId = facilities["CTR-H"].Id, Status = CourtStatus.Available, Capacity = 2, DisplayOrder = 12, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Squash Court Glass 02", Code = "SQU-G2", SportId = sports["SQU"].Id, FacilityId = facilities["CTR-H"].Id, Status = CourtStatus.Available, Capacity = 2, DisplayOrder = 13, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Table Tennis Zone 01", Code = "TTN-M1", SportId = sports["TTN"].Id, FacilityId = facilities["PRM-C"].Id, Status = CourtStatus.Available, Capacity = 8, DisplayOrder = 14, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Court { Name = "Basketball Court Riverside 01", Code = "BKB-R1", SportId = sports["BKB"].Id, FacilityId = facilities["RIV-G"].Id, Status = CourtStatus.Available, Capacity = 12, DisplayOrder = 15, IsActive = true, CreatedAt = DateTime.UtcNow }
            };

            foreach (var court in courts)
            {
                if (!await db.Courts.AnyAsync(c => c.Code == court.Code))
                {
                    db.Courts.Add(court);
                }
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedCourtPricingsAsync(ApplicationDbContext db)
        {
            var courts = await db.Courts.ToDictionaryAsync(c => c.Code);

            var pricings = new[]
            {
                new CourtPricing { CourtId = courts["CRK-N1"].Id, Name = "Morning Off-Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(12, 0), DurationMinutes = 60, Price = 1200, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["CRK-N1"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 60, Price = 2200, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["CRK-N2"].Id, Name = "Morning Off-Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(12, 0), DurationMinutes = 60, Price = 1200, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["CRK-N2"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 60, Price = 2200, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["FTB-E1"].Id, Name = "Day Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), DurationMinutes = 90, Price = 3500, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["FTB-E1"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 90, Price = 5500, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["FTB-E2"].Id, Name = "Day Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), DurationMinutes = 90, Price = 2500, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["FTB-E2"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 90, Price = 4000, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["BDM-I1"].Id, Name = "Morning Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(12, 0), DurationMinutes = 60, Price = 800, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["BDM-I1"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 60, Price = 1400, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["BDM-I2"].Id, Name = "Morning Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(12, 0), DurationMinutes = 60, Price = 800, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["BDM-I2"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 60, Price = 1400, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["BDM-I3"].Id, Name = "Off-Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), DurationMinutes = 60, Price = 700, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["PAD-G1"].Id, Name = "Day Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), DurationMinutes = 60, Price = 1500, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["PAD-G1"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 60, Price = 2500, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["PAD-G2"].Id, Name = "Day Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), DurationMinutes = 60, Price = 1500, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["PAD-G2"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 60, Price = 2500, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["TNS-H1"].Id, Name = "Day Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), DurationMinutes = 90, Price = 1800, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["TNS-H1"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 90, Price = 3000, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["SQU-G1"].Id, Name = "Day Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), DurationMinutes = 45, Price = 600, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["SQU-G1"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 45, Price = 1100, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["SQU-G2"].Id, Name = "Day Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), DurationMinutes = 45, Price = 600, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["SQU-G2"].Id, Name = "Evening Peak Rate", DayOfWeek = null, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 45, Price = 1100, IsPeakRate = true, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["TTN-M1"].Id, Name = "Hourly Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 30, Price = 400, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new CourtPricing { CourtId = courts["BKB-R1"].Id, Name = "Hourly Rate", DayOfWeek = null, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(23, 0), DurationMinutes = 60, Price = 1000, IsPeakRate = false, IsActive = true, CreatedAt = DateTime.UtcNow }
            };

            foreach (var pricing in pricings)
            {
                var exists = await db.CourtPricings.AnyAsync(p =>
                    p.CourtId == pricing.CourtId &&
                    p.DayOfWeek == pricing.DayOfWeek &&
                    p.StartTime == pricing.StartTime &&
                    p.EndTime == pricing.EndTime &&
                    p.DurationMinutes == pricing.DurationMinutes);
                if (!exists)
                {
                    db.CourtPricings.Add(pricing);
                }
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedCourtSchedulesAsync(ApplicationDbContext db)
        {
            var courts = await db.Courts.ToDictionaryAsync(c => c.Code);

            var scheduledCourts = new[] { "CRK-N1", "FTB-E1", "BDM-I1", "PAD-G1" };
            var days = Enum.GetValues<DayOfWeek>().Where(d => d != DayOfWeek.Sunday).ToArray();

            foreach (var courtCode in scheduledCourts)
            {
                var courtId = courts[courtCode].Id;
                foreach (var day in days)
                {
                    var exists = await db.CourtSchedules.AnyAsync(s =>
                        s.CourtId == courtId && s.DayOfWeek == day && s.IsActive);
                    if (!exists)
                    {
                        db.CourtSchedules.Add(new CourtSchedule
                        {
                            CourtId = courtId,
                            DayOfWeek = day,
                            OpeningTime = new TimeOnly(6, 0),
                            ClosingTime = new TimeOnly(23, 0),
                            IsClosed = false,
                            SlotDurationMinutes = 60,
                            BufferMinutes = 10,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedCustomerRecordsAsync(ApplicationDbContext db)
        {
            var now = DateTime.UtcNow;
            var customers = new[]
            {
                new Customer
                {
                    CustomerCode = "CUST-000001",
                    FirstName = "Omar",
                    LastName = "Khalid",
                    Gender = Gender.Male,
                    PrimaryPhone = "03001122334",
                    Email = "omar.khalid@lhrsports.com",
                    NationalIdNumber = "3520211223344",
                    City = "Lahore",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Individual,
                    CustomerSource = CustomerSource.Referral,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "CRK,FTB",
                    ReceiveBookingReminders = true,
                    LoyaltyPoints = 250,
                    IsActive = true,
                    CreatedAt = now.AddDays(-45)
                },
                new Customer
                {
                    CustomerCode = "CUST-000002",
                    FirstName = "Fatima",
                    LastName = "Ahmed",
                    Gender = Gender.Female,
                    PrimaryPhone = "03012233445",
                    Email = "fatima.ahmed@islabc.com",
                    NationalIdNumber = "6110122334455",
                    City = "Islamabad",
                    Country = "Pakistan",
                    CustomerType = CustomerType.VIP,
                    CustomerSource = CustomerSource.SocialMedia,
                    PreferredContactMethod = PreferredContactMethod.Email,
                    PreferredSportCodes = "PAD,TNS",
                    IsMember = true,
                    MembershipNumber = "MEM-VIP-001",
                    MembershipStartDate = DateOnly.FromDateTime(now.AddMonths(-3)),
                    MembershipExpiryDate = DateOnly.FromDateTime(now.AddMonths(9)),
                    AllowCredit = true,
                    CreditLimit = 75000,
                    LoyaltyPoints = 1500,
                    ReceiveBookingReminders = true,
                    ReceiveMarketingMessages = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-90)
                },
                new Customer
                {
                    CustomerCode = "CUST-000003",
                    FirstName = "Imran",
                    LastName = "Hussain",
                    Gender = Gender.Male,
                    PrimaryPhone = "03023344556",
                    Email = "imran.hussain@karachicc.com",
                    NationalIdNumber = "4210133445566",
                    City = "Karachi",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Corporate,
                    CustomerSource = CustomerSource.Corporate,
                    OrganizationName = "Karachi Cricket Club",
                    Occupation = "Sports Coordinator",
                    PreferredContactMethod = PreferredContactMethod.Phone,
                    PreferredSportCodes = "CRK,FTB",
                    AllowCredit = true,
                    CreditLimit = 120000,
                    OutstandingBalance = 15000,
                    LoyaltyPoints = 600,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-120)
                },
                new Customer
                {
                    CustomerCode = "CUST-000004",
                    FirstName = "Ayesha",
                    LastName = "Siddiqui",
                    Gender = Gender.Female,
                    PrimaryPhone = "03034455667",
                    Email = "ayesha.siddiqui@student.edu",
                    NationalIdNumber = "3520144556677",
                    City = "Lahore",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Student,
                    CustomerSource = CustomerSource.WalkIn,
                    PreferredContactMethod = PreferredContactMethod.SMS,
                    PreferredSportCodes = "BDM",
                    ReceiveBookingReminders = true,
                    LoyaltyPoints = 80,
                    IsActive = true,
                    CreatedAt = now.AddDays(-60)
                },
                new Customer
                {
                    CustomerCode = "CUST-000005",
                    FirstName = "Ali",
                    LastName = "Raza",
                    Gender = Gender.Male,
                    PrimaryPhone = "03045566778",
                    Email = "ali.raza@rawalpindi.club",
                    NationalIdNumber = "3740155667788",
                    City = "Rawalpindi",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Family,
                    CustomerSource = CustomerSource.Referral,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "PAD,FTB",
                    LoyaltyPoints = 350,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-75)
                },
                new Customer
                {
                    CustomerCode = "CUST-000006",
                    FirstName = "Sara",
                    LastName = "Malik",
                    Gender = Gender.Female,
                    PrimaryPhone = "03056677889",
                    Email = "sara.malik@faisalabad.net",
                    NationalIdNumber = "3310166778899",
                    City = "Faisalabad",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Individual,
                    CustomerSource = CustomerSource.Website,
                    PreferredContactMethod = PreferredContactMethod.Email,
                    PreferredSportCodes = "BDM,CRK",
                    IsMember = true,
                    MembershipNumber = "MEM-IND-002",
                    MembershipStartDate = DateOnly.FromDateTime(now.AddMonths(-1)),
                    MembershipExpiryDate = DateOnly.FromDateTime(now.AddMonths(11)),
                    ReceiveBookingReminders = true,
                    LoyaltyPoints = 400,
                    IsActive = true,
                    CreatedAt = now.AddDays(-30)
                },
                new Customer
                {
                    CustomerCode = "CUST-000007",
                    FirstName = "Bilal",
                    LastName = "Khan",
                    Gender = Gender.Male,
                    PrimaryPhone = "03067788990",
                    Email = "bilal.khan@multan.pk",
                    NationalIdNumber = "3630177889900",
                    City = "Multan",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Student,
                    CustomerSource = CustomerSource.PhoneCall,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "PAD",
                    LoyaltyPoints = 120,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-40)
                },
                new Customer
                {
                    CustomerCode = "CUST-000008",
                    FirstName = "Hina",
                    LastName = "Farooq",
                    Gender = Gender.Female,
                    PrimaryPhone = "03078899001",
                    Email = "hina.farooq@peshawar.sports",
                    NationalIdNumber = "1730188990011",
                    City = "Peshawar",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Individual,
                    CustomerSource = CustomerSource.SocialMedia,
                    PreferredContactMethod = PreferredContactMethod.SMS,
                    PreferredSportCodes = "BDM,CRK",
                    LoyaltyPoints = 200,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-55)
                },
                new Customer
                {
                    CustomerCode = "CUST-000009",
                    FirstName = "Usman",
                    LastName = "Tariq",
                    Gender = Gender.Male,
                    PrimaryPhone = "03089900112",
                    Email = "usman.tariq@quetta.org",
                    NationalIdNumber = "5440199001122",
                    City = "Quetta",
                    Country = "Pakistan",
                    CustomerType = CustomerType.WalkIn,
                    CustomerSource = CustomerSource.WalkIn,
                    PreferredContactMethod = PreferredContactMethod.Phone,
                    PreferredSportCodes = "FTB",
                    LoyaltyPoints = 50,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-20)
                },
                new Customer
                {
                    CustomerCode = "CUST-000010",
                    FirstName = "Zainab",
                    LastName = "Nawaz",
                    Gender = Gender.Female,
                    PrimaryPhone = "03090011223",
                    Email = "zainab.nawaz@gujranwala.com",
                    NationalIdNumber = "3410100112233",
                    City = "Gujranwala",
                    Country = "Pakistan",
                    CustomerType = CustomerType.VIP,
                    CustomerSource = CustomerSource.Referral,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "FTB,BDM",
                    IsMember = true,
                    MembershipNumber = "MEM-VIP-003",
                    MembershipStartDate = DateOnly.FromDateTime(now.AddMonths(-4)),
                    MembershipExpiryDate = DateOnly.FromDateTime(now.AddMonths(8)),
                    AllowCredit = true,
                    CreditLimit = 60000,
                    LoyaltyPoints = 980,
                    ReceiveBookingReminders = true,
                    ReceiveMarketingMessages = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-50)
                },
                new Customer
                {
                    CustomerCode = "CUST-000011",
                    FirstName = "Hassan",
                    LastName = "Ali",
                    Gender = Gender.Male,
                    PrimaryPhone = "03101122334",
                    Email = "hassan.ali@hyderabad.biz",
                    NationalIdNumber = "4130111223344",
                    City = "Hyderabad",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Corporate,
                    CustomerSource = CustomerSource.Corporate,
                    OrganizationName = "Hyderabad Business Group",
                    Occupation = "Procurement Manager",
                    PreferredContactMethod = PreferredContactMethod.Email,
                    PreferredSportCodes = "CRK,FTB",
                    AllowCredit = true,
                    CreditLimit = 90000,
                    OutstandingBalance = 8000,
                    LoyaltyPoints = 450,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-100)
                },
                new Customer
                {
                    CustomerCode = "CUST-000012",
                    FirstName = "Mariam",
                    LastName = "Shah",
                    Gender = Gender.Female,
                    PrimaryPhone = "03112233445",
                    Email = "mariam.shah@islamabad.edu",
                    NationalIdNumber = "6110133445566",
                    City = "Islamabad",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Student,
                    CustomerSource = CustomerSource.Website,
                    PreferredContactMethod = PreferredContactMethod.SMS,
                    PreferredSportCodes = "TNS,SQU",
                    LoyaltyPoints = 100,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-35)
                },
                new Customer
                {
                    CustomerCode = "CUST-000013",
                    FirstName = "Ahmed",
                    LastName = "Raza",
                    Gender = Gender.Male,
                    PrimaryPhone = "03123344556",
                    Email = "ahmed.raza@lahore.pk",
                    NationalIdNumber = "3520233445566",
                    City = "Lahore",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Individual,
                    CustomerSource = CustomerSource.PhoneCall,
                    PreferredContactMethod = PreferredContactMethod.Phone,
                    PreferredSportCodes = "PAD,TNS",
                    LoyaltyPoints = 180,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-25)
                },
                new Customer
                {
                    CustomerCode = "CUST-000014",
                    FirstName = "Nida",
                    LastName = "Iqbal",
                    Gender = Gender.Female,
                    PrimaryPhone = "03134455667",
                    Email = "nida.iqbal@karachi.net",
                    NationalIdNumber = "4210144556677",
                    City = "Karachi",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Family,
                    CustomerSource = CustomerSource.SocialMedia,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "BDM,FTB",
                    IsMember = true,
                    MembershipNumber = "MEM-FAM-004",
                    MembershipStartDate = DateOnly.FromDateTime(now.AddMonths(-6)),
                    MembershipExpiryDate = DateOnly.FromDateTime(now.AddMonths(6)),
                    AllowCredit = true,
                    CreditLimit = 40000,
                    LoyaltyPoints = 550,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-70)
                },
                new Customer
                {
                    CustomerCode = "CUST-000015",
                    FirstName = "Yasir",
                    LastName = "Mahmood",
                    Gender = Gender.Male,
                    PrimaryPhone = "03145566778",
                    Email = "yasir.mahmood@rawalpindi.org",
                    NationalIdNumber = "3740166778899",
                    City = "Rawalpindi",
                    Country = "Pakistan",
                    CustomerType = CustomerType.WalkIn,
                    CustomerSource = CustomerSource.WalkIn,
                    PreferredContactMethod = PreferredContactMethod.SMS,
                    PreferredSportCodes = "CRK",
                    LoyaltyPoints = 70,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-15)
                },
                new Customer
                {
                    CustomerCode = "CUST-000016",
                    FirstName = "Samina",
                    LastName = "Bano",
                    Gender = Gender.Female,
                    PrimaryPhone = "03156677889",
                    Email = "samina.bano@faisalabad.pk",
                    NationalIdNumber = "3310177889900",
                    City = "Faisalabad",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Individual,
                    CustomerSource = CustomerSource.Referral,
                    PreferredContactMethod = PreferredContactMethod.Email,
                    PreferredSportCodes = "TNS,PAD",
                    LoyaltyPoints = 220,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-45)
                },
                new Customer
                {
                    CustomerCode = "CUST-000017",
                    FirstName = "Bilal",
                    LastName = "Ahmed",
                    Gender = Gender.Male,
                    PrimaryPhone = "03167788990",
                    Email = "bilal.ahmed@peshawar.net",
                    NationalIdNumber = "1730177889900",
                    City = "Peshawar",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Student,
                    CustomerSource = CustomerSource.PhoneCall,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "SQU,TTN",
                    LoyaltyPoints = 90,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-38)
                },
                new Customer
                {
                    CustomerCode = "CUST-000018",
                    FirstName = "Aiman",
                    LastName = "Khan",
                    Gender = Gender.Female,
                    PrimaryPhone = "03178899001",
                    Email = "aiman.khan@quetta.edu",
                    NationalIdNumber = "5440188990011",
                    City = "Quetta",
                    Country = "Pakistan",
                    CustomerType = CustomerType.VIP,
                    CustomerSource = CustomerSource.SocialMedia,
                    PreferredContactMethod = PreferredContactMethod.WhatsApp,
                    PreferredSportCodes = "PAD,VOL",
                    IsMember = true,
                    MembershipNumber = "MEM-VIP-005",
                    MembershipStartDate = DateOnly.FromDateTime(now.AddMonths(-2)),
                    MembershipExpiryDate = DateOnly.FromDateTime(now.AddMonths(10)),
                    AllowCredit = true,
                    CreditLimit = 50000,
                    LoyaltyPoints = 850,
                    ReceiveBookingReminders = true,
                    ReceiveMarketingMessages = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-52)
                },
                new Customer
                {
                    CustomerCode = "CUST-000019",
                    FirstName = "Farhan",
                    LastName = "Siddiqui",
                    Gender = Gender.Male,
                    PrimaryPhone = "03189900112",
                    Email = "farhan.siddiqui@multan.org",
                    NationalIdNumber = "3630199001122",
                    City = "Multan",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Corporate,
                    CustomerSource = CustomerSource.Corporate,
                    OrganizationName = "Multan Sports Organization",
                    Occupation = "Operations Head",
                    PreferredContactMethod = PreferredContactMethod.Phone,
                    PreferredSportCodes = "FTB,CRK",
                    AllowCredit = true,
                    CreditLimit = 110000,
                    OutstandingBalance = 5000,
                    LoyaltyPoints = 520,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-85)
                },
                new Customer
                {
                    CustomerCode = "CUST-000020",
                    FirstName = "Hina",
                    LastName = "Malik",
                    Gender = Gender.Female,
                    PrimaryPhone = "03190011223",
                    Email = "hina.malik@lhrsports.com",
                    NationalIdNumber = "3520200112233",
                    City = "Lahore",
                    Country = "Pakistan",
                    CustomerType = CustomerType.Family,
                    CustomerSource = CustomerSource.Referral,
                    PreferredContactMethod = PreferredContactMethod.SMS,
                    PreferredSportCodes = "FTB,BDM",
                    LoyaltyPoints = 280,
                    ReceiveBookingReminders = true,
                    IsActive = true,
                    CreatedAt = now.AddDays(-28)
                }
            };

            foreach (var customer in customers)
            {
                if (!await db.Customers.AnyAsync(c => c.CustomerCode == customer.CustomerCode))
                {
                    db.Customers.Add(customer);
                }
            }
            await db.SaveChangesAsync();
        }
    }
}
