using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GatherUp.API.DTOs
{
    public record RegisterPaymentRequest(
        [Required] int     ParticipantId,
        [Required] int     EventId,
        [Required, Range(0.01, double.MaxValue)] decimal Amount
    );

    public record AddVendorRequest(
        [Required] int     EventId,
        [Required, MinLength(2)] string Name,
        [Range(0, double.MaxValue)] decimal InitialDebt
    );

    public record AddVendorDebtRequest(
        [Required, Range(0.01, double.MaxValue)] decimal Amount
    );

    public record AddReceiptRequest(
        [Required] string   ReceiptNumber,
        [Required] string   FilePath,
        [Required, Range(0.01, double.MaxValue)] decimal Amount,
        [Required] DateTime Date
    );

    public record SendRemindersRequest(
        [Required] string BankDetails
    );

    public record VendorResponse(
        int       Id,
        string    Name,
        decimal   AmountOwed,
        List<int> ReceiptIds
    );

    public record ReceiptResponse(
        int      Id,
        int      VendorId,
        string   ReceiptNumber,
        string   FilePath,
        decimal  Amount,
        DateTime Date
    );

    public record PayerResponse(
        int     ParticipantId,
        string  ParticipantName,
        decimal Amount
    );

    public record FinancialSummaryResponse(
        List<PayerResponse>  Payers,
        decimal              TotalIncome,
        List<VendorResponse> Vendors,
        decimal              TotalExpenses,
        decimal              Balance
    );

    public record ReceiptSortedResponse(
        int      ReceiptId,
        int      VendorId,
        string   FilePath,
        decimal  Amount,
        DateTime Date
    );
}
