using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using GatherUp.Core.DO;
using GatherUp.Core.Exceptions;

namespace GatherUp.Infrastructure.Data
{
    public class ReceiptRepository : XmlRepository<Receipt>
    {
        private readonly string _receiptsStorageFolder;

        public ReceiptRepository(string baseFolder, string receiptsStorageFolder)
            : base(baseFolder)
        {
            if (!Directory.Exists(receiptsStorageFolder))
                Directory.CreateDirectory(receiptsStorageFolder);
            _receiptsStorageFolder = receiptsStorageFolder;
        }

        public override async Task AddAsync(Receipt receipt)
        {
            if (!string.IsNullOrWhiteSpace(receipt.FilePath) && File.Exists(receipt.FilePath))
            {
                string fileName   = Path.GetFileName(receipt.FilePath);
                string targetPath = Path.Combine(_receiptsStorageFolder, fileName);
                File.Copy(receipt.FilePath, targetPath, overwrite: true);

                receipt = new Receipt
                {
                    Id            = receipt.Id,
                    VendorId      = receipt.VendorId,
                    ReceiptNumber = receipt.ReceiptNumber,
                    FilePath      = targetPath,
                    Amount        = receipt.Amount,
                    Date          = receipt.Date
                };
            }

            await base.AddAsync(receipt);
        }

        public override async Task<Receipt> GetByIdAsync(int id)
        {
            XDocument doc = await XmlDocManager.LoadAsync(_filePath);

            XElement? found = XmlDocManager.FindById(doc, id)
                           ?? XmlDocManager.FindByChildId(doc, id);

            if (found == null)
                throw new KeyNotFoundException($"Receipt עם Id={id} לא נמצא");

            return new Receipt
            {
                Id            = id,
                VendorId      = XmlDocManager.GetInt(found,     "VendorId"),
                ReceiptNumber = XmlDocManager.GetString(found,  "ReceiptNumber"),
                FilePath      = XmlDocManager.GetString(found,  "FilePath"),
                Amount        = XmlDocManager.GetDecimal(found, "Amount"),
                Date          = XmlDocManager.GetDateTime(found,"Date"),
            };
        }

        public override Task UpdateAsync(Receipt receipt) =>
            throw new ReceiptLockedException(receipt.VendorId);

        public override Task DeleteAsync(int id) =>
            throw new ReceiptLockedException(id);
    }
}
