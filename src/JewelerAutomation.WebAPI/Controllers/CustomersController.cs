using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICustomerService _customerService;

    public CustomersController(IUnitOfWork unitOfWork, ICustomerService customerService)
    {
        _unitOfWork = unitOfWork;
        _customerService = customerService;
    }

    /// <summary>Aktif cariler (varsayılan). Raporlar için pasif dahil: ?includeInactive=true</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Customer>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var list = await _unitOfWork.Customers.GetAllAsync(cancellationToken, includeInactive).ConfigureAwait(false);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Customer>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _unitOfWork.Customers.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<Customer>> Create([FromBody] CustomerCreateDto dto, CancellationToken cancellationToken)
    {
        var entity = new Customer
        {
            Name = dto.Name,
            Phone = dto.Phone,
            Address = dto.Address,
            Type = dto.Type,
            Description = dto.Description,
            IsActive = true
        };
        await _unitOfWork.Customers.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] CustomerUpdateDto dto, CancellationToken cancellationToken)
    {
        var entity = await _unitOfWork.Customers.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity == null) return NotFound();
        entity.Name = dto.Name;
        entity.Phone = dto.Phone;
        entity.Address = dto.Address;
        entity.Type = dto.Type;
        entity.Description = dto.Description;
        _unitOfWork.Customers.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _customerService.TryDeleteCustomerAsync(id, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            CustomerDeleteResult.NotFound => NotFound(),
            CustomerDeleteResult.BlockedNonZeroBalance => BadRequest(CustomerService.NonZeroBalanceMessage),
            CustomerDeleteResult.SoftDeleted or CustomerDeleteResult.HardDeleted => NoContent(),
            _ => NoContent()
        };
    }
}

public record CustomerCreateDto(string Name, string? Phone, string? Address, CustomerType Type, string? Description);
public record CustomerUpdateDto(string Name, string? Phone, string? Address, CustomerType Type, string? Description);
