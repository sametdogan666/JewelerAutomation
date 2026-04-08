namespace JewelerAutomation.Application.Interfaces;

public interface ILedgerMigrationService
{
    Task MigrateExistingDataToLedgerAsync(CancellationToken cancellationToken = default);
    Task RebuildLedgerAsync(CancellationToken cancellationToken = default);
}
