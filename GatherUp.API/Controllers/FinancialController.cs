using GatherUp.API.DTOs;
using GatherUp.BL;
using GatherUp.Core.DO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;

namespace GatherUp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Manager")]
    public class FinancialController : ControllerBase
    {
        private readonly FinanceService _financeService;
        private readonly string _receiptsStorageFolder;
        private readonly IWebHostEnvironment _env;

        public FinancialController(FinanceService financeService, IWebHostEnvironment env)
        {
            _financeService        = financeService;
            _env                   = env;
            _receiptsStorageFolder = Path.Combine(env.ContentRootPath, "ReceiptsStorage");
        }

        #region Payments

        [HttpPost("payment")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RegisterPayment([FromBody] RegisterPaymentRequest request)
        {
            await _financeService.RegisterPaymentAsync(request.ParticipantId, request.EventId, request.Amount);
            return NoContent();
        }

        [HttpPost("event/{eventId:int}/reminders")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> SendReminders(int eventId, [FromBody] SendRemindersRequest request)
        {
            await _financeService.SendPaymentRemindersAsync(eventId, request.BankDetails);
            return NoContent();
        }

        #endregion

        #region Vendors

        [HttpPost("vendor")]
        [ProducesResponseType(typeof(VendorResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddVendor([FromBody] AddVendorRequest request)
        {
            var vendor = await _financeService.AddVendorToEventAsync(
                request.EventId, request.Name, request.InitialDebt);

            return CreatedAtAction(nameof(GetVendor), new { vendorId = vendor.Id },
                new VendorResponse(vendor.Id, vendor.Name, vendor.AmountOwed, vendor.ReceiptIds));
        }

        [HttpGet("vendor/{vendorId:int}")]
        [ProducesResponseType(typeof(VendorResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVendor(int vendorId)
        {
            var v = await _financeService.GetVendorByIdAsync(vendorId);
            return Ok(new VendorResponse(v.Id, v.Name, v.AmountOwed, v.ReceiptIds));
        }

        [HttpGet("vendor")]
        [ProducesResponseType(typeof(IEnumerable<VendorResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllVendors() =>
            Ok((await _financeService.GetAllVendorsAsync())
                .Select(v => new VendorResponse(v.Id, v.Name, v.AmountOwed, v.ReceiptIds)));

        [HttpPost("vendor/{vendorId:int}/debt")]
        [ProducesResponseType(typeof(VendorResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddDebt(int vendorId, [FromBody] AddVendorDebtRequest request)
        {
            await _financeService.AddVendorDebtAsync(vendorId, request.Amount);
            var v = await _financeService.GetVendorByIdAsync(vendorId);
            return Ok(new VendorResponse(v.Id, v.Name, v.AmountOwed, v.ReceiptIds));
        }

        [HttpDelete("vendor/{vendorId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteVendor(int vendorId)
        {
            await _financeService.DeleteVendorAsync(vendorId);
            return NoContent();
        }

        [HttpGet("event/{eventId:int}/vendors")]
        [ProducesResponseType(typeof(IEnumerable<VendorResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEventVendors(int eventId) =>
            Ok((await _financeService.GetEventVendorsAsync(eventId))
                .Select(v => new VendorResponse(v.Id, v.Name, v.AmountOwed, v.ReceiptIds)));

        #endregion

        #region Receipts

        [HttpPost("vendor/{vendorId:int}/receipt")]
        [ProducesResponseType(typeof(ReceiptResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddReceipt(int vendorId, [FromBody] AddReceiptRequest request)
        {
            string? normalizedPath = request.FilePath;
            if (!string.IsNullOrWhiteSpace(normalizedPath))
            {
                normalizedPath = System.IO.Path.GetFileName(
                    normalizedPath.Replace('\\', '/').TrimEnd('/'));
            }

            var receipt = new Receipt
            {
                Id            = 0,
                VendorId      = vendorId,
                ReceiptNumber = request.ReceiptNumber,
                FilePath      = normalizedPath,
                Amount        = request.Amount,
                Date          = request.Date
            };

            var saved = await _financeService.AddReceiptAsync(receipt, vendorId);

            return StatusCode(StatusCodes.Status201Created,
                new ReceiptResponse(saved.Id, saved.VendorId, saved.ReceiptNumber,
                                    saved.FilePath, saved.Amount, saved.Date));
        }

        [HttpPost("vendor/{vendorId:int}/receipt/upload")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> UploadReceiptFile(int vendorId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "קובץ לא תקין" });

            string ext      = Path.GetExtension(file.FileName);
            string safeName = $"vendor-{vendorId}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            string destPath = Path.Combine(_receiptsStorageFolder, safeName);

            Directory.CreateDirectory(_receiptsStorageFolder);
            await using var stream = new FileStream(destPath, FileMode.Create);
            await file.CopyToAsync(stream);

            string baseUrl  = $"{Request.Scheme}://{Request.Host}";
            string fileUrl  = $"{baseUrl}/receipts/{safeName}";

            return Ok(new { fileName = safeName, fileUrl });
        }

        [HttpGet("receipt/file/{fileName}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult ServeReceiptFile(string fileName)
        {
            string safeName = System.IO.Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeName))
                return BadRequest(new { message = "שם קובץ לא תקין" });

            string storagePath = Path.Combine(_receiptsStorageFolder, safeName);
            if (System.IO.File.Exists(storagePath))
            {
                string mimeType = GetMimeType(safeName);
                return PhysicalFile(storagePath, mimeType);
            }

            string wwwrootPath = Path.Combine(_env.WebRootPath ?? "", safeName);
            if (System.IO.File.Exists(wwwrootPath))
            {
                string mimeType = GetMimeType(safeName);
                return PhysicalFile(wwwrootPath, mimeType);
            }

            return NotFound(new { message = $"קובץ קבלה '{safeName}' לא נמצא" });
        }

        private static string GetMimeType(string fileName)
        {
            string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"            => "image/png",
                ".gif"            => "image/gif",
                ".pdf"            => "application/pdf",
                ".webp"           => "image/webp",
                _                 => "application/octet-stream"
            };
        }
        [HttpGet("event/{eventId:int}/receipts")]
        [ProducesResponseType(typeof(IEnumerable<ReceiptSortedResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReceiptsSorted(int eventId) =>
            Ok((await _financeService.GetAllReceiptsSortedAsync(eventId))
                .Select(r => new ReceiptSortedResponse(r.ReceiptId, r.VendorId, r.FilePath, r.Amount, r.Date)));

        #endregion

        #region Summary

        [HttpGet("event/{eventId:int}/summary")]
        [ProducesResponseType(typeof(FinancialSummaryResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSummary(int eventId)
        {
            var s = await _financeService.GetFinancialSummaryAsync(eventId);
            return Ok(new FinancialSummaryResponse(
                s.Payers.Select(p  => new PayerResponse(p.Participant.Id, p.Participant.Name, p.Amount)).ToList(),
                s.TotalIncome,
                s.Vendors.Select(v => new VendorResponse(v.Vendor.Id, v.Vendor.Name, v.Owed, v.Vendor.ReceiptIds)).ToList(),
                s.TotalExpenses,
                s.Balance));
        }

        [HttpGet("event/{eventId:int}/balance")]
        [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBalance(int eventId) =>
            Ok(await _financeService.GetNetBalanceAsync(eventId));

        #endregion
    }
}
