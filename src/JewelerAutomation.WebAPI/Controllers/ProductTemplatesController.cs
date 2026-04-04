using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductTemplatesController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public ProductTemplatesController(IUnitOfWork uow) => _uow = uow;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductTemplateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var list = await _uow.ProductTemplates.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return Ok(list.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductTemplateDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var e = await _uow.ProductTemplates.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (e == null) return NotFound();
        return Ok(Map(e));
    }

    [HttpPost]
    public async Task<ActionResult<ProductTemplateDto>> Create([FromBody] ProductTemplateCreateDto dto, CancellationToken cancellationToken)
    {
        var entity = new ProductTemplate
        {
            Name = dto.Name.Trim(),
            MilyemSatis = dto.MilyemSatis,
            MilyemAlis = dto.MilyemAlis,
            DefaultGram = dto.DefaultGram,
            DefaultLaborPrice = dto.DefaultLaborPrice,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim(),
        };
        await _uow.ProductTemplates.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Map(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductTemplateDto>> Update(Guid id, [FromBody] ProductTemplateCreateDto dto, CancellationToken cancellationToken)
    {
        var entity = await _uow.ProductTemplates.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity == null) return NotFound();
        entity.Name = dto.Name.Trim();
        entity.MilyemSatis = dto.MilyemSatis;
        entity.MilyemAlis = dto.MilyemAlis;
        entity.DefaultGram = dto.DefaultGram;
        entity.DefaultLaborPrice = dto.DefaultLaborPrice;
        entity.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        _uow.ProductTemplates.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(Map(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _uow.ProductTemplates.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity == null) return NotFound();
        _uow.ProductTemplates.Delete(entity);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private static ProductTemplateDto Map(ProductTemplate x) => new(
        x.Id,
        x.Name,
        x.MilyemSatis,
        x.MilyemAlis,
        x.DefaultGram,
        x.DefaultLaborPrice,
        x.Category,
        x.CreatedAt,
        x.UpdatedAt);
}

public record ProductTemplateDto(
    Guid Id,
    string Name,
    decimal MilyemSatis,
    decimal MilyemAlis,
    decimal DefaultGram,
    decimal DefaultLaborPrice,
    string? Category,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ProductTemplateCreateDto(
    string Name,
    decimal MilyemSatis,
    decimal MilyemAlis,
    decimal DefaultGram,
    decimal DefaultLaborPrice,
    string? Category);
