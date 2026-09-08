using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GatherUp.Core.Exceptions;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;

namespace GatherUp.BL
{
    public record FinancialSummary(
        IEnumerable<(Participant Participant, decimal Amount)> Payers,
        decimal TotalIncome,
        IEnumerable<(VendorAllocation Vendor, decimal Owed)> Vendors,
        decimal TotalExpenses,
        decimal Balance
    );

    public class FinanceService
    {
        private readonly IRepository<Participant>      _participantRepo;
        private readonly IRepository<VendorAllocation> _vendorRepo;
        private readonly IRepository<Receipt>          _receiptRepo;
        private readonly IRepository<Event>            _eventRepo;
        private readonly IEmailService                 _emailService;
        private readonly IEventNotifier?               _notifier;

        public FinanceService(
            IRepository<Participant>      participantRepo,
            IRepository<VendorAllocation> vendorRepo,
            IRepository<Receipt>          receiptRepo,
            IRepository<Event>            eventRepo,
            IEmailService                 emailService,
            IEventNotifier?               notifier = null)
        {
            _participantRepo = participantRepo;
            _vendorRepo      = vendorRepo;
            _receiptRepo     = receiptRepo;
            _eventRepo       = eventRepo;
            _emailService    = emailService;
            _notifier        = notifier;
        }

        public async Task RegisterPaymentAsync(int participantId, int eventId, decimal amount)
        {
            if (amount <= 0)
                throw new InvalidInputException(nameof(amount), "payment amount must be greater than zero");

            var ev = await _eventRepo.GetByIdAsync(eventId);
            if (!ev.ParticipantIds.Contains(participantId))
                throw new InvalidInputException(nameof(participantId), "participant is not registered for the event");

            var participant = await _participantRepo.GetByIdAsync(participantId);

            if (participant.IsAttending != true)
                throw new InvalidInputException(nameof(participant.IsAttending),
                    "participant must confirm attendance before payment can be registered");

            var existing = participant.EventPayments.FirstOrDefault(p => p.EventId == eventId);
            if (existing != null)
                existing.Amount += amount;
            else
                participant.EventPayments.Add(new EventPayment { EventId = eventId, Amount = amount });

            participant.HasPaid = true;
            participant.AmountContributed += amount;
            await _participantRepo.UpdateAsync(participant);

            if (_notifier != null)
                await _notifier.OnPaymentReceivedAsync(participantId, eventId);
        }

        public async Task<VendorAllocation> AddVendorToEventAsync(int eventId, string vendorName, decimal initialDebt)
        {
            if (string.IsNullOrWhiteSpace(vendorName))
                throw new InvalidInputException(nameof(vendorName), "vendor name cannot be empty");
            if (initialDebt < 0)
                throw new InvalidInputException(nameof(initialDebt), "initial debt cannot be negative");

            var ev  = await _eventRepo.GetByIdAsync(eventId);
            var all = (await _vendorRepo.GetAllAsync()).ToList();
            int newId = all.Any() ? all.Max(v => v.Id) + 1 : 1;

            var vendor = new VendorAllocation { Id = newId, Name = vendorName, AmountOwed = initialDebt, IsPaid = initialDebt == 0 };
            await _vendorRepo.AddAsync(vendor);

            if (!ev.VendorIds.Contains(vendor.Id))
            {
                ev.VendorIds.Add(vendor.Id);
                await _eventRepo.UpdateAsync(ev);
            }
            return vendor;
        }

        public async Task AddVendorDebtAsync(int vendorId, decimal amount)
        {
            if (amount <= 0)
                throw new InvalidInputException(nameof(amount), "debt amount must be greater than zero");
            var vendor = await _vendorRepo.GetByIdAsync(vendorId);
            vendor.AmountOwed += amount;
            vendor.IsPaid = vendor.AmountOwed == 0;
            await _vendorRepo.UpdateAsync(vendor);
        }

        public async Task<Receipt> AddReceiptAsync(Receipt receipt, int vendorId)
        {
            if (receipt.Amount <= 0)
                throw new InvalidInputException(nameof(receipt.Amount), "receipt amount must be greater than zero");
            if (string.IsNullOrWhiteSpace(receipt.FilePath))
                throw new InvalidInputException(nameof(receipt.FilePath), "receipt file path is required");

            var vendor = await _vendorRepo.GetByIdAsync(vendorId);
            if (receipt.Amount > vendor.AmountOwed)
                throw new InvalidInputException(nameof(receipt.Amount), "receipt amount cannot exceed vendor outstanding debt");
            if (vendor.AmountOwed == 0 && vendor.ReceiptIds.Count > 0)
                throw new ReceiptLockedException(vendorId);

            var allReceipts = (await _receiptRepo.GetAllAsync()).ToList();
            int newId = allReceipts.Any() ? allReceipts.Max(r => r.Id) + 1 : 1;
            var finalReceipt = new Receipt
            {
                Id            = newId,
                VendorId      = receipt.VendorId,
                ReceiptNumber = receipt.ReceiptNumber,
                FilePath      = receipt.FilePath,
                Amount        = receipt.Amount,
                Date          = receipt.Date
            };

            await _receiptRepo.AddAsync(finalReceipt);
            vendor.ReceiptIds.Add(finalReceipt.Id);
            vendor.HasReceipt = true;
            vendor.AmountOwed -= finalReceipt.Amount;
            vendor.IsPaid = vendor.AmountOwed == 0;
            await _vendorRepo.UpdateAsync(vendor);
            return finalReceipt;
        }

