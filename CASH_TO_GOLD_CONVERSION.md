# Cash to Gold Conversion Feature

## Overview

This feature allows converting accumulated cash into gold (Has) at a specified price. The system automatically creates proper ledger transactions to maintain accurate financial records.

## Business Logic

### Conversion Formula

```
ConvertedGoldHas = CashAmount / HasPrice
```

**Example:**
- Cash Amount: 150,000 TL
- Has Price: 8,500 TL
- Result: 150,000 / 8,500 = **17.64 Has Gr**

### Financial Impact

When a conversion is confirmed:

1. **Cash Balance decreases** by the specified amount
2. **Gold Balance increases** by the calculated Has amount
3. Two ledger entries are created:
   - `CashOut` entry (records cash decrease)
   - `GoldIn` entry (records gold increase)

## Database Schema

### CashToGoldConversion Entity

```csharp
public class CashToGoldConversion : BaseEntity
{
    public DateTime TransactionDate { get; set; }
    public decimal CashAmount { get; set; }
    public decimal HasPrice { get; set; }
    public decimal ConvertedGoldHas { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Description { get; set; }
    
    public Customer? Customer { get; set; }
}
```

**Fields:**
- `Id`: Unique identifier (inherited from BaseEntity)
- `TransactionDate`: When the conversion occurred
- `CashAmount`: Amount of cash being converted
- `HasPrice`: Current price per Has gram
- `ConvertedGoldHas`: Calculated gold amount (auto-calculated)
- `CustomerId`: Optional customer reference
- `Description`: Optional notes
- `CreatedAt`, `UpdatedAt`: Audit timestamps (inherited from BaseEntity)

**Indexes:**
- `TransactionDate` (for period queries)
- `CustomerId` (for customer-specific queries)

## API Endpoints

### Base URL: `/api/CashToGoldConversion`

All endpoints require authentication (`[Authorize]`).

---

### 1. Get All Conversions

```http
GET /api/CashToGoldConversion
```

**Query Parameters:**
- `from` (DateTime, optional): Start date filter
- `to` (DateTime, optional): End date filter

**Response:** `200 OK`
```json
[
  {
    "id": "guid",
    "transactionDate": "2026-03-09T10:30:00Z",
    "cashAmount": 150000.00,
    "hasPrice": 8500.00,
    "convertedGoldHas": 17.64,
    "customerId": "guid or null",
    "description": "Nakit-Altın Dönüşümü: 150,000.00 TL → 17.64 Has Gr"
  }
]
```

---

### 2. Get Conversion by ID

```http
GET /api/CashToGoldConversion/{id}
```

**Response:** `200 OK` or `404 Not Found`

---

### 3. Get Conversions by Customer

```http
GET /api/CashToGoldConversion/customer/{customerId}
```

