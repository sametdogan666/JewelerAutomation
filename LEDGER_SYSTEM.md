# Ledger-Based Transaction System

## Overview

The system now uses a unified **ledger-based accounting system** that provides:

- ✅ **Immutable audit trail** - All financial movements are recorded
- ✅ **Dynamic balance calculation** - No stored balances, calculated on-demand
- ✅ **Single source of truth** - All transactions flow through the ledger
- ✅ **Financial accuracy** - SUM(In) - SUM(Out) methodology
- ✅ **Traceability** - Every entry links to its source transaction

---

## Architecture

### Core Entity: `LedgerEntry`

```csharp
public class LedgerEntry : BaseEntity
{
    public DateTime TransactionDate { get; set; }
    public LedgerEntryType EntryType { get; set; }       // GoldIn, GoldOut, CashIn, CashOut
    public decimal GoldHasAmount { get; set; }
    public decimal CashAmount { get; set; }
    public LedgerReferenceType ReferenceType { get; set; } // Transaction, CustomerTransaction, SafeMovement, etc.
    public Guid? ReferenceId { get; set; }                // Links to source transaction
    public Guid? CustomerId { get; set; }                 // Optional customer link
    public string? Description { get; set; }
}
```

### Entry Types

- **`GoldIn`** - Gold enters the system (purchase, capital injection)
- **`GoldOut`** - Gold leaves the system (sale, withdrawal)
- **`CashIn`** - Cash enters the system (sale, payment received)
- **`CashOut`** - Cash leaves the system (purchase, payment made)

### Reference Types

- **`Transaction`** - Buy/Sell transactions (Alış/Satış)
- **`CustomerTransaction`** - Customer account movements
- **`SafeMovement`** - Safe gold movements (capital, manual adjustments)
- **`CashPegging`** - Cash-to-gold conversion records
- **`ManualAdjustment`** - Manual corrections

---

## How It Works

### 1. **Transaction Recording**

When a sale happens:
```
Sale Transaction → Creates TWO ledger entries:
  1. LedgerEntry(GoldOut, goldAmount, Transaction, transactionId)
  2. LedgerEntry(CashIn, cashAmount, Transaction, transactionId)
```

When a purchase happens:
```
Purchase Transaction → Creates TWO ledger entries:
  1. LedgerEntry(GoldIn, goldAmount, Transaction, transactionId)
  2. LedgerEntry(CashOut, cashAmount, Transaction, transactionId)
```

### 2. **Balance Calculation**

All balances are calculated dynamically:

```sql
-- Gold Balance
SELECT SUM(GoldHasAmount) 
FROM LedgerEntries 
WHERE EntryType = 'GoldIn'
  MINUS
SELECT SUM(GoldHasAmount) 
FROM LedgerEntries 
WHERE EntryType = 'GoldOut'

-- Cash Balance
SELECT SUM(CashAmount) 
FROM LedgerEntries 
WHERE EntryType = 'CashIn'
  MINUS
SELECT SUM(CashAmount) 
FROM LedgerEntries 
WHERE EntryType = 'CashOut'
```

### 3. **Customer Balances**

Same logic with `WHERE CustomerId = @customerId` filter.

---

## API Endpoints

### Get Balances

**GET** `/api/ledger/balances`

Returns total gold and cash balances:
```json
{
  "totalGoldBalance": 1234.56,
  "totalCashBalance": 567890.12,
  "safeGoldBalance": 1234.56,
  "safeCashBalance": 567890.12
}
```

### Get Customer Balances

**GET** `/api/ledger/customer/{customerId}/balances`

Returns customer-specific balances:
```json
{
  "customerId": "guid",
  "goldBalance": 123.45,
  "cashBalance": 67890.12
}
```

### Get Ledger Entries

**GET** `/api/ledger/entries?from=2024-01-01&to=2024-12-31`

Returns all ledger entries in the specified period.

### Get Customer Ledger Entries

**GET** `/api/ledger/customer/{customerId}/entries?from=2024-01-01&to=2024-12-31`

Returns customer-specific ledger entries.

### Migrate Existing Data

**POST** `/api/ledger/migrate`

One-time migration endpoint to populate the ledger from existing:
- Transactions
- CustomerTransactions
- SafeMovements

**⚠️ Important:** This endpoint is idempotent - it will skip migration if ledger already has data.

---

## Migration Process

### Step 1: Apply Database Migration

```bash
dotnet ef database update --project src/JewelerAutomation.Infrastructure --startup-project src/JewelerAutomation.WebAPI
```

This creates the `LedgerEntries` table.

### Step 2: Migrate Existing Data

Call the migration endpoint:
```bash
POST http://localhost:5000/api/ledger/migrate
Authorization: Bearer {token}
```

This will:
1. Check if ledger is empty
2. Read all existing transactions
3. Create corresponding ledger entries
4. Preserve historical data

### Step 3: Verify Balances

Compare old vs new balance calculations:
```bash
# Old system (still works)
GET /api/safe/status

# New system
GET /api/ledger/balances
```

Both should return the same values.

---

## Code Integration

### Automatic Ledger Recording

All transaction operations now automatically write to the ledger:

**TransactionsController:**
- `Create()` → Records gold and cash movements
- `Update()` → Deletes old entries, records new ones
- `Delete()` → Removes ledger entries

**CustomerAccountController:**
- `CreateCustomerTransaction()` → Records customer account movements
- `UpdateTransaction()` → Updates ledger entries
- `DeleteTransaction()` → Removes ledger entries

### Balance Calculations

**SafeStatusService** now uses ledger:
```csharp
var balances = await _ledger.GetBalancesAsync(cancellationToken);
var actualGold = balances.SafeGoldBalance;
var cashBalance = balances.SafeCashBalance;
```

