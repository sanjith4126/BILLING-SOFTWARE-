namespace GroceryPos.Domain
{
    public enum SoldBy { Piece, Weight, Volume }
    public enum PaymentMode { Cash, Upi, Card, Khata }
    public enum BillStatus { Completed, Cancelled }
    public enum WeightSource { Na, Scale, Label, Manual }
    public enum StockReason { Sale, Purchase, ReturnToSupplier, Damage, Wastage, StockTake, Conversion }
    public enum UserRole { Owner, Manager, Cashier }
    public enum LedgerType { Opening, CreditSale, Payment, Discount, WriteOff, Adjustment, Reversal }
    public enum ShiftStatus { Open, Closed }
    public enum WeightMode { Serial, Label, Manual }
}
