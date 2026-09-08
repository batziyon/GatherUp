namespace GatherUp.Core.Exceptions
{
    public class ReceiptLockedException : Exception
    {
        public int VendorId { get; }

        public ReceiptLockedException(int vendorId)
            : base($"Vendor {vendorId} receipts are locked because the debt has already been settled.")
        {
            VendorId = vendorId;
        }
    }
}