---

## Benefits

### 1. **Financial Integrity**
- No balance corruption from failed transactions
- Every movement is traceable
- Audit trail is complete and immutable

### 2. **Historical Analysis**
- Query balances at any point in time
- Period-based profit calculations
- Customer activity analysis

### 3. **Reconciliation**
- Easy to verify balances: `SUM(In) - SUM(Out)`
- Detect discrepancies quickly
- Identify missing or duplicate entries

### 4. **Scalability**
- Indexed queries for fast balance calculation
- Efficient period-based filtering
- Customer-specific balance queries optimized

---

## Database Schema

### Table: `LedgerEntries`

| Column | Type | Description |
|--------|------|-------------|
| Id | uuid | Primary key |
| TransactionDate | timestamp | When the movement occurred |
| EntryType | int | 0=GoldIn, 1=GoldOut, 2=CashIn, 3=CashOut |
| GoldHasAmount | decimal(18,6) | Gold amount in has grams |
| CashAmount | decimal(18,6) | Cash amount in TL |
| ReferenceType | int | Source type (Transaction, CustomerTransaction, etc.) |
| ReferenceId | uuid | Links to source record |
| CustomerId | uuid | Optional customer reference |
| Description | varchar(512) | Human-readable description |
| CreatedAt | timestamp | Record creation time |
| UpdatedAt | timestamp | Last modification time |

### Indexes

- `IX_LedgerEntries_TransactionDate` - Fast date range queries
- `IX_LedgerEntries_CustomerId` - Customer balance queries
- `IX_LedgerEntries_ReferenceType_ReferenceId` - Traceability

---

## Testing

### Verify Gold Balance

```bash
# Get from ledger
GET /api/ledger/balances

# Compare with old safe balance
GET /api/safe/status
```

### Verify Customer Balance

```bash
# New ledger system
GET /api/ledger/customer/{customerId}/balances

# Old system (still works)
GET /api/customers/{customerId}/account/balance
```

### Audit Trail

```bash
# View all movements for a customer
GET /api/ledger/customer/{customerId}/entries

# View all movements in a period
GET /api/ledger/entries?from=2024-01-01&to=2024-12-31
```

---

## Backward Compatibility

The old transaction tables still exist and are still updated:
- `Transactions`
- `CustomerTransactions`
- `SafeMovements`

The ledger system runs **in parallel** with the old system. This allows for:
- Gradual transition
- A/B testing of calculations
- Rollback capability if needed

---

## Next Steps

### Phase 2 (Future)
- Migrate all balance queries to use ledger
- Deprecate direct balance calculations from old tables
- Add ledger-based reporting endpoints
- Implement balance snapshots for performance optimization

### Phase 3 (Future)
- Remove redundant balance calculation code
- Archive old transaction tables (or keep for historical reference)
- Full cutover to ledger-based system

---

## Performance Considerations

### Current Implementation
- **No caching** - Balances calculated on every request
- **Full table scan** - SUM operations on entire table

### Optimization Strategies (Future)
1. **Balance snapshots** - Store daily/monthly balances in cache
2. **Materialized views** - Database-level pre-calculated balances
3. **Redis caching** - Cache frequently accessed balances
4. **Partial recalculation** - Calculate delta from last snapshot

---

## Security

- All ledger operations require authentication
- Ledger entries are append-only (no direct updates)
- Modifications create new entries with reference to original
- Full audit trail preserved

---

## Maintenance

### Check Ledger Integrity

```sql
-- Should return all unique reference IDs
SELECT ReferenceType, COUNT(DISTINCT ReferenceId) 
FROM LedgerEntries 
GROUP BY ReferenceType;

-- Verify no orphaned entries
SELECT * FROM LedgerEntries 
WHERE ReferenceType = 0 
AND ReferenceId NOT IN (SELECT Id FROM Transactions);
```

### Reconcile Balances

Compare ledger balances with legacy system:
```csharp
var ledgerGold = await _ledger.GetSafeGoldBalanceAsync();
var legacyGold = await _unitOfWork.SafeMovements.GetTotalHasGramBalanceAsync();
var difference = ledgerGold - legacyGold; // Should be 0
```

---

## Troubleshooting

### Issue: Balances don't match

**Solution:** Run migration again to ensure all historical data is in ledger:
```bash
POST /api/ledger/migrate
```

### Issue: Duplicate entries

**Solution:** Check ReferenceId - each source transaction should have unique ledger entries:
```sql
SELECT ReferenceId, COUNT(*) 
FROM LedgerEntries 
GROUP BY ReferenceId 
HAVING COUNT(*) > 2; -- Sale/Purchase creates 2 entries max
```

### Issue: Performance slow

**Solution:** Ensure indexes are created:
```sql
CREATE INDEX IF NOT EXISTS IX_LedgerEntries_TransactionDate ON LedgerEntries(TransactionDate);
CREATE INDEX IF NOT EXISTS IX_LedgerEntries_CustomerId ON LedgerEntries(CustomerId);
CREATE INDEX IF NOT EXISTS IX_LedgerEntries_ReferenceType_ReferenceId ON LedgerEntries(ReferenceType, ReferenceId);
```

---

## Summary

The ledger-based system provides:
- ✅ **Financial accuracy** through SUM-based calculations
- ✅ **Complete audit trail** for compliance
- ✅ **Simplified balance logic** (no manual updates)
- ✅ **Historical analysis** capabilities
- ✅ **Backward compatibility** during transition

All new transactions automatically write to the ledger. Legacy tables remain for reference and backward compatibility.
