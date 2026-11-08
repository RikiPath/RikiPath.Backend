using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Essay;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class LearnerEssayDraftTests
{
    private readonly Mock<IUnitOfWork> uow = new();
    private readonly Mock<ILearnerEssayRepository> essays = new();
    private readonly Mock<IClaimService> claims = new();
    private readonly Mock<IFileStorageService> storage = new();
    private readonly Mock<IGeminiOcrClient> ocr = new();

    public LearnerEssayDraftTests()
    {
        uow.SetupGet(x => x.LearnerEssays).Returns(essays.Object);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 7 });
        essays.Setup(x => x.AddAsync(It.IsAny<LearnerEssay>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LearnerEssay e, CancellationToken _) => { e.Id = 42; return e; });
        storage.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "essays", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadedFileResult("https://storage/new.png", "new.png", 3));
        storage.Setup(x => x.DeleteAsync(It.IsAny<string>(), "essays", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        ocr.Setup(x => x.ScanHandwritingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("新しい文字");
    }

    private LearnerEssayService Service() => new(uow.Object, claims.Object, storage.Object, ocr.Object);
    private static IFormFile Image(string type = "image/png") => new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "image", "essay.png") { Headers = new HeaderDictionary(), ContentType = type };
    private LearnerEssay Existing(int owner = 7)
    {
        var e = new LearnerEssay { Id = 42, UserId = owner, Title = "Old title", ContentText = "Saved corrections", OriginalOcrText = "Original", ImageUrl = "https://storage/old.png", ImageStoragePath = "old.png" };
        essays.Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(e);
        return e;
    }

    [Fact]
    public async Task Create_text_only_returns_owned_persisted_draft_with_identity()
    {
        var response = await Service().CreateAsync(null, "  日本語  ", null);
        Assert.True(response.IsSuccess);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(42, response.Result!.Id);
        Assert.Equal("日本語", response.Result.ContentText);
        Assert.Equal("日本語", response.Result.Title);
        Assert.NotNull(response.Result.ScannedAt);
        essays.Verify(x => x.AddAsync(It.Is<LearnerEssay>(e => e.UserId == 7 && e.ImageUrl == ""), It.IsAny<CancellationToken>()), Times.Once);
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_with_image_keeps_corrected_text_and_original_ocr_separately()
    {
        var result = await Service().CreateAsync(Image(), "Corrected", "OCR preview");
        Assert.True(result.IsSuccess);
        Assert.Equal("Corrected", result.Result!.ContentText);
        Assert.Equal("OCR preview", result.Result.OriginalOcrText);
        Assert.Equal("https://storage/new.png", result.Result.ImageUrl);
        ocr.VerifyNoOtherCalls(); // Explicit save does not run OCR again.
    }

    [Fact]
    public async Task Rescan_is_preview_only_and_never_changes_saved_text_or_storage()
    {
        var e = Existing();
        var result = await Service().RescanAsync(e.Id, Image());
        Assert.Equal("新しい文字", result.Result!.Text);
        Assert.Equal("Saved corrections", e.ContentText);
        Assert.Equal("Original", e.OriginalOcrText);
        Assert.Equal("old.png", e.ImageStoragePath);
        Assert.Null(e.ModifiedDate);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Explicit_save_replaces_image_after_persisting_and_keeps_identity_for_second_save()
    {
        var e = Existing();
        var result = await Service().SaveDraftAsync(e.Id, Image(), "Reviewed OCR", "Raw OCR");
        Assert.Equal(42, result.Result!.Id);
        Assert.Equal("Reviewed OCR", result.Result.ContentText);
        Assert.Equal("Raw OCR", result.Result.OriginalOcrText);
        Assert.Equal("https://storage/new.png", result.Result.ImageUrl);
        storage.Verify(x => x.DeleteAsync("old.png", "essays", It.IsAny<CancellationToken>()), Times.Once);
        var second = await Service().UpdateAsync(result.Result.Id, "Second edit");
        Assert.Equal(42, second.Result!.Id);
        Assert.Equal("https://storage/new.png", second.Result.ImageUrl);
        Assert.Equal("Raw OCR", second.Result.OriginalOcrText);
        Assert.Equal("Second edit", second.Result.Title);
    }

    [Fact]
    public async Task Other_users_cannot_preview_or_replace_someone_elses_essay()
    {
        Existing(owner: 99);
        Assert.Equal(404, (await Service().RescanAsync(42, Image())).StatusCode);
        Assert.Equal(404, (await Service().SaveDraftAsync(42, Image(), "New", "OCR")).StatusCode);
        storage.VerifyNoOtherCalls();
        ocr.VerifyNoOtherCalls();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Database_failure_cleans_new_upload_but_does_not_delete_saved_image()
    {
        Existing();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("db unavailable"));
        Assert.Equal(500, (await Service().SaveDraftAsync(42, Image(), "New", "OCR")).StatusCode);
        storage.Verify(x => x.DeleteAsync("new.png", "essays", It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(x => x.DeleteAsync("old.png", "essays", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invalid_input_never_uploads_or_persists()
    {
        Assert.Equal(400, (await Service().CreateAsync(null, " ", null)).StatusCode);
        Assert.Equal(400, (await Service().CreateAsync(Image("text/plain"), "Text", null)).StatusCode);
        storage.VerifyNoOtherCalls();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Failed_rescan_leaves_saved_essay_intact()
    {
        var e = Existing();
        ocr.Setup(x => x.ScanHandwritingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("provider unavailable"));
        Assert.False((await Service().RescanAsync(42, Image())).IsSuccess);
        Assert.Equal("Saved corrections", e.ContentText);
        storage.VerifyNoOtherCalls();
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Controller_returns_created_or_not_found_HTTP_status_instead_of_always_200()
    {
        var service = new Mock<ILearnerEssayService>();
        service.Setup(x => x.CreateAsync(null, "Text", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EssayDetailResponse>.Created(new() { Id = 42 }));
        service.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<EssayDetailResponse>.NotFound());
        var controller = new LearnerEssayController(service.Object);
        Assert.Equal(201, Assert.IsType<ObjectResult>(await controller.Create(new() { ContentText = "Text" }, default)).StatusCode);
        Assert.Equal(404, Assert.IsType<ObjectResult>(await controller.GetById(999, default)).StatusCode);
    }
}