        public async Task SendPaymentRemindersAsync(int eventId, string bankDetails)
        {
            var ev  = await _eventRepo.GetByIdAsync(eventId);
            var all = (await _participantRepo.GetAllAsync()).ToList();

            var unpaid = all.Where(p =>
                ev.ParticipantIds.Contains(p.Id) &&
                p.IsAttending == true &&
                !p.HasPaidForEvent(eventId)).ToList();

            await Task.WhenAll(unpaid.Select(p =>
                _emailService.SendAsync(p.Email, "תזכורת תשלום",
                    $"שלום {p.Name},\n\nטרם התקבל תשלומך עבור האירוע \"{ev.Name}\".\n" +
                    $"סכום לתשלום: {ev.PricePerParticipant}₪\n" +
                    $"פרטי חשבון: {bankDetails}\n\n" +
                    $"בברכה,\nמערכת GatherUp",
                    eventId)
                    .ContinueWith(t => {
                        if (t.IsFaulted)
                            System.Diagnostics.Debug.WriteLine($"Failed reminder to {p.Email}: {t.Exception?.GetBaseException().Message}");
                    })));
        }

        public async Task<FinancialSummary> GetFinancialSummaryAsync(int eventId)
        {
            var ev      = await _eventRepo.GetByIdAsync(eventId);
            var payers  = await CalcPayersAsync(ev);
            var vendors = await CalcVendorsAsync(ev);
            decimal income   = payers.Sum(x => x.Item2);
            decimal expenses = vendors.Sum(x => x.Item2);
            return new FinancialSummary(payers, income, vendors, expenses, income - expenses);
        }

        public async Task<decimal> GetNetBalanceAsync(int eventId)
        {
            var ev = await _eventRepo.GetByIdAsync(eventId);
            var participants = (await _participantRepo.GetAllAsync())
                .Where(p => ev.ParticipantIds.Contains(p.Id) && p.IsAttending == true && p.HasPaidForEvent(eventId))
                .Sum(p => p.GetAmountForEvent(eventId));
            var vendorDebt = (await _vendorRepo.GetAllAsync())
                .Where(v => ev.VendorIds.Contains(v.Id))
                .Sum(v => v.AmountOwed);
            return participants - vendorDebt;
        }

        public Task<VendorAllocation> GetVendorByIdAsync(int vendorId) => _vendorRepo.GetByIdAsync(vendorId);
        public Task<IEnumerable<VendorAllocation>> GetAllVendorsAsync() => _vendorRepo.GetAllAsync();

        public async Task<IEnumerable<VendorAllocation>> GetEventVendorsAsync(int eventId)
        {
            var ev = await _eventRepo.GetByIdAsync(eventId);
            return (await _vendorRepo.GetAllAsync()).Where(v => ev.VendorIds.Contains(v.Id));
        }

        public Task DeleteVendorAsync(int vendorId) => _vendorRepo.DeleteAsync(vendorId);

        public async Task<IEnumerable<(int ReceiptId, int VendorId, string FilePath, decimal Amount, DateTime Date)>> GetAllReceiptsSortedAsync(int eventId)
        {
            var ev         = await _eventRepo.GetByIdAsync(eventId);
            var vendors    = (await _vendorRepo.GetAllAsync()).Where(v => ev.VendorIds.Contains(v.Id)).ToList();
            var receiptIds = vendors.SelectMany(v => v.ReceiptIds).ToList();
            var receipts   = await Task.WhenAll(receiptIds.Select(rid => _receiptRepo.GetByIdAsync(rid)));
            return receipts.OrderByDescending(r => r.Date)
                .Select(r => (r.Id, r.VendorId, r.FilePath ?? string.Empty, r.Amount, r.Date));
        }

        private async Task<IEnumerable<(Participant, decimal)>> CalcPayersAsync(Event ev)
        {
            return (await _participantRepo.GetAllAsync())
                .Where(p => ev.ParticipantIds.Contains(p.Id) && p.HasPaidForEvent(ev.Id) && p.IsAttending == true)
                .Select(p => (p, p.GetAmountForEvent(ev.Id)));
        }

        private async Task<IEnumerable<(VendorAllocation, decimal)>> CalcVendorsAsync(Event ev)
        {
            return (await _vendorRepo.GetAllAsync())
                .Where(v => ev.VendorIds.Contains(v.Id))
                .Select(v => (v, v.AmountOwed));
        }
    }
}
