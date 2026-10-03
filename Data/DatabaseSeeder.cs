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
                var admin = new User
                {
                    FirstName = "Mustafa",
                    LastName = "Ahmed",
                    Email = "admin@gamehub.pk",
                    PhoneNumber = "0300-0000000",
                    Role = UserRole.SuperAdmin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                admin.PasswordHash = PasswordHelper.HashPassword(admin, "Admin@123");

                dbContext.Users.Add(admin);
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
                    Temperature = LeadTemperature.Hot,
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
            var customers = await dbContext.Customers.Take(3).ToListAsync();
            if (owner == null) return;

            var now = DateTime.UtcNow;
            var leads = new List<Lead>
            {
                new() { LeadNumber = "LEAD-000001", CustomerName = "Saad Farooq", Email = "saad.farooq@example.pk", Phone = "03055550101", Source = LeadSource.WebsiteInquiry, ServiceInterest = LeadServiceInterest.CourtBooking, SportId = sports.FirstOrDefault()?.Id, Status = LeadStatus.New, Priority = LeadPriority.High, Temperature = LeadTemperature.Hot, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddHours(6), InquirySubject = "Evening padel booking", InquiryDetails = "Looking for recurring evening court availability.", IsWebsiteInquiry = true, PublicReferenceNumber = "INQ-100001", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-1), IsActive = true },
                new() { LeadNumber = "LEAD-000002", CustomerId = customers.ElementAtOrDefault(0)?.Id, CustomerName = customers.ElementAtOrDefault(0)?.FullName ?? "Ayaan Khan", Email = customers.ElementAtOrDefault(0)?.Email, Phone = customers.ElementAtOrDefault(0)?.PrimaryPhone, Source = LeadSource.ExistingCustomer, ServiceInterest = LeadServiceInterest.Membership, Status = LeadStatus.Contacted, Priority = LeadPriority.Medium, Temperature = LeadTemperature.Warm, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddDays(1), InquirySubject = "Premium membership upgrade", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-3), IsActive = true },
                new() { LeadNumber = "LEAD-000003", CustomerName = "Mehwish Ali", Phone = "03055550103", WhatsAppNumber = "03055550103", OrganizationName = "Lahore Tech Hub", Source = LeadSource.Corporate, ServiceInterest = LeadServiceInterest.CorporateBooking, Status = LeadStatus.Qualified, Priority = LeadPriority.Urgent, Temperature = LeadTemperature.Hot, AssignedToUserId = owner.Id, QualifiedAt = now.AddDays(-1), QualificationNotes = "Corporate team sports package required.", InquirySubject = "Corporate sports evening", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-7), IsActive = true },
                new() { LeadNumber = "LEAD-000004", CustomerName = "Hamza Noor", Email = "hamza.noor@example.pk", Phone = "03055550104", Source = LeadSource.Phone, ServiceInterest = LeadServiceInterest.Tournament, Status = LeadStatus.FollowUp, Priority = LeadPriority.High, Temperature = LeadTemperature.Warm, AssignedToUserId = owner.Id, NextFollowUpAt = now.AddHours(-4), InquirySubject = "Weekend tournament", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-4), IsActive = true },
                new() { LeadNumber = "LEAD-000005", CustomerName = "Nimra Sheikh", Email = "nimra.sheikh@example.pk", Phone = "03055550105", Source = LeadSource.SocialMedia, ServiceInterest = LeadServiceInterest.Coaching, Status = LeadStatus.Disqualified, Priority = LeadPriority.Low, Temperature = LeadTemperature.Cold, AssignedToUserId = owner.Id, DisqualifiedAt = now.AddDays(-1), DisqualificationReason = "Outside current coaching scope.", InquirySubject = "Tennis coaching", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-8), IsActive = true },
                new() { LeadNumber = "LEAD-000006", CustomerName = "Bilal Raza", OrganizationName = "Northstar Sports Club", Phone = "03021234567", Source = LeadSource.Referral, ServiceInterest = LeadServiceInterest.Event, Status = LeadStatus.Qualified, Priority = LeadPriority.High, Temperature = LeadTemperature.Hot, AssignedToUserId = owner.Id, QualifiedAt = now.AddDays(-2), QualificationNotes = "Facility event package likely.", InquirySubject = "Sports family day", CreatedByUserId = owner.Id, CreatedAt = now.AddDays(-10), IsActive = true }
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
                    Email = "ayaan.khan@example.pk",
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
                    Email = "zara.malik@example.pk",
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
                    Email = "sportsdesk@example.pk",
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
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-140)
                },
                new Customer
                {
                    CustomerCode = "CUS-000005",
                    FirstName = "Danish",
                    LastName = "Qureshi",
                    Gender = Gender.Male,
                    PrimaryPhone = "03041234567",
                    City = "Faisalabad",
                    Country = "Pakistan",
                    CustomerType = CustomerType.WalkIn,
                    CustomerSource = CustomerSource.PhoneCall,
                    PreferredContactMethod = PreferredContactMethod.Phone,
                    IsBlacklisted = true,
                    BlacklistReason = "Repeated no-shows without prior notice.",
                    ReceiveBookingReminders = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-25)
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
    }
}
