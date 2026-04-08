using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/customers/{customerId:guid}/account")]
[Authorize]
public class CustomerAccountController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingService _accounting;
    private readonly ILedgerService _ledger;

    public CustomerAccountController(IUnitOfWork unitOfWork, IAccountingService accounting, ILedgerService ledger)
    {
        _unitOfWork = unitOfWork;
        _accounting = accounting;
        _ledger = ledger;
    }

    /// <summary>
    /// Cari defter bakiyesi (altın has + nakit para birimleri).
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<CustomerBalanceDto>> GetCustomerBalance(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null) return NotFound();

        var book = await _unitOfWork.CustomerTransactions.GetBalanceAsync(customerId, cancellationToken).ConfigureAwait(false);
        return Ok(new CustomerBalanceDto(
            customerId,
            customer.Name,
            book.GoldHasGram,
            book.CashTry,
            book.CashUsd,
            book.CashEur,
            book.CashGbp));
    }

    /// <summary>
    /// Hesap ekstresi — aynı sepetten gelen şahıs emanet satırları tek satırda gruplanır.
    /// </summary>
    [HttpGet("statement")]
    public async Task<ActionResult<IReadOnlyList<CustomerStatementEntryDto>>> GetCustomerStatement(
        Guid customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null) return NotFound();

        var list = await _unitOfWork.CustomerTransactions.GetStatementAsync(customerId, from, to, cancellationToken).ConfigureAwait(false);
        var entries = await BuildStatementEntriesAsync(list, cancellationToken).ConfigureAwait(false);
        return Ok(entries);
    }

    /// <summary>
    /// Şahıs: eski bakiye / devir — yalnız cari defterine yazılır; kasa ve fiziki kasa defterine dokunulmaz.
    /// </summary>
    [HttpPost("sahis/opening-balance")]
    public async Task<ActionResult<CustomerTransactionDto>> PostSahisOpeningBalance(
        Guid customerId,
        [FromBody] SahisOpeningBalanceRequestDto dto,
        CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null) return NotFound();
        if (customer.Type != CustomerType.Sahis)
            return BadRequest("Eski bakiye (devir) yalnız şahıs kayıtları için kullanılabilir.");
        if (dto.Amount <= 0)
            return BadRequest("Tutar sıfırdan büyük olmalıdır.");

        var cashCur = dto.AssetKind switch
        {
            SahisOpeningAssetKind.Try => CashCurrency.Try,
            SahisOpeningAssetKind.Usd => CashCurrency.Usd,
            SahisOpeningAssetKind.Eur => CashCurrency.Eur,
            SahisOpeningAssetKind.Gbp => CashCurrency.Gbp,
            _ => CashCurrency.Try
        };

        var desc = string.IsNullOrWhiteSpace(dto.Description)
            ? $"Eski bakiye (devir): {dto.AssetKind} — {(dto.CustomerIsCreditor ? "müşteri alacaklı" : "müşteri borçlu")}"
            : dto.Description.Trim();

        var entity = new CustomerTransaction
        {
            CustomerId = customerId,
            TransactionDate = dto.TransactionDate,
            TransactionType = CustomerTransactionType.OpeningBalance,
            GoldGram = 0,
            GoldMilyem = 0,
            GoldHas = dto.AssetKind == SahisOpeningAssetKind.Gold ? dto.Amount : 0,
            CashAmount = dto.AssetKind == SahisOpeningAssetKind.Gold ? 0 : dto.Amount,
            CashCurrency = dto.AssetKind == SahisOpeningAssetKind.Gold ? CashCurrency.Try : cashCur,
            PostToLedger = false,
            OpeningAssetKind = dto.AssetKind,
            OpeningCustomerIsCreditor = dto.CustomerIsCreditor,
            Description = desc,
        };

        await _unitOfWork.CustomerTransactions.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetCustomerStatement), new { customerId }, MapToCustomerTransactionDto(entity));
    }

    /// <summary>
    /// Yeni cari hesap hareketi (deftere de yansır, <see cref="CreateCustomerTransactionRequest.PostToLedger"/> true ise).
    /// </summary>
    [HttpPost("transactions")]
    public async Task<ActionResult<CustomerTransactionDto>> CreateCustomerTransaction(Guid customerId, [FromBody] CreateCustomerTransactionRequest dto, CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (customer == null) return NotFound();

        if (dto.TransactionType is CustomerTransactionType.OpeningBalance or CustomerTransactionType.SahisEmanetLiability)
            return BadRequest("Bu türler doğrudan oluşturulamaz.");

        decimal goldHas = dto.GoldHas;
        if (dto.TransactionType is CustomerTransactionType.GoldPurchase or CustomerTransactionType.GoldSale)
        {
            if (dto.GoldGram > 0 && dto.GoldMilyem >= 0)
                goldHas = _accounting.CalculateHasGram(dto.GoldGram, dto.GoldMilyem);
        }

        var cashCurrency = (CashCurrency)Math.Clamp(dto.CashCurrency, 0, 3);

        var entity = new CustomerTransaction
        {
            CustomerId = customerId,
            TransactionDate = dto.TransactionDate,
            TransactionType = dto.TransactionType,
            GoldGram = dto.GoldGram,
            GoldMilyem = dto.GoldMilyem,
            GoldHas = goldHas,
            CashAmount = dto.CashAmount,
            CashCurrency = cashCurrency,
            PostToLedger = dto.PostToLedger,
            Description = dto.Description
        };
        await _unitOfWork.CustomerTransactions.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        if (entity.PostToLedger)
        {
            await _ledger.RecordCustomerTransactionAsync(
                transactionDate: entity.TransactionDate,
                transactionType: entity.TransactionType,
                goldHasAmount: goldHas,
                cashAmount: entity.CashAmount,
                customerId: customerId,
                referenceId: entity.Id,
                description: entity.Description,
                cashCurrency: cashCurrency,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetCustomerStatement), new { customerId }, MapToCustomerTransactionDto(entity));
    }

    [HttpDelete("/api/customer-transactions/{id:guid}")]
    public async Task<ActionResult> DeleteTransaction(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _unitOfWork.CustomerTransactions.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (transaction == null) return NotFound();

        if (transaction.SourceBasketTransactionId.HasValue)
            return BadRequest("Sepete bağlı hareketler yalnızca ilgili sepet silinerek kaldırılabilir.");

        if (transaction.PostToLedger)
        {
            await _ledger.DeleteEntriesByReferenceAsync(
                LedgerReferenceType.CustomerTransaction,
                id,
                cancellationToken
            ).ConfigureAwait(false);
        }

        _unitOfWork.CustomerTransactions.Delete(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    [HttpPut("/api/customer-transactions/{id:guid}")]
    public async Task<ActionResult<CustomerTransactionDto>> UpdateTransaction(
        Guid id,
        [FromBody] CreateCustomerTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = await _unitOfWork.CustomerTransactions.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (transaction == null) return NotFound();

        if (transaction.SourceBasketTransactionId.HasValue)
            return BadRequest("Sepete bağlı hareketler bu ekrandan güncellenemez; sepeti düzenleyin veya silin.");

        if (request.TransactionType is CustomerTransactionType.OpeningBalance or CustomerTransactionType.SahisEmanetLiability)
            return BadRequest("Bu türler bu uçtan güncellenemez.");

        decimal goldHas = request.GoldHas;
        if (request.TransactionType is CustomerTransactionType.GoldPurchase or CustomerTransactionType.GoldSale)
        {
            if (request.GoldGram > 0 && request.GoldMilyem >= 0)
                goldHas = _accounting.CalculateHasGram(request.GoldGram, request.GoldMilyem);
        }

        var cashCurrency = (CashCurrency)Math.Clamp(request.CashCurrency, 0, 3);

        transaction.TransactionDate = request.TransactionDate;
        transaction.TransactionType = request.TransactionType;
        transaction.GoldGram = request.GoldGram;
        transaction.GoldMilyem = request.GoldMilyem;
        transaction.GoldHas = goldHas;
        transaction.CashAmount = request.CashAmount;
        transaction.CashCurrency = cashCurrency;
        transaction.PostToLedger = request.PostToLedger;
        transaction.Description = request.Description;

        _unitOfWork.CustomerTransactions.Update(transaction);

        await _ledger.DeleteEntriesByReferenceAsync(
            LedgerReferenceType.CustomerTransaction,
            id,
            cancellationToken
        ).ConfigureAwait(false);

        if (transaction.PostToLedger)
        {
            await _ledger.RecordCustomerTransactionAsync(
                transactionDate: request.TransactionDate,
                transactionType: request.TransactionType,
                goldHasAmount: goldHas,
                cashAmount: request.CashAmount,
                customerId: transaction.CustomerId,
                referenceId: id,
                description: request.Description,
                cashCurrency: cashCurrency,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(MapToCustomerTransactionDto(transaction));
    }

    private static CustomerTransactionDto MapToCustomerTransactionDto(CustomerTransaction t) =>
        new(
            t.Id,
            t.TransactionDate,
            t.TransactionType,
            t.GoldGram,
            t.GoldMilyem,
            t.GoldHas,
            t.CashAmount,
            (int)t.CashCurrency,
            t.PostToLedger,
            t.OpeningAssetKind.HasValue ? (int)t.OpeningAssetKind.Value : null,
            t.OpeningCustomerIsCreditor,
            t.SourceBasketTransactionId,
            t.Description);

    private async Task<IReadOnlyList<CustomerStatementEntryDto>> BuildStatementEntriesAsync(
        IReadOnlyList<CustomerTransaction> list,
        CancellationToken cancellationToken)
    {
        var sorted = list.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.CreatedAt).ToList();
        var processedBaskets = new HashSet<Guid>();
        var result = new List<CustomerStatementEntryDto>();

        foreach (var t in sorted)
        {
            if (t.SourceBasketTransactionId is { } bid
                && t.TransactionType == CustomerTransactionType.SahisEmanetLiability)
            {
                if (!processedBaskets.Add(bid))
                    continue;

                var items = list.Where(x => x.SourceBasketTransactionId == bid && x.TransactionType == CustomerTransactionType.SahisEmanetLiability)
                    .OrderByDescending(x => x.TransactionDate)
                    .ThenByDescending(x => x.CreatedAt)
                    .ToList();
                var basketTx = await _unitOfWork.Transactions.GetByIdAsync(bid, cancellationToken).ConfigureAwait(false);
                result.Add(BuildBasketGroupEntry(items, basketTx));
                continue;
            }

            result.Add(BuildSingleEntry(t));
        }

        return result;
    }

    private static CustomerStatementEntryDto BuildSingleEntry(CustomerTransaction t)
    {
        var (nt, nu, ne, ng) = GetSignedCashByCurrency(t);
        var milyem = GetDisplayMilyemForSingle(t);
        return new CustomerStatementEntryDto(
            EntryId: t.Id,
            PrimaryTransactionId: t.Id,
            IsBasketGroup: false,
            SourceBasketTransactionId: t.SourceBasketTransactionId,
            TransactionDate: t.TransactionDate,
            TransactionType: t.TransactionType,
            TotalGoldHas: t.GoldHas,
            SumGoldGram: t.GoldGram,
            DisplayMilyem: milyem,
            NetCashTry: nt,
            NetCashUsd: nu,
            NetCashEur: ne,
            NetCashGbp: ng,
            PostToLedger: t.PostToLedger,
            OpeningAssetKind: t.OpeningAssetKind.HasValue ? (int)t.OpeningAssetKind.Value : null,
            OpeningCustomerIsCreditor: t.OpeningCustomerIsCreditor,
            Description: t.Description,
            CanDelete: t.SourceBasketTransactionId == null,
            CanEdit: t.SourceBasketTransactionId == null && t.TransactionType is not CustomerTransactionType.OpeningBalance and not CustomerTransactionType.SahisEmanetLiability,
            LineItems: []);
    }

    private static CustomerStatementEntryDto BuildBasketGroupEntry(IReadOnlyList<CustomerTransaction> items, Transaction? basketTx)
    {
        if (items.Count == 0)
            throw new ArgumentException("Basket group requires at least one line.", nameof(items));

        var bid = items[0].SourceBasketTransactionId!.Value;
        var date = items.Max(x => x.TransactionDate);
        var totalHas = items.Sum(x => x.GoldHas);
        var sumGram = items.Sum(x => x.GoldGram);

        decimal? displayMilyem = null;
        if (basketTx?.Items is { Count: > 0 } txItems)
        {
            var weighted = txItems.Where(i => i.HasGram > 0.000001m).ToList();
            if (weighted.Count > 0)
                displayMilyem = Math.Round(weighted.Sum(i => i.HasGram * i.Milyem) / weighted.Sum(i => i.HasGram), 4);
        }

        var lineDtos = items.Select(MapToCustomerTransactionDto).ToList();

        return new CustomerStatementEntryDto(
            EntryId: bid,
            PrimaryTransactionId: null,
            IsBasketGroup: true,
            SourceBasketTransactionId: bid,
            TransactionDate: date,
            TransactionType: CustomerTransactionType.SahisEmanetLiability,
            TotalGoldHas: totalHas,
            SumGoldGram: sumGram,
            DisplayMilyem: displayMilyem,
            NetCashTry: basketTx?.NetCashAmount ?? 0,
            NetCashUsd: basketTx?.NetCashAmountUsd ?? 0,
            NetCashEur: basketTx?.NetCashAmountEur ?? 0,
            NetCashGbp: basketTx?.NetCashAmountGbp ?? 0,
            PostToLedger: false,
            OpeningAssetKind: null,
            OpeningCustomerIsCreditor: null,
            Description: items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Description))?.Description ?? "Şahıs emanet (sepet)",
            CanDelete: false,
            CanEdit: false,
            LineItems: lineDtos);
    }

    private static decimal? GetDisplayMilyemForSingle(CustomerTransaction t)
    {
        if (t.TransactionType is not (CustomerTransactionType.GoldPurchase or CustomerTransactionType.GoldSale))
            return null;
        if (t.GoldGram <= 0)
            return null;
        return t.GoldMilyem;
    }

    /// <summary>Defter bakiyesi ile uyumlu: satırın TRY/USD/EUR/GBP net nakit etkisi (işaretli).</summary>
    private static (decimal tryB, decimal usd, decimal eur, decimal gbp) GetSignedCashByCurrency(CustomerTransaction t)
    {
        decimal tr = 0, u = 0, e = 0, g = 0;
        void Add(CashCurrency c, decimal amt)
        {
            switch (c)
            {
                case CashCurrency.Try: tr += amt; break;
                case CashCurrency.Usd: u += amt; break;
                case CashCurrency.Eur: e += amt; break;
                case CashCurrency.Gbp: g += amt; break;
            }
        }

        switch (t.TransactionType)
        {
            case CustomerTransactionType.CashPayment:
                Add(t.CashCurrency, -t.CashAmount);
                break;
            case CustomerTransactionType.CashCollection:
                Add(t.CashCurrency, t.CashAmount);
                break;
            case CustomerTransactionType.OpeningBalance:
                ApplyOpeningCashToSigned(t, Add);
                break;
        }

        return (tr, u, e, g);
    }

    private static void ApplyOpeningCashToSigned(CustomerTransaction t, Action<CashCurrency, decimal> addCash)
    {
        if (t.OpeningAssetKind is not { } kind || t.OpeningCustomerIsCreditor is not { } isCreditor)
            return;
        if (kind == SahisOpeningAssetKind.Gold)
            return;

        var sign = isCreditor ? 1m : -1m;
        var cur = kind switch
        {
            SahisOpeningAssetKind.Try => CashCurrency.Try,
            SahisOpeningAssetKind.Usd => CashCurrency.Usd,
            SahisOpeningAssetKind.Eur => CashCurrency.Eur,
            SahisOpeningAssetKind.Gbp => CashCurrency.Gbp,
            _ => CashCurrency.Try
        };
        addCash(cur, sign * t.CashAmount);
    }
}

