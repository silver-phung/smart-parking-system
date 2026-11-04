using Microsoft.EntityFrameworkCore;
using SmartParking.Infrastructure.Data;

namespace SmartParking.Infrastructure.Services;

public interface IRecognitionEvidenceReader
{
    Task<RecognitionEvidence?> FindAsync(long id, CancellationToken cancellationToken);
}

public sealed record RecognitionEvidence(long StaffUserId, string? PlateNumber, string? ContentSha256);

public sealed class RecognitionEvidenceReader(AiRecognitionDbContext db) : IRecognitionEvidenceReader
{
    public Task<RecognitionEvidence?> FindAsync(long id, CancellationToken cancellationToken) =>
        db.RecognitionRecords.AsNoTracking().Where(record => record.Id == id)
            .Select(record => new RecognitionEvidence(record.StaffUserId, record.PlateNumber, record.ContentSha256))
            .FirstOrDefaultAsync(cancellationToken);
}
