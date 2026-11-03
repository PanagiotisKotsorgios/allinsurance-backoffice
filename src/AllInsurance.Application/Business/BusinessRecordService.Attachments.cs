using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Application.Business;

public sealed partial class BusinessRecordService
{
    private static readonly string AttachmentRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInsurance", "RecordFiles");
    private static readonly HashSet<string> AllowedAttachmentExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".docx" };

    public async Task<IReadOnlyList<BusinessRecordAttachment>> GetAttachmentsAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.BusinessRecordAttachments.AsNoTracking()
            .Where(item => item.RecordId == id)
            .OrderByDescending(item => item.UploadedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<BusinessRecordAttachment> AddAttachmentAsync(Guid id, string sourcePath,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        var source = new FileInfo(sourcePath);
        if (!source.Exists || source.Length > 50 * 1024 * 1024 ||
            !AllowedAttachmentExtensions.Contains(source.Extension))
            throw new ArgumentException("Επιλέξτε PDF, εικόνα ή DOCX έως 50 MB.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.BusinessRecords.AnyAsync(item => item.Id == id, cancellationToken))
            throw new InvalidOperationException("Η εγγραφή δεν υπάρχει.");
        var attachmentId = Guid.NewGuid();
        var relative = Path.Combine(id.ToString("N"), attachmentId.ToString("N") +
            source.Extension.ToLowerInvariant());
        var target = Path.Combine(AttachmentRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try
        {
            await using (var input = source.OpenRead())
            await using (var output = File.Create(target))
                await input.CopyToAsync(output, cancellationToken);
            var item = new BusinessRecordAttachment
            {
                Id = attachmentId, RecordId = id, FileName = source.Name,
                RelativePath = relative, UploadedAtUtc = DateTime.UtcNow,
                UploadedBy = session.Current!.DisplayName
            };
            db.BusinessRecordAttachments.Add(item);
            AddActivity(db, id, "Επισύναψη αρχείου", $"Επισυνάφθηκε {source.Name}.",
                session.Current!.DisplayName, item.UploadedAtUtc);
            await db.SaveChangesAsync(cancellationToken);
            return item;
        }
        catch
        {
            if (File.Exists(target)) File.Delete(target);
            throw;
        }
    }

    public async Task DeleteAttachmentAsync(Guid id, Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        RequireSession();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var item = await db.BusinessRecordAttachments.SingleOrDefaultAsync(entry =>
            entry.RecordId == id && entry.Id == attachmentId, cancellationToken)
            ?? throw new InvalidOperationException("Το αρχείο δεν υπάρχει.");
        var path = GetAttachmentPath(item);
        db.BusinessRecordAttachments.Remove(item);
        AddActivity(db, id, "Αφαίρεση αρχείου", $"Αφαιρέθηκε {item.FileName}.",
            session.Current!.DisplayName, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        if (File.Exists(path)) File.Delete(path);
    }

    public string GetAttachmentPath(BusinessRecordAttachment attachment)
    {
        RequireSession();
        var root = Path.GetFullPath(AttachmentRoot) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(AttachmentRoot, attachment.RelativePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Μη έγκυρη διαδρομή αρχείου.");
        return path;
    }
}
