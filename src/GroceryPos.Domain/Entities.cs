using System;
using System.Collections.Generic;

namespace GroceryPos.Domain
{
    public class User
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string PinHash { get; set; }
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class Setting
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class Category
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }

    public class Supplier
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Gstin { get; set; }
        public string Address { get; set; }
        public int PaymentTermsDays { get; set; }
    }

    public class Item
    {
        public long Id { get; set; }
        public string Sku { get; set; }
        public string Name { get; set; }
        public string PrintName { get; set; }
        public long? CategoryId { get; set; }
        public string Brand { get; set; }
        public string Rack { get; set; }
        public SoldBy SoldBy { get; set; }
        public string Unit { get; set; }
        public int TaxRateBp { get; set; }
        public string HsnCode { get; set; }
        public int ReorderLevel { get; set; }
        public int MaxLevel { get; set; }
        public long? DefaultSupplierId { get; set; }
        public bool TrackBatch { get; set; }
        public bool TrackExpiry { get; set; }
        public bool AllowDiscount { get; set; }
        public bool WeighAtCounter { get; set; }
        public int TareGrams { get; set; }
        public int RoundToGrams { get; set; }
        public int MinSaleGrams { get; set; }
        public bool IsActive { get; set; }
        public long DefaultCostPaise { get; set; }
        public long DefaultSellingPaise { get; set; }
        public long DefaultMrpPaise { get; set; }
    }

    public class ItemBarcode
    {
        public long Id { get; set; }
        public long ItemId { get; set; }
        public string Barcode { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class AuditEntry
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Action { get; set; }
        public string Entity { get; set; }
        public long EntityId { get; set; }
        public string BeforeJson { get; set; }
        public string AfterJson { get; set; }
        public DateTime At { get; set; }
    }

    public class Batch
    {
        public long Id { get; set; }
        public long ItemId { get; set; }
        public string BatchCode { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public long CostPaise { get; set; }
        public long MrpPaise { get; set; }
        public long SellingPaise { get; set; }
        public int QtyGrams { get; set; }
        public int QtyUnits { get; set; }
        public long? SupplierId { get; set; }
        public long? PurchaseLineId { get; set; }
        public DateTime ReceivedAt { get; set; }
    }

    public class Bill
    {
        public long Id { get; set; }
        public long BillNo { get; set; }
        public int CounterId { get; set; }
        public long UserId { get; set; }
        public long? CustomerId { get; set; }
        public DateTime BilledAt { get; set; }
        public BillStatus Status { get; set; }
        public long SubtotalPaise { get; set; }
        public long DiscountPaise { get; set; }
        public long TaxablePaise { get; set; }
        public long CgstPaise { get; set; }
        public long SgstPaise { get; set; }
        public long RoundOffPaise { get; set; }
        public long NetPaise { get; set; }
        public bool IsCreditSale { get; set; }
        public long? CancelledBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string CancelReason { get; set; }
        public List<BillLine> Lines { get; set; } = new List<BillLine>();
        public List<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class BillLine
    {
        public long Id { get; set; }
        public long BillId { get; set; }
        public int LineNo { get; set; }
        public long ItemId { get; set; }
        public long? BatchId { get; set; }
        public int QtyUnits { get; set; }
        public int QtyGrams { get; set; }
        public WeightSource WeightSource { get; set; }
        public int RawGrams { get; set; }
        public long RatePaise { get; set; }
        public long DiscountPaise { get; set; }
        public int TaxRateBp { get; set; }
        public long TaxPaise { get; set; }
        public long AmountPaise { get; set; }
        public string HsnCode { get; set; }
        public string ItemName { get; set; } // convenience, joined from items
    }

    public class Payment
    {
        public long Id { get; set; }
        public long BillId { get; set; }
        public PaymentMode Mode { get; set; }
        public long AmountPaise { get; set; }
        public string Reference { get; set; }
    }

    public class Customer
    {
        public long Id { get; set; }
        public string Phone { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public long CreditLimitPaise { get; set; }
        public bool CreditAllowed { get; set; }
        public long OpeningBalancePaise { get; set; }
        public DateTime? OpeningBalanceAt { get; set; }
        public long CurrentBalancePaise { get; set; }
        public long LoyaltyPoints { get; set; }
        public DateTime Since { get; set; }
        public DateTime? LastTxnAt { get; set; }
        public string Notes { get; set; }
        public bool IsActive { get; set; }
    }

    public class LedgerEntry
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public DateTime At { get; set; }
        public LedgerType Type { get; set; }
        public string RefTable { get; set; }
        public long? RefId { get; set; }
        public string Description { get; set; }
        public long DebitPaise { get; set; }
        public long CreditPaise { get; set; }
        public long BalancePaise { get; set; }
        public long? ReversesLedgerId { get; set; }
        public long UserId { get; set; }
        public int? CounterId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Purchase
    {
        public long Id { get; set; }
        public long SupplierId { get; set; }
        public string InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public long GoodsPaise { get; set; }
        public long TaxPaise { get; set; }
        public long FreightPaise { get; set; }
        public long DiscountPaise { get; set; }
        public long TotalPaise { get; set; }
        public string PaymentMode { get; set; }
        public DateTime? DueDate { get; set; }
        public List<PurchaseLine> Lines { get; set; } = new List<PurchaseLine>();
    }

    public class PurchaseLine
    {
        public long Id { get; set; }
        public long PurchaseId { get; set; }
        public long ItemId { get; set; }
        public string BatchCode { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int QtyUnits { get; set; }
        public int QtyGrams { get; set; }
        public int FreeUnits { get; set; }
        public int FreeGrams { get; set; }
        public long CostPaise { get; set; }
        public long MrpPaise { get; set; }
        public long ValuePaise { get; set; }
    }

    public class Shift
    {
        public long Id { get; set; }
        public int CounterId { get; set; }
        public long UserId { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public long OpeningFloatPaise { get; set; }
        public long ExpectedCashPaise { get; set; }
        public long CountedCashPaise { get; set; }
        public long DifferencePaise { get; set; }
        public ShiftStatus Status { get; set; }
    }

    public class CreditPayment
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public long AmountPaise { get; set; }
        public PaymentMode Mode { get; set; }
        public string Reference { get; set; }
        public DateTime ReceivedAt { get; set; }
        public long ReceivedBy { get; set; }
        public long? ShiftId { get; set; }
        public string Note { get; set; }
        public bool IsReversed { get; set; }
    }
}