**Response:** `200 OK` (array of conversions) or `404 Not Found` (customer doesn't exist)

---

### 4. Calculate Conversion (Preview)

```http
POST /api/CashToGoldConversion/calculate
```

**Request Body:**
```json
{
  "cashAmount": 150000.00,
  "hasPrice": 8500.00
}
```

**Response:** `200 OK`
```json
{
  "cashAmount": 150000.00,
  "hasPrice": 8500.00,
  "convertedGoldHas": 17.647059
}
```

Use this endpoint to preview the conversion result before creating it.

---

### 5. Create Conversion

```http
POST /api/CashToGoldConversion
```

**Request Body:**
```json
{
  "transactionDate": "2026-03-09T10:30:00Z",
  "cashAmount": 150000.00,
  "hasPrice": 8500.00,
  "customerId": "optional-guid",
  "description": "Optional description"
}
```

**Validation:**
- `cashAmount` must be > 0
- `hasPrice` must be > 0
- `customerId` must exist in database (if provided)

**Response:** `201 Created`
```json
{
  "id": "new-guid",
  "transactionDate": "2026-03-09T10:30:00Z",
  "cashAmount": 150000.00,
  "hasPrice": 8500.00,
  "convertedGoldHas": 17.647059,
  "customerId": "guid or null",
  "description": "Nakit-Altın Dönüşümü: 150,000.00 TL → 17.65 Has Gr"
}
```

**What Happens:**
1. Conversion record is created
2. Two ledger entries are created:
   - CashOut: -150,000.00 TL
   - GoldIn: +17.65 Has Gr
3. Balances are automatically updated via the ledger system

---

### 6. Delete Conversion

```http
DELETE /api/CashToGoldConversion/{id}
```

**Response:** `204 No Content` or `404 Not Found`

**What Happens:**
1. Associated ledger entries are deleted
2. Conversion record is deleted
3. Balances are recalculated from remaining ledger entries

---

### 7. Get Statistics

```http
GET /api/CashToGoldConversion/stats
```

**Response:** `200 OK`
```json
{
  "totalCashConverted": 500000.00,
  "totalGoldReceived": 58.82,
  "averagePrice": 8500.00,
  "conversionCount": 5
}
```

## Ledger Integration

### LedgerReferenceType

Added new enum value: `CashToGoldConversion`

```csharp
public enum LedgerReferenceType
{
    Transaction,
    CustomerTransaction,
    SafeMovement,
    CustomerMovement,
    CashPegging,
    CashToGoldConversion,  // NEW
    ManualAdjustment
}
```

### Ledger Entries Created

For each conversion, two entries are created:

**Entry 1: Cash Out**
```csharp
{
    TransactionDate = conversionDate,
    EntryType = LedgerEntryType.CashOut,
    CashAmount = cashAmount,
    GoldHasAmount = 0,
    ReferenceType = LedgerReferenceType.CashToGoldConversion,
    ReferenceId = conversionId,
    CustomerId = customerId (optional),
    Description = description
}
```

**Entry 2: Gold In**
```csharp
{
    TransactionDate = conversionDate,
    EntryType = LedgerEntryType.GoldIn,
    CashAmount = 0,
    GoldHasAmount = convertedGoldHas,
    ReferenceType = LedgerReferenceType.CashToGoldConversion,
    ReferenceId = conversionId,
    CustomerId = customerId (optional),
    Description = description
}
```

## Repository Methods

### ICashToGoldConversionRepository

```csharp
public interface ICashToGoldConversionRepository : IRepository<CashToGoldConversion>
{
    Task<IReadOnlyList<CashToGoldConversion>> GetByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashToGoldConversion>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalConvertedGoldAsync(CancellationToken cancellationToken = default);
}
```

## Service Methods

### ILedgerService

```csharp
Task RecordCashToGoldConversionAsync(
    DateTime transactionDate,
    decimal cashAmount,
    decimal goldHasAmount,
    Guid referenceId,
    Guid? customerId,
    string? description,
    CancellationToken cancellationToken = default);
```

## Usage Workflow

### Backend Workflow

1. User calls `/calculate` endpoint to preview conversion
2. User confirms and calls `/create` endpoint
3. System creates `CashToGoldConversion` record
4. System creates two ledger entries (CashOut + GoldIn)
5. Changes are saved to database
6. Balances are automatically updated via ledger system

### Querying Conversions

```csharp
// Get all conversions
var all = await _unitOfWork.CashToGoldConversions.GetAllAsync();

// Get by period
var period = await _unitOfWork.CashToGoldConversions.GetByPeriodAsync(
    DateTime.UtcNow.AddMonths(-1), 
    DateTime.UtcNow
);

// Get by customer
var customerConversions = await _unitOfWork.CashToGoldConversions.GetByCustomerAsync(customerId);

// Get total converted gold
var total = await _unitOfWork.CashToGoldConversions.GetTotalConvertedGoldAsync();
```

## Database Migration

Run the migration to create the table:

```bash
dotnet ef database update --project src/JewelerAutomation.Infrastructure --startup-project src/JewelerAutomation.WebAPI
```

This will create the `CashToGoldConversions` table with proper indexes and foreign keys.

## Testing

### Manual Testing

1. **Calculate Conversion:**
   ```bash
   POST /api/CashToGoldConversion/calculate
   {
     "cashAmount": 100000,
     "hasPrice": 8500
   }
   # Expected: convertedGoldHas = 11.76
   ```

2. **Create Conversion:**
   ```bash
   POST /api/CashToGoldConversion
   {
     "transactionDate": "2026-03-09T12:00:00Z",
     "cashAmount": 100000,
     "hasPrice": 8500,
     "description": "Test conversion"
   }
   ```

3. **Verify Ledger Entries:**
   ```bash
   GET /api/Ledger/entries?referenceType=CashToGoldConversion
   # Should show 2 entries (CashOut + GoldIn)
   ```

4. **Check Balances:**
   ```bash
   GET /api/Ledger/balances
   # Cash should decrease by 100000
   # Gold should increase by 11.76
   ```

5. **Delete and Verify:**
   ```bash
   DELETE /api/CashToGoldConversion/{id}
   GET /api/Ledger/balances
   # Balances should be restored
   ```

## Benefits

1. **Accurate Financial Records**: All conversions are tracked in the ledger
2. **Audit Trail**: Complete history of all cash-to-gold conversions
3. **Automatic Balance Updates**: No manual balance adjustments needed
4. **Customer Support**: Optional customer linking for detailed tracking
5. **Flexible Querying**: Filter by date, customer, or view statistics
6. **Safe Deletion**: Removes both conversion record and ledger entries

## Notes

- All amounts use `decimal(18,6)` precision for accuracy
- Timestamps are stored in UTC
- Conversion calculation is performed server-side for consistency
- Description is auto-generated if not provided
- Customer reference is optional but recommended for tracking