public record CustomerBalanceDto(
    Guid CustomerId,
    string CustomerName,
    decimal GoldBalance,
    decimal CashBalanceTry,
    decimal CashBalanceUsd,
    decimal CashBalanceEur,
    decimal CashBalanceGbp);

public record CustomerTransactionDto(
    Guid Id,
    DateTime TransactionDate,
    CustomerTransactionType TransactionType,
    decimal GoldGram,
    decimal GoldMilyem,
    decimal GoldHas,
    decimal CashAmount,
    int CashCurrency,
    bool PostToLedger,
    int? OpeningAssetKind,
    bool? OpeningCustomerIsCreditor,
    Guid? SourceBasketTransactionId,
    string? Description);

public record CustomerStatementEntryDto(
    Guid EntryId,
    Guid? PrimaryTransactionId,
    bool IsBasketGroup,
    Guid? SourceBasketTransactionId,
    DateTime TransactionDate,
    CustomerTransactionType TransactionType,
    decimal TotalGoldHas,
    decimal SumGoldGram,
    decimal? DisplayMilyem,
    decimal NetCashTry,
    decimal NetCashUsd,
    decimal NetCashEur,
    decimal NetCashGbp,
    bool PostToLedger,
    int? OpeningAssetKind,
    bool? OpeningCustomerIsCreditor,
    string? Description,
    bool CanDelete,
    bool CanEdit,
    IReadOnlyList<CustomerTransactionDto> LineItems);

public record CreateCustomerTransactionRequest(
    DateTime TransactionDate,
    CustomerTransactionType TransactionType,
    decimal GoldGram,
    decimal GoldMilyem,
    decimal GoldHas,
    decimal CashAmount,
    string? Description,
    int CashCurrency = 0,
    bool PostToLedger = true);

public record SahisOpeningBalanceRequestDto(
    DateTime TransactionDate,
    SahisOpeningAssetKind AssetKind,
    decimal Amount,
    bool CustomerIsCreditor,
    string? Description);
