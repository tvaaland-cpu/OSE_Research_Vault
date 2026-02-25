using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using OseResearchVault.Core.Interfaces;
using OseResearchVault.Core.Models;
using OseResearchVault.Data.Services;

namespace OseResearchVault.Tests;

public sealed class DocumentImportServiceTests
{
    [Fact]
    public async Task ImportFilesAsync_ImportsSupportedAndExtractsText()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ose-research-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsService = new TestAppSettingsService(tempRoot);
            var initializer = new SqliteDatabaseInitializer(settingsService, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var inputDirectory = Path.Combine(tempRoot, "inputs");
            Directory.CreateDirectory(inputDirectory);

            var txtPath = Path.Combine(inputDirectory, "alpha.txt");
            await File.WriteAllTextAsync(txtPath, "alpha text");

            var htmlPath = Path.Combine(inputDirectory, "beta.html");
            await File.WriteAllTextAsync(htmlPath, "<html><body><h1>hello</h1><p>world</p></body></html>");

            var docxPath = Path.Combine(inputDirectory, "gamma.docx");
            CreateDocx(docxPath, "docx content");

            var pngPath = Path.Combine(inputDirectory, "delta.png");
            await File.WriteAllBytesAsync(pngPath, [0x89, 0x50, 0x4E, 0x47]);

            var unsupportedPath = Path.Combine(inputDirectory, "epsilon.exe");
            await File.WriteAllBytesAsync(unsupportedPath, [0x4D, 0x5A]);

            var service = new SqliteDocumentImportService(settingsService, NullLogger<SqliteDocumentImportService>.Instance);
            var results = await service.ImportFilesAsync([txtPath, htmlPath, docxPath, pngPath, unsupportedPath]);

            Assert.Equal(5, results.Count);
            Assert.Equal(4, results.Count(static r => r.Succeeded));
            Assert.Single(results.Where(static r => !r.Succeeded));

            var documents = await service.GetDocumentsAsync();
            Assert.Equal(4, documents.Count);

            var textDoc = documents.Single(static d => d.Title == "alpha");
            var htmlDoc = documents.Single(static d => d.Title == "beta");
            var docxDoc = documents.Single(static d => d.Title == "gamma");
            var pngDoc = documents.Single(static d => d.Title == "delta");

            var textDetails = await service.GetDocumentDetailsAsync(textDoc.Id);
            var htmlDetails = await service.GetDocumentDetailsAsync(htmlDoc.Id);
            var docxDetails = await service.GetDocumentDetailsAsync(docxDoc.Id);
            var pngDetails = await service.GetDocumentDetailsAsync(pngDoc.Id);

            Assert.Contains("alpha text", textDetails!.ExtractedText);
            Assert.Contains("hello", htmlDetails!.ExtractedText);
            Assert.Contains("docx content", docxDetails!.ExtractedText);
            Assert.True(string.IsNullOrWhiteSpace(pngDetails!.ExtractedText));

            var vaultFiles = Directory.GetFiles(Path.Combine(tempRoot, "vault", "documents"));
            Assert.Equal(4, vaultFiles.Length);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void CreateDocx(string path, string text)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("word/document.xml");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:body></w:document>");
    }

    private sealed class TestAppSettingsService(string rootDirectory) : IAppSettingsService
    {
        private readonly AppSettings _settings = new()
        {
            DatabaseDirectory = Path.Combine(rootDirectory, "db"),
            VaultStorageDirectory = Path.Combine(rootDirectory, "vault")
        };

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(_settings.DatabaseDirectory);
            Directory.CreateDirectory(_settings.VaultStorageDirectory);
            return Task.FromResult(_settings);
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
