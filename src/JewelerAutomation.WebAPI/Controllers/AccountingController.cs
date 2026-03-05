using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Services;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _accountingService;

    public AccountingController(IAccountingService accountingService) => _accountingService = accountingService;

    /// <summary>
    /// Has Gram (sade): Miktar * Milyem / 1000. Excel: Kasa D, Alış O, Cari GR-HAS.
    /// </summary>
    [HttpPost("has-gram")]
    public ActionResult<decimal> CalculateHasGram([FromBody] HasGramRequest request)
    {
        var result = _accountingService.CalculateHasGram(request.Quantity, request.Milyem);
        return Ok(result);
    }

    /// <summary>
    /// Has Gram (işçilik dahil): (Miktar * Milyem / 1000) + Toplamİşçilik. Excel: SATIŞ G.
    /// </summary>
    [HttpPost("has-gram-with-labour")]
    public ActionResult<decimal> CalculateHasGramWithLabour([FromBody] HasGramWithLabourRequest request)
    {
        var result = _accountingService.CalculateHasGramWithLabour(request.Quantity, request.Milyem, request.TotalLabour);
        return Ok(result);
    }

    /// <summary>
    /// Toplam İşçilik: ±(Adet * Birimİşçilik * 0.01). Excel: F.
    /// </summary>
    [HttpPost("total-labour")]
    public ActionResult<decimal> CalculateTotalLabour([FromBody] TotalLabourRequest request)
    {
        var result = _accountingService.CalculateTotalLabour(request.PieceCount, request.UnitLabour, request.Subtract);
        return Ok(result);
    }

    /// <summary>
    /// Milyem İşçilik (916 üstü): (Milyem - 916) * Miktar * 0.001. Excel: J.
    /// </summary>
    [HttpPost("milyem-labour")]
    public ActionResult<decimal> CalculateMilyemLabour([FromBody] MilyemLabourRequest request)
    {
        var result = _accountingService.CalculateMilyemLabour(request.Quantity, request.Milyem, request.OnlyWhenAlindi);
        return Ok(result);
    }
}

public record HasGramRequest(decimal Quantity, decimal Milyem);
public record HasGramWithLabourRequest(decimal Quantity, decimal Milyem, decimal TotalLabour);
public record TotalLabourRequest(int PieceCount, decimal UnitLabour, bool Subtract = true);
public record MilyemLabourRequest(decimal Quantity, decimal Milyem, bool OnlyWhenAlindi = false);
