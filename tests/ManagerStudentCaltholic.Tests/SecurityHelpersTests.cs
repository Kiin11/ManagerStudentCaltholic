using System.Text;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ManagerStudentCaltholic.Tests;

public class SecurityHelpersTests
{
    private static ParishDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ParishDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ParishDbContext(options);
    }

    [Theory]
    [InlineData(42L, 42L, true)]
    [InlineData(42L, 99L, false)]
    [InlineData(null, 42L, false)]
    [InlineData(0L, 0L, false)]
    [InlineData(-1L, -1L, false)]
    public void IsSelfStudentAction_DetectsMatchingPositiveStudentIdOnly(
        long? linkedStudentId,
        long targetStudentId,
        bool expected)
    {
        Assert.Equal(expected, StaffSelfActionGuard.IsSelfStudentAction(linkedStudentId, targetStudentId));
    }

    [Fact]
    public async Task GetLinkedStudentIdAsync_ResolvesLinkedStudentIdOrLegacyStudentId()
    {
        await using var db = CreateInMemoryContext(nameof(GetLinkedStudentIdAsync_ResolvesLinkedStudentIdOrLegacyStudentId));

        db.Users.AddRange(
            new User
            {
                Id = 1,
                Username = "dubi1",
                PasswordHash = "hash",
                FirstName = "Van",
                LastName = "A",
                Role = UserRole.ProbationaryTeacher,
                LinkedStudentId = 501
            },
            new User
            {
                Id = 2,
                Username = "legacy1",
                PasswordHash = "hash",
                FirstName = "Van",
                LastName = "B",
                Role = UserRole.Teacher,
                StudentId = 502
            },
            new User
            {
                Id = 3,
                Username = "glv1",
                PasswordHash = "hash",
                FirstName = "Van",
                LastName = "C",
                Role = UserRole.Teacher
            });
        await db.SaveChangesAsync();

        Assert.Equal(501L, await StaffSelfActionGuard.GetLinkedStudentIdAsync(db, 1));
        Assert.Equal(502L, await StaffSelfActionGuard.GetLinkedStudentIdAsync(db, 2));
        Assert.Null(await StaffSelfActionGuard.GetLinkedStudentIdAsync(db, 3));
        Assert.Null(await StaffSelfActionGuard.GetLinkedStudentIdAsync(db, 0));
    }

    [Theory]
    [InlineData(".jpg", "<script>alert(1)</script>")]
    [InlineData(".jpeg", "%PDF-1.7\n")]
    [InlineData(".xlsx", "MZ\x90\x00This is not an xlsx")]
    [InlineData(".xlsx", "<html><body>spoof</body></html>")]
    [InlineData(".pdf", "<svg onload=alert(1)>")]
    public async Task ValidateStreamMagicBytesAsync_RejectsSpoofedTextPayloads(string extension, string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        await using var stream = new MemoryStream(bytes);
        stream.Position = 0;

        bool valid = await FileUploadSecurityHelper.ValidateStreamMagicBytesAsync(stream, extension);

        Assert.False(valid);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task ValidateStreamMagicBytesAsync_RejectsRawJpegBytesWithPdfExtension()
    {
        await using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 });

        bool valid = await FileUploadSecurityHelper.ValidateStreamMagicBytesAsync(stream, ".pdf");

        Assert.False(valid);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task ValidateStreamMagicBytesAsync_AcceptsValidMagicBytesAndRestoresPosition()
    {
        await using var jpg = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 });
        await using var xlsx = new MemoryStream(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 });
        await using var pdf = new MemoryStream("%PDF-1.7\n1 0 obj"u8.ToArray());

        Assert.True(await FileUploadSecurityHelper.ValidateStreamMagicBytesAsync(jpg, ".jpg"));
        Assert.True(await FileUploadSecurityHelper.ValidateStreamMagicBytesAsync(xlsx, ".xlsx"));
        Assert.True(await FileUploadSecurityHelper.ValidateStreamMagicBytesAsync(pdf, ".pdf"));
        Assert.Equal(0, jpg.Position);
        Assert.Equal(0, xlsx.Position);
        Assert.Equal(0, pdf.Position);
    }

    [Fact]
    public async Task ValidateFormFileMagicBytesAsync_RejectsSpoofedPdfPayloadWithJpgExtension()
    {
        var bytes = "%PDF-1.4\nfake"u8.ToArray();
        await using var ms = new MemoryStream(bytes);
        IFormFile spoofedJpg = new FormFile(ms, 0, bytes.Length, "file", "avatar.jpg");

        bool valid = await FileUploadSecurityHelper.ValidateFormFileMagicBytesAsync(spoofedJpg, ".jpg");

        Assert.False(valid);
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("..\\appsettings.json")]
    [InlineData("/../etc/passwd")]
    [InlineData("subdir/../../secret.txt")]
    [InlineData("..")]
    [InlineData("cert.pdf\0.jpg")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("")]
    public void TryResolveContainedPath_RejectsPathTraversalRootedAndNullByteInputs(string unsafeInput)
    {
        var root = Path.Combine(Path.GetTempPath(), "ParishTestRoot");

        bool resolved = FileUploadSecurityHelper.TryResolveContainedPath(root, unsafeInput, out var fullPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, fullPath);
    }

    [Fact]
    public void TryResolveContainedPath_LeadingSlashCannotEscapeRootDirectory()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ParishTestRoot"));
        var rootWithSep = root + Path.DirectorySeparatorChar;

        bool resolved = FileUploadSecurityHelper.TryResolveContainedPath(root, "/etc/passwd", out var fullPath);

        Assert.True(resolved);
        Assert.StartsWith(rootWithSep, fullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolveContainedPath_AcceptsSafeRelativePathInsideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ParishTestRoot");

        bool resolved = FileUploadSecurityHelper.TryResolveContainedPath(root, "cert_20261124.pdf", out var fullPath);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "cert_20261124.pdf")), fullPath);
    }
}
