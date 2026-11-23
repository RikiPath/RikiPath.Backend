using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Notebook;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notebook;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RikiPath.Application.Services
{
    public class NotebookService(
         IUnitOfWork unitOfWork,
         IClaimService claimService) : INotebookService
    {
        private const string DefaultFolderName = "Sổ tay chung";
        private const string DefaultFileName = "Ghi chú nhanh";
        private const string DefaultPageTitle = "Trang ghi chú nhanh";

        private static readonly HashSet<string> AllowedItemKinds = new(StringComparer.Ordinal)
        {
            "vocabulary",
            "kanji",
            "grammar",
            "highlight",
            "note",
            "manual",
        };

        // =========================================================================
        // 1. Tree & Folders
        // =========================================================================

        public async Task<ApiResponse<NotebookTreeResponse>> GetTreeAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var folders = await unitOfWork.NotebookFolders.GetByUserIdAsync(userId, cancellationToken);
                var files = await unitOfWork.LearnerNotes.GetByUserIdAsync(userId, cancellationToken);
                var pages = await unitOfWork.NotebookPages.GetByUserIdAsync(userId, cancellationToken);

                var itemCountsByPage = await unitOfWork.LearnerNoteEntries.Query()
                    .Where(x => !x.IsDeleted &&
                                x.NotebookPageId.HasValue &&
                                x.LearnerNote.UserId == userId &&
                                !x.LearnerNote.IsDeleted)
                    .GroupBy(x => x.NotebookPageId!.Value)
                    .Select(g => new { PageId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.PageId, x => x.Count, cancellationToken);

                var unassignedCountsByFile = await unitOfWork.LearnerNoteEntries.Query()
                    .Where(x => !x.IsDeleted &&
                                !x.NotebookPageId.HasValue &&
                                x.LearnerNote.UserId == userId &&
                                !x.LearnerNote.IsDeleted)
                    .GroupBy(x => x.LearnerNoteId)
                    .Select(g => new { FileId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.FileId, x => x.Count, cancellationToken);

                var totalSavedItems = itemCountsByPage.Values.Sum() + unassignedCountsByFile.Values.Sum();
                var pagesByFile = pages.GroupBy(p => p.LearnerNoteId).ToDictionary(g => g.Key, g => g.ToList());
                var filesByFolder = files
                    .Where(f => f.FolderId.HasValue)
                    .GroupBy(f => f.FolderId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var orphanFiles = files.Where(f => !f.FolderId.HasValue).ToList();
                var folderTrees = new List<NotebookFolderTreeResponse>(folders.Count);

                foreach (var folder in folders)
                {
                    var childFiles = filesByFolder.TryGetValue(folder.Id, out var groupedFiles)
                        ? groupedFiles
                        : new List<LearnerNote>();

                    if (folder.IsDefault && orphanFiles.Count > 0)
                    {
                        childFiles = childFiles.Concat(orphanFiles).ToList();
                        orphanFiles.Clear();
                    }

                    var fileDtos = childFiles
                        .Select(file => MapFileTree(file, folder.Name, pagesByFile, itemCountsByPage, unassignedCountsByFile))
                        .ToList();

                    folderTrees.Add(new NotebookFolderTreeResponse
                    {
                        Id = folder.Id,
                        Name = folder.Name,
                        IsDefault = folder.IsDefault,
                        SortOrder = folder.SortOrder,
                        Revision = folder.Revision,
                        FileCount = fileDtos.Count,
                        PageCount = fileDtos.Sum(f => f.PageCount),
                        SavedItemCount = fileDtos.Sum(f => f.SavedItemCount),
                        CreatedDate = folder.CreatedDate ?? DateTime.UtcNow,
                        ModifiedDate = folder.ModifiedDate,
                        Files = fileDtos,
                    });
                }

                if (orphanFiles.Count > 0)
                {
                    var orphanDtos = orphanFiles
                        .Select(file => MapFileTree(file, DefaultFolderName, pagesByFile, itemCountsByPage, unassignedCountsByFile))
                        .ToList();
                    folderTrees.Add(new NotebookFolderTreeResponse
                    {
                        Id = 0,
                        Name = DefaultFolderName,
                        IsDefault = true,
                        SortOrder = -1,
                        Revision = 1,
                        FileCount = orphanDtos.Count,
                        PageCount = orphanDtos.Sum(f => f.PageCount),
                        SavedItemCount = orphanDtos.Sum(f => f.SavedItemCount),
                        CreatedDate = DateTime.UtcNow,
                        Files = orphanDtos,
                    });
                }

                var tree = new NotebookTreeResponse
                {
                    Folders = folderTrees,
                    Stats = new NotebookStatsResponse
                    {
                        TotalFolders = folders.Count,
                        TotalFiles = files.Count,
                        TotalPages = pages.Count,
                        TotalSavedItems = totalSavedItems,
                        FavoritePagesCount = pages.Count(p => p.IsFavorite),
                    },
                };

                return ApiResponse<NotebookTreeResponse>.Success(tree);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookTreeResponse>.Fail(
                    "Không thể tải cấu trúc Sổ tay cá nhân.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<List<NotebookFolderTreeResponse>>> GetFoldersAsync(CancellationToken cancellationToken)
        {
            var tree = await GetTreeAsync(cancellationToken);
            if (!tree.IsSuccess)
            {
                return ApiResponse<List<NotebookFolderTreeResponse>>.Fail(
                    tree.ErrorMessage ?? "Không thể tải danh sách thư mục.",
                    (HttpStatusCode)tree.StatusCode,
                    tree.Errors);
            }
            return ApiResponse<List<NotebookFolderTreeResponse>>.Success(tree.Result!.Folders);
        }

        public async Task<ApiResponse<NotebookFolderTreeResponse>> CreateFolderAsync(
            CreateNotebookFolderRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var cleanName = NotebookDocumentHelper.NormalizePlainText(request.Name, 120).Trim();
                if (string.IsNullOrWhiteSpace(cleanName))
                    return ApiResponse<NotebookFolderTreeResponse>.Fail("Tên thư mục không được để trống.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");

                var normalized = cleanName.ToUpperInvariant();
                NotebookFolder? created = null;
                ApiResponse<NotebookFolderTreeResponse>? failure = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);

                    var existingFolders = await unitOfWork.NotebookFolders.GetByUserIdAsync(userId, token);
                    if (existingFolders.Any(f => string.Equals(f.NormalizedName, normalized, StringComparison.Ordinal)))
                    {
                        failure = ApiResponse<NotebookFolderTreeResponse>.Fail(
                            "Tên thư mục này đã tồn tại.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_FOLDER_DUPLICATE");
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    var nextSortOrder = existingFolders.Count == 0 ? 1 : existingFolders.Max(f => f.SortOrder) + 1;
                    created = new NotebookFolder
                    {
                        UserId = userId,
                        Name = cleanName,
                        NormalizedName = normalized,
                        SortOrder = nextSortOrder,
                        IsDefault = false,
                        Revision = 1,
                        CreatedDate = utcNow,
                        ModifiedDate = utcNow,
                    };
                    await unitOfWork.NotebookFolders.AddAsync(created, token);
                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                if (failure is not null)
                    return failure;

                return ApiResponse<NotebookFolderTreeResponse>.Created(new NotebookFolderTreeResponse
                {
                    Id = created!.Id,
                    Name = created.Name,
                    IsDefault = created.IsDefault,
                    SortOrder = created.SortOrder,
                    Revision = created.Revision,
                    CreatedDate = created.CreatedDate ?? DateTime.UtcNow,
                    ModifiedDate = created.ModifiedDate,
                    Files = [],
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookFolderTreeResponse>.Fail(
                    "Không thể tạo thư mục.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookFolderTreeResponse>> UpdateFolderAsync(
            int folderId,
            UpdateNotebookFolderRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var cleanName = NotebookDocumentHelper.NormalizePlainText(request.Name, 120).Trim();
                if (string.IsNullOrWhiteSpace(cleanName))
                    return ApiResponse<NotebookFolderTreeResponse>.Fail("Tên thư mục không được để trống.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");

                var normalized = cleanName.ToUpperInvariant();
                ApiResponse<NotebookFolderTreeResponse>? outcome = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var folder = await unitOfWork.NotebookFolders.GetByIdForUpdateAsync(folderId, token);
                    if (folder is null || folder.IsDeleted)
                    {
                        outcome = ApiResponse<NotebookFolderTreeResponse>.NotFound("Không tìm thấy thư mục.");
                        return;
                    }
                    if (folder.UserId != userId)
                    {
                        outcome = ApiResponse<NotebookFolderTreeResponse>.Fail("Thư mục này không thuộc về bạn.", HttpStatusCode.Forbidden);
                        return;
                    }
                    if (request.ExpectedRevision.HasValue && request.ExpectedRevision.Value != folder.Revision)
                    {
                        outcome = ApiResponse<NotebookFolderTreeResponse>.Fail(
                            "Thư mục vừa được cập nhật ở nơi khác. Vui lòng tải lại.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_REVISION_CONFLICT");
                        return;
                    }

                    var userFolders = await unitOfWork.NotebookFolders.GetByUserIdAsync(userId, token);
                    if (userFolders.Any(f => f.Id != folder.Id && string.Equals(f.NormalizedName, normalized, StringComparison.Ordinal)))
                    {
                        outcome = ApiResponse<NotebookFolderTreeResponse>.Fail(
                            "Tên thư mục này đã tồn tại.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_FOLDER_DUPLICATE");
                        return;
                    }

                    folder.Name = cleanName;
                    folder.NormalizedName = normalized;
                    folder.Revision += 1;
                    folder.ModifiedDate = DateTime.UtcNow;
                    unitOfWork.NotebookFolders.Update(folder);
                    await unitOfWork.SaveChangesAsync(token);

                    var files = await unitOfWork.LearnerNotes.GetByFolderIdAsync(userId, folder.Id, token);
                    var pages = await unitOfWork.NotebookPages.GetByUserIdAsync(userId, token);
                    var fileIds = files.Select(f => f.Id).ToHashSet();
                    var folderPages = pages.Where(p => fileIds.Contains(p.LearnerNoteId)).ToList();

                    outcome = ApiResponse<NotebookFolderTreeResponse>.Success(new NotebookFolderTreeResponse
                    {
                        Id = folder.Id,
                        Name = folder.Name,
                        IsDefault = folder.IsDefault,
                        SortOrder = folder.SortOrder,
                        Revision = folder.Revision,
                        FileCount = files.Count,
                        PageCount = folderPages.Count,
                        CreatedDate = folder.CreatedDate ?? DateTime.UtcNow,
                        ModifiedDate = folder.ModifiedDate,
                        Files = [],
                    });
                }, cancellationToken);

                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookFolderTreeResponse>.Fail(
                    "Không thể cập nhật thư mục.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> DeleteFolderAsync(int folderId, bool cascade, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse? outcome = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var folder = await unitOfWork.NotebookFolders.GetByIdForUpdateAsync(folderId, token);
                    if (folder is null || folder.IsDeleted)
                    {
                        outcome = ApiResponse.NotFound("Không tìm thấy thư mục.");
                        return;
                    }
                    if (folder.UserId != userId)
                    {
                        outcome = ApiResponse.Fail("Thư mục này không thuộc về bạn.", HttpStatusCode.Forbidden);
                        return;
                    }

                    var files = await unitOfWork.LearnerNotes.GetByFolderIdAsync(userId, folder.Id, token);
                    if (files.Count > 0 && !cascade)
                    {
                        outcome = ApiResponse.Fail(
                            $"Thư mục đang chứa {files.Count} tệp ghi chú. Vui lòng di chuyển tệp hoặc xác nhận xóa toàn bộ thư mục.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_FOLDER_NOT_EMPTY");
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    if (files.Count > 0)
                    {
                        var fileIds = files.Select(f => f.Id).ToList();
                        await SoftDeleteFilesPagesAndEntriesAsync(fileIds, utcNow, token);
                        foreach (var file in files)
                        {
                            file.IsDeleted = true;
                            file.IsDefault = false;
                            file.Revision += 1;
                            file.ModifiedDate = utcNow;
                            unitOfWork.LearnerNotes.Update(file);
                        }
                    }

                    folder.IsDeleted = true;
                    folder.IsDefault = false;
                    folder.Revision += 1;
                    folder.ModifiedDate = utcNow;
                    unitOfWork.NotebookFolders.Update(folder);
                    await unitOfWork.SaveChangesAsync(token);

                    outcome = ApiResponse.Success();
                }, cancellationToken);

                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Không thể xóa thư mục.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        // =========================================================================
        // 2. Notebook Files (Level 2 — stored in LearnerNotes)
        // =========================================================================

        public async Task<ApiResponse<NotebookFileTreeResponse>> GetFileAsync(int fileId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var file = await unitOfWork.LearnerNotes.GetByIdAsync(fileId, cancellationToken);
                if (file is null || file.IsDeleted)
                    return ApiResponse<NotebookFileTreeResponse>.NotFound("Không tìm thấy tệp ghi chú.");
                if (file.UserId != userId)
                    return ApiResponse<NotebookFileTreeResponse>.Fail("Tệp ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden);

                string folderName = DefaultFolderName;
                if (file.FolderId.HasValue)
                {
                    var folder = await unitOfWork.NotebookFolders.GetByIdAsync(file.FolderId.Value, cancellationToken);
                    if (folder is not null && !folder.IsDeleted)
                        folderName = folder.Name;
                }

                var pages = await unitOfWork.NotebookPages.GetByFileIdAsync(userId, file.Id, cancellationToken);
                var countsByPage = await unitOfWork.LearnerNoteEntries.Query()
                    .Where(x => !x.IsDeleted && x.LearnerNoteId == file.Id && x.NotebookPageId.HasValue)
                    .GroupBy(x => x.NotebookPageId!.Value)
                    .Select(g => new { PageId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.PageId, x => x.Count, cancellationToken);

                var pageDtos = pages.Select(p => MapPageSummary(
                    p,
                    file.Name,
                    folderName,
                    countsByPage.TryGetValue(p.Id, out var c) ? c : 0)).ToList();

                return ApiResponse<NotebookFileTreeResponse>.Success(new NotebookFileTreeResponse
                {
                    Id = file.Id,
                    FolderId = file.FolderId,
                    FolderName = folderName,
                    Name = file.Name,
                    Description = file.Description,
                    IsDefault = file.IsDefault,
                    SortOrder = file.SortOrder,
                    Revision = file.Revision,
                    PageCount = pageDtos.Count,
                    SavedItemCount = pageDtos.Sum(p => p.SavedItemCount),
                    CreatedDate = file.CreatedDate ?? DateTime.UtcNow,
                    ModifiedDate = file.ModifiedDate,
                    Pages = pageDtos,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookFileTreeResponse>.Fail(
                    "Không thể tải tệp ghi chú.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookFileTreeResponse>> CreateFileAsync(
            CreateNotebookFileRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var cleanName = NotebookDocumentHelper.NormalizePlainText(request.Name, 200).Trim();
                if (string.IsNullOrWhiteSpace(cleanName))
                    return ApiResponse<NotebookFileTreeResponse>.Fail("Tên tệp ghi chú không được để trống.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");

                var cleanDesc = string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : NotebookDocumentHelper.NormalizePlainText(request.Description, 1000).Trim();

                LearnerNote? file = null;
                NotebookFolder? folder = null;
                NotebookPage? initialPage = null;
                ApiResponse<NotebookFileTreeResponse>? failure = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);

                    if (request.FolderId.HasValue && request.FolderId.Value > 0)
                    {
                        folder = await unitOfWork.NotebookFolders.GetByIdAsync(request.FolderId.Value, token);
                        if (folder is null || folder.IsDeleted)
                        {
                            failure = ApiResponse<NotebookFileTreeResponse>.NotFound("Không tìm thấy thư mục.");
                            return;
                        }
                        if (folder.UserId != userId)
                        {
                            failure = ApiResponse<NotebookFileTreeResponse>.Fail("Thư mục này không thuộc về bạn.", HttpStatusCode.Forbidden);
                            return;
                        }
                    }
                    else
                    {
                        folder = await EnsureDefaultFolderLockedAsync(userId, token);
                    }

                    var siblingFiles = await unitOfWork.LearnerNotes.GetByFolderIdAsync(userId, folder.Id, token);
                    if (siblingFiles.Any(f => string.Equals(f.Name.Trim(), cleanName, StringComparison.OrdinalIgnoreCase)))
                    {
                        failure = ApiResponse<NotebookFileTreeResponse>.Fail(
                            "Tên tệp ghi chú này đã tồn tại trong thư mục.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_FILE_DUPLICATE");
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    var nextOrder = siblingFiles.Count == 0 ? 1 : siblingFiles.Max(f => f.SortOrder) + 1;
                    file = new LearnerNote
                    {
                        UserId = userId,
                        FolderId = folder.Id,
                        Name = cleanName,
                        Description = cleanDesc,
                        SortOrder = nextOrder,
                        IsDefault = false,
                        Revision = 1,
                        CreatedDate = utcNow,
                        ModifiedDate = utcNow,
                    };
                    await unitOfWork.LearnerNotes.AddAsync(file, token);
                    await unitOfWork.SaveChangesAsync(token);

                    if (request.CreateInitialPage)
                    {
                        initialPage = new NotebookPage
                        {
                            UserId = userId,
                            LearnerNoteId = file.Id,
                            Title = cleanName,
                            ContentJson = NotebookDocumentHelper.EmptyDocumentJson,
                            ContentPlainText = string.Empty,
                            ContentFormatVersion = 1,
                            SortOrder = 1,
                            Revision = 1,
                            CreatedDate = utcNow,
                            ModifiedDate = utcNow,
                        };
                        await unitOfWork.NotebookPages.AddAsync(initialPage, token);
                        await unitOfWork.SaveChangesAsync(token);
                    }
                }, cancellationToken);

                if (failure is not null)
                    return failure;

                var pages = initialPage is not null
                    ? new List<NotebookPageSummaryResponse> { MapPageSummary(initialPage, file!.Name, folder!.Name, 0) }
                    : [];

                return ApiResponse<NotebookFileTreeResponse>.Created(new NotebookFileTreeResponse
                {
                    Id = file!.Id,
                    FolderId = folder!.Id,
                    FolderName = folder.Name,
                    Name = file.Name,
                    Description = file.Description,
                    IsDefault = file.IsDefault,
                    SortOrder = file.SortOrder,
                    Revision = file.Revision,
                    PageCount = pages.Count,
                    SavedItemCount = 0,
                    CreatedDate = file.CreatedDate ?? DateTime.UtcNow,
                    ModifiedDate = file.ModifiedDate,
                    Pages = pages,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookFileTreeResponse>.Fail(
                    "Không thể tạo tệp ghi chú.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookFileTreeResponse>> UpdateFileAsync(
            int fileId,
            UpdateNotebookFileRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse<NotebookFileTreeResponse>? outcome = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var file = await unitOfWork.LearnerNotes.GetByIdForUpdateAsync(fileId, token);
                    if (file is null || file.IsDeleted)
                    {
                        outcome = ApiResponse<NotebookFileTreeResponse>.NotFound("Không tìm thấy tệp ghi chú.");
                        return;
                    }
                    if (file.UserId != userId)
                    {
                        outcome = ApiResponse<NotebookFileTreeResponse>.Fail("Tệp ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden);
                        return;
                    }
                    if (request.ExpectedRevision.HasValue && request.ExpectedRevision.Value != file.Revision)
                    {
                        outcome = ApiResponse<NotebookFileTreeResponse>.Fail(
                            "Tệp ghi chú vừa thay đổi ở phiên khác. Vui lòng tải lại.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_REVISION_CONFLICT");
                        return;
                    }

                    var targetFolderId = request.FolderId ?? file.FolderId;
                    NotebookFolder? targetFolder = null;
                    if (targetFolderId.HasValue)
                    {
                        targetFolder = await unitOfWork.NotebookFolders.GetByIdAsync(targetFolderId.Value, token);
                        if (targetFolder is null || targetFolder.IsDeleted)
                        {
                            outcome = ApiResponse<NotebookFileTreeResponse>.NotFound("Không tìm thấy thư mục đích.");
                            return;
                        }
                        if (targetFolder.UserId != userId)
                        {
                            outcome = ApiResponse<NotebookFileTreeResponse>.Fail("Thư mục đích không thuộc về bạn.", HttpStatusCode.Forbidden);
                            return;
                        }
                    }
                    else
                    {
                        targetFolder = await EnsureDefaultFolderLockedAsync(userId, token);
                        targetFolderId = targetFolder.Id;
                    }

                    var nextName = file.Name;
                    if (request.Name is not null)
                    {
                        nextName = NotebookDocumentHelper.NormalizePlainText(request.Name, 200).Trim();
                        if (string.IsNullOrWhiteSpace(nextName))
                        {
                            outcome = ApiResponse<NotebookFileTreeResponse>.Fail("Tên tệp ghi chú không được để trống.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");
                            return;
                        }
                    }

                    var siblings = await unitOfWork.LearnerNotes.GetByFolderIdAsync(userId, targetFolderId.Value, token);
                    if (siblings.Any(f => f.Id != file.Id && string.Equals(f.Name.Trim(), nextName, StringComparison.OrdinalIgnoreCase)))
                    {
                        outcome = ApiResponse<NotebookFileTreeResponse>.Fail(
                            "Đã có tệp ghi chú cùng tên trong thư mục đích.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_FILE_DUPLICATE");
                        return;
                    }

                    file.Name = nextName;
                    file.FolderId = targetFolderId;
                    if (request.Description is not null)
                        file.Description = string.IsNullOrWhiteSpace(request.Description)
                            ? null
                            : NotebookDocumentHelper.NormalizePlainText(request.Description, 1000).Trim();
                    if (request.SortOrder.HasValue)
                        file.SortOrder = Math.Max(0, request.SortOrder.Value);

                    file.Revision += 1;
                    file.ModifiedDate = DateTime.UtcNow;
                    unitOfWork.LearnerNotes.Update(file);
                    await unitOfWork.SaveChangesAsync(token);

                    var pages = await unitOfWork.NotebookPages.GetByFileIdAsync(userId, file.Id, token);
                    outcome = ApiResponse<NotebookFileTreeResponse>.Success(new NotebookFileTreeResponse
                    {
                        Id = file.Id,
                        FolderId = file.FolderId,
                        FolderName = targetFolder.Name,
                        Name = file.Name,
                        Description = file.Description,
                        IsDefault = file.IsDefault,
                        SortOrder = file.SortOrder,
                        Revision = file.Revision,
                        PageCount = pages.Count,
                        CreatedDate = file.CreatedDate ?? DateTime.UtcNow,
                        ModifiedDate = file.ModifiedDate,
                        Pages = pages.Select(p => MapPageSummary(p, file.Name, targetFolder.Name, 0)).ToList(),
                    });
                }, cancellationToken);

                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookFileTreeResponse>.Fail(
                    "Không thể cập nhật tệp ghi chú.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> DeleteFileAsync(int fileId, bool cascade, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse? outcome = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var file = await unitOfWork.LearnerNotes.GetByIdForUpdateAsync(fileId, token);
                    if (file is null || file.IsDeleted)
                    {
                        outcome = ApiResponse.NotFound("Không tìm thấy tệp ghi chú.");
                        return;
                    }
                    if (file.UserId != userId)
                    {
                        outcome = ApiResponse.Fail("Tệp ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden);
                        return;
                    }

                    var pages = await unitOfWork.NotebookPages.GetByFileIdAsync(userId, file.Id, token);
                    if (pages.Count > 0 && !cascade)
                    {
                        outcome = ApiResponse.Fail(
                            $"Tệp ghi chú này có {pages.Count} trang. Vui lòng xác nhận để xóa toàn bộ tệp và các trang bên trong.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_FILE_NOT_EMPTY");
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    await SoftDeleteFilesPagesAndEntriesAsync([file.Id], utcNow, token);

                    file.IsDeleted = true;
                    file.IsDefault = false;
                    file.Revision += 1;
                    file.ModifiedDate = utcNow;
                    unitOfWork.LearnerNotes.Update(file);
                    await unitOfWork.SaveChangesAsync(token);

                    outcome = ApiResponse.Success();
                }, cancellationToken);

                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Không thể xóa tệp ghi chú.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        // =========================================================================
        // 3. Notebook Pages (Level 3 — Rich Text + Autosave + Favorites)
        // =========================================================================

        public async Task<ApiResponse<NotebookPageDetailResponse>> GetPageAsync(int pageId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var page = await unitOfWork.NotebookPages.GetDetailedByIdAsync(pageId, cancellationToken);
                if (page is null || page.IsDeleted || page.LearnerNote is null || page.LearnerNote.IsDeleted)
                    return ApiResponse<NotebookPageDetailResponse>.NotFound("Không tìm thấy trang ghi chú.");
                if (page.UserId != userId)
                    return ApiResponse<NotebookPageDetailResponse>.Fail("Trang ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden);

                var entries = await unitOfWork.LearnerNoteEntries.GetByPageIdAsync(userId, page.Id, cancellationToken);
                return ApiResponse<NotebookPageDetailResponse>.Success(MapPageDetail(page, entries));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookPageDetailResponse>.Fail(
                    "Không thể tải trang ghi chú.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookPageDetailResponse>> CreatePageAsync(
            CreateNotebookPageRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var cleanTitle = NotebookDocumentHelper.NormalizePlainText(request.Title, 200).Trim();
                if (string.IsNullOrWhiteSpace(cleanTitle))
                    return ApiResponse<NotebookPageDetailResponse>.Fail("Tiêu đề trang không được để trống.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");

                var parsedDoc = NotebookDocumentHelper.ParseOrNormalizeDocument(request.ContentJson);
                NotebookPage? page = null;
                LearnerNote? parentFile = null;
                ApiResponse<NotebookPageDetailResponse>? failure = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    parentFile = await unitOfWork.LearnerNotes.GetByIdForUpdateAsync(request.FileId, token);
                    if (parentFile is null || parentFile.IsDeleted)
                    {
                        failure = ApiResponse<NotebookPageDetailResponse>.NotFound("Không tìm thấy tệp ghi chú đích.");
                        return;
                    }
                    if (parentFile.UserId != userId)
                    {
                        failure = ApiResponse<NotebookPageDetailResponse>.Fail("Tệp ghi chú đích không thuộc về bạn.", HttpStatusCode.Forbidden);
                        return;
                    }

                    var siblingPages = await unitOfWork.NotebookPages.GetByFileIdAsync(userId, parentFile.Id, token);
                    if (siblingPages.Any(p => string.Equals(p.Title.Trim(), cleanTitle, StringComparison.OrdinalIgnoreCase)))
                    {
                        failure = ApiResponse<NotebookPageDetailResponse>.Fail(
                            "Tên trang này đã tồn tại trong tệp ghi chú.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_PAGE_DUPLICATE");
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    var nextOrder = siblingPages.Count == 0 ? 1 : siblingPages.Max(p => p.SortOrder) + 1;
                    page = new NotebookPage
                    {
                        UserId = userId,
                        LearnerNoteId = parentFile.Id,
                        Title = cleanTitle,
                        ContentJson = parsedDoc.ContentJson,
                        ContentPlainText = parsedDoc.ContentPlainText,
                        ContentFormatVersion = 1,
                        ChecklistTotalCount = parsedDoc.ChecklistTotalCount,
                        ChecklistCompletedCount = parsedDoc.ChecklistCompletedCount,
                        SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? null : request.SourceType.Trim(),
                        SourceId = request.SourceId,
                        SourcePath = NotebookDocumentHelper.SanitizeInternalPath(request.SourcePath),
                        SourceTitle = string.IsNullOrWhiteSpace(request.SourceTitle) ? null : NotebookDocumentHelper.NormalizePlainText(request.SourceTitle, 200).Trim(),
                        SortOrder = nextOrder,
                        IsDefault = false,
                        Revision = 1,
                        CreatedDate = utcNow,
                        ModifiedDate = utcNow,
                        LastAutosavedAtUtc = string.IsNullOrWhiteSpace(parsedDoc.ContentPlainText) ? null : utcNow,
                    };
                    await unitOfWork.NotebookPages.AddAsync(page, token);
                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                if (failure is not null)
                    return failure;

                var detailed = await unitOfWork.NotebookPages.GetDetailedByIdAsync(page!.Id, cancellationToken);
                return ApiResponse<NotebookPageDetailResponse>.Created(MapPageDetail(detailed ?? page!, []));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookPageDetailResponse>.Fail(
                    "Không thể tạo trang ghi chú.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookPageDetailResponse>> UpdatePageAsync(
            int pageId,
            UpdateNotebookPageRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse<NotebookPageDetailResponse>? outcome = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var page = await unitOfWork.NotebookPages.GetByIdForUpdateAsync(pageId, token);
                    if (page is null || page.IsDeleted)
                    {
                        outcome = ApiResponse<NotebookPageDetailResponse>.NotFound("Không tìm thấy trang ghi chú.");
                        return;
                    }
                    if (page.UserId != userId)
                    {
                        outcome = ApiResponse<NotebookPageDetailResponse>.Fail("Trang ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden);
                        return;
                    }

                    if (request.ExpectedRevision.HasValue && request.ExpectedRevision.Value != page.Revision)
                    {
                        outcome = ApiResponse<NotebookPageDetailResponse>.Fail(
                            "Trang ghi chú vừa được cập nhật ở tab hoặc thiết bị khác. Hãy tải bản mới nhất trước khi ghi đè.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_REVISION_CONFLICT");
                        return;
                    }

                    var targetFileId = request.FileId ?? page.LearnerNoteId;
                    if (targetFileId != page.LearnerNoteId)
                    {
                        var targetFile = await unitOfWork.LearnerNotes.GetByIdForUpdateAsync(targetFileId, token);
                        if (targetFile is null || targetFile.IsDeleted)
                        {
                            outcome = ApiResponse<NotebookPageDetailResponse>.NotFound("Không tìm thấy tệp ghi chú đích.");
                            return;
                        }
                        if (targetFile.UserId != userId)
                        {
                            outcome = ApiResponse<NotebookPageDetailResponse>.Fail("Tệp ghi chú đích không thuộc về bạn.", HttpStatusCode.Forbidden);
                            return;
                        }
                    }

                    var nextTitle = page.Title;
                    if (request.Title is not null)
                    {
                        nextTitle = NotebookDocumentHelper.NormalizePlainText(request.Title, 200).Trim();
                        if (string.IsNullOrWhiteSpace(nextTitle))
                        {
                            outcome = ApiResponse<NotebookPageDetailResponse>.Fail("Tiêu đề trang không được để trống.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");
                            return;
                        }
                    }

                    var siblingPages = await unitOfWork.NotebookPages.GetByFileIdAsync(userId, targetFileId, token);
                    if (siblingPages.Any(p => p.Id != page.Id && string.Equals(p.Title.Trim(), nextTitle, StringComparison.OrdinalIgnoreCase)))
                    {
                        outcome = ApiResponse<NotebookPageDetailResponse>.Fail(
                            "Tên trang này đã tồn tại trong tệp ghi chú.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_PAGE_DUPLICATE");
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    page.Title = nextTitle;

                    if (request.ContentJson is not null)
                    {
                        var parsedDoc = NotebookDocumentHelper.ParseOrNormalizeDocument(request.ContentJson);
                        page.ContentJson = parsedDoc.ContentJson;
                        page.ContentPlainText = parsedDoc.ContentPlainText;
                        page.ContentFormatVersion = 1;
                        page.ChecklistTotalCount = parsedDoc.ChecklistTotalCount;
                        page.ChecklistCompletedCount = parsedDoc.ChecklistCompletedCount;
                        page.LastAutosavedAtUtc = utcNow;
                    }

                    if (request.IsFavorite.HasValue)
                    {
                        page.IsFavorite = request.IsFavorite.Value;
                        page.FavoritedAtUtc = request.IsFavorite.Value ? utcNow : null;
                    }

                    if (request.SortOrder.HasValue)
                        page.SortOrder = Math.Max(0, request.SortOrder.Value);

                    var fileMoved = targetFileId != page.LearnerNoteId;
                    page.LearnerNoteId = targetFileId;
                    page.Revision += 1;
                    page.ModifiedDate = utcNow;
                    unitOfWork.NotebookPages.Update(page);

                    if (fileMoved)
                    {
                        var childEntries = await unitOfWork.LearnerNoteEntries.Query()
                            .Where(e => e.NotebookPageId == page.Id && !e.IsDeleted)
                            .ToListAsync(token);
                        foreach (var entry in childEntries)
                        {
                            entry.LearnerNoteId = targetFileId;
                            entry.Revision += 1;
                            entry.ModifiedDate = utcNow;
                            unitOfWork.LearnerNoteEntries.Update(entry);
                        }
                    }

                    await unitOfWork.SaveChangesAsync(token);

                    var detailed = await unitOfWork.NotebookPages.GetDetailedByIdAsync(page.Id, token);
                    var items = await unitOfWork.LearnerNoteEntries.GetByPageIdAsync(userId, page.Id, token);
                    outcome = ApiResponse<NotebookPageDetailResponse>.Success(MapPageDetail(detailed ?? page, items));
                }, cancellationToken);

                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookPageDetailResponse>.Fail(
                    "Không thể lưu trang ghi chú.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookPageSummaryResponse>> TogglePageFavoriteAsync(
            int pageId,
            bool isFavorite,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse<NotebookPageSummaryResponse>? outcome = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var page = await unitOfWork.NotebookPages.GetByIdForUpdateAsync(pageId, token);
                    if (page is null || page.IsDeleted)
                    {
                        outcome = ApiResponse<NotebookPageSummaryResponse>.NotFound("Không tìm thấy trang ghi chú.");
                        return;
                    }
                    if (page.UserId != userId)
                    {
                        outcome = ApiResponse<NotebookPageSummaryResponse>.Fail("Trang ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden);
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    page.IsFavorite = isFavorite;
                    page.FavoritedAtUtc = isFavorite ? utcNow : null;
                    page.Revision += 1;
                    page.ModifiedDate = utcNow;
                    unitOfWork.NotebookPages.Update(page);
                    await unitOfWork.SaveChangesAsync(token);

                    var detailed = await unitOfWork.NotebookPages.GetDetailedByIdAsync(page.Id, token);
                    var itemCount = await unitOfWork.LearnerNoteEntries.Query()
                        .CountAsync(x => !x.IsDeleted && x.NotebookPageId == page.Id, token);

                    outcome = ApiResponse<NotebookPageSummaryResponse>.Success(MapPageSummary(
                        detailed ?? page,
                        detailed?.LearnerNote?.Name ?? string.Empty,
                        detailed?.LearnerNote?.Folder?.Name ?? DefaultFolderName,
                        itemCount));
                }, cancellationToken);

                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookPageSummaryResponse>.Fail(
                    "Không thể cập nhật trạng thái yêu thích.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> DeletePageAsync(int pageId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse? outcome = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var page = await unitOfWork.NotebookPages.GetByIdForUpdateAsync(pageId, token);
                    if (page is null || page.IsDeleted)
                    {
                        outcome = ApiResponse.NotFound("Không tìm thấy trang ghi chú.");
                        return;
                    }
                    if (page.UserId != userId)
                    {
                        outcome = ApiResponse.Fail("Trang ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden);
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    await SoftDeleteEntriesForPagesAsync([page.Id], utcNow, token);

                    page.IsDeleted = true;
                    page.IsDefault = false;
                    page.Revision += 1;
                    page.ModifiedDate = utcNow;
                    unitOfWork.NotebookPages.Update(page);
                    await unitOfWork.SaveChangesAsync(token);

                    outcome = ApiResponse.Success();
                }, cancellationToken);

                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Không thể xóa trang ghi chú.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        // =========================================================================
        // 4. Smart Views & Server Search
        // =========================================================================

        public async Task<ApiResponse<List<NotebookPageSummaryResponse>>> GetFavoritePagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var favPages = await unitOfWork.NotebookPages.GetFavoritesByUserAsync(userId, cancellationToken);
                var pageIds = favPages.Select(p => p.Id).ToList();

                var counts = pageIds.Count == 0
                    ? new Dictionary<int, int>()
                    : await unitOfWork.LearnerNoteEntries.Query()
                        .Where(x => !x.IsDeleted && x.NotebookPageId.HasValue && pageIds.Contains(x.NotebookPageId.Value))
                        .GroupBy(x => x.NotebookPageId!.Value)
                        .Select(g => new { PageId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.PageId, x => x.Count, cancellationToken);

                var dtos = favPages.Select(p => MapPageSummary(
                    p,
                    p.LearnerNote?.Name ?? string.Empty,
                    p.LearnerNote?.Folder?.Name ?? DefaultFolderName,
                    counts.TryGetValue(p.Id, out var c) ? c : 0)).ToList();

                return ApiResponse<List<NotebookPageSummaryResponse>>.Success(dtos);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<List<NotebookPageSummaryResponse>>.Fail(
                    "Không thể tải danh sách trang yêu thích.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookSavedItemsPageResponse>> GetSavedItemsAsync(
            string? search,
            string? kind,
            int? pageId,
            int? fileId,
            int? folderId,
            bool? favoriteOnly,
            string? sortBy,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var normalizedPage = Math.Max(1, page);
                var normalizedPageSize = Math.Clamp(pageSize, 1, 60);
                var pattern = string.IsNullOrWhiteSpace(search) ? null : BuildILikePattern(search);

                var (items, totalCount) = await unitOfWork.LearnerNoteEntries.QuerySavedItemsAsync(
                    userId,
                    pattern,
                    kind,
                    pageId,
                    fileId,
                    folderId,
                    favoriteOnly,
                    sortBy,
                    normalizedPage,
                    normalizedPageSize,
                    cancellationToken);

                return ApiResponse<NotebookSavedItemsPageResponse>.Success(new NotebookSavedItemsPageResponse
                {
                    Items = items.Select(MapEntry).ToList(),
                    Page = normalizedPage,
                    PageSize = normalizedPageSize,
                    TotalCount = totalCount,
                    TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)normalizedPageSize)),
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookSavedItemsPageResponse>.Fail(
                    "Không thể tải danh sách mục đã lưu.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookSearchResponse>> SearchAsync(
            string? query,
            int limit,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var cleanQuery = NotebookDocumentHelper.NormalizePlainText(query, 120).Trim();
                var boundedLimit = Math.Clamp(limit, 1, 50);

                if (string.IsNullOrWhiteSpace(cleanQuery))
                {
                    return ApiResponse<NotebookSearchResponse>.Success(new NotebookSearchResponse { Query = string.Empty });
                }

                var pattern = BuildILikePattern(cleanQuery);

                var matchingFolders = await unitOfWork.NotebookFolders.SearchAsync(userId, pattern, boundedLimit, cancellationToken);
                var matchingFiles = await unitOfWork.LearnerNotes.SearchFilesAsync(userId, pattern, boundedLimit, cancellationToken);
                var matchingPages = await unitOfWork.NotebookPages.SearchPagesAsync(userId, pattern, boundedLimit, cancellationToken);
                var savedItemsRes = await GetSavedItemsAsync(cleanQuery, null, null, null, null, null, "recent", 1, boundedLimit, cancellationToken);

                return ApiResponse<NotebookSearchResponse>.Success(new NotebookSearchResponse
                {
                    Query = cleanQuery,
                    Folders = matchingFolders.Select(f => new NotebookFolderTreeResponse
                    {
                        Id = f.Id,
                        Name = f.Name,
                        IsDefault = f.IsDefault,
                        SortOrder = f.SortOrder,
                        Revision = f.Revision,
                        CreatedDate = f.CreatedDate ?? DateTime.UtcNow,
                        ModifiedDate = f.ModifiedDate,
                        Files = [],
                    }).ToList(),
                    Files = matchingFiles.Select(f => new NotebookFileTreeResponse
                    {
                        Id = f.Id,
                        FolderId = f.FolderId,
                        FolderName = f.Folder?.Name ?? DefaultFolderName,
                        Name = f.Name,
                        Description = f.Description,
                        IsDefault = f.IsDefault,
                        SortOrder = f.SortOrder,
                        Revision = f.Revision,
                        CreatedDate = f.CreatedDate ?? DateTime.UtcNow,
                        ModifiedDate = f.ModifiedDate,
                        Pages = [],
                    }).ToList(),
                    Pages = matchingPages.Select(p => MapPageSummary(
                        p,
                        p.LearnerNote?.Name ?? string.Empty,
                        p.LearnerNote?.Folder?.Name ?? DefaultFolderName,
                        0)).ToList(),
                    SavedItems = savedItemsRes.IsSuccess ? savedItemsRes.Result!.Items : [],
                    TotalSavedItems = savedItemsRes.IsSuccess ? savedItemsRes.Result!.TotalCount : 0,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookSearchResponse>.Fail(
                    "Không thể tìm kiếm trong Sổ tay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        // =========================================================================
        // 5. Quick Save (Smart Text Selection -> Idempotent Default Hierarchy)
        // =========================================================================

        public async Task<ApiResponse<NotebookEntryResponse>> QuickSaveAsync(
            QuickSaveNotebookRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var cleanWord = NotebookDocumentHelper.NormalizePlainText(request.Text, 200).Trim();
                if (string.IsNullOrWhiteSpace(cleanWord))
                    return ApiResponse<NotebookEntryResponse>.Fail("Vui lòng chọn đoạn văn bản hợp lệ để lưu.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");

                var cleanReading = string.IsNullOrWhiteSpace(request.Reading)
                    ? null
                    : NotebookDocumentHelper.NormalizePlainText(request.Reading, 200).Trim();
                var cleanNote = string.IsNullOrWhiteSpace(request.Note)
                    ? null
                    : NotebookDocumentHelper.NormalizePlainText(request.Note, 2000).Trim();
                var cleanMeaning = NotebookDocumentHelper.NormalizePlainText(
                    string.IsNullOrWhiteSpace(request.Meaning) ? cleanNote ?? cleanWord : request.Meaning,
                    500).Trim();
                if (string.IsNullOrWhiteSpace(cleanMeaning))
                    cleanMeaning = cleanWord;

                var kind = NormalizeItemKind(request.ItemKind, request.VocabularyId, request.KanjiId, request.GrammarPatternId);
                var cleanSourcePath = NotebookDocumentHelper.SanitizeInternalPath(request.SourcePath);
                var cleanSourceTitle = string.IsNullOrWhiteSpace(request.SourceTitle)
                    ? null
                    : NotebookDocumentHelper.NormalizePlainText(request.SourceTitle, 200).Trim();
                var cleanSnippet = string.IsNullOrWhiteSpace(request.ContextSnippet)
                    ? null
                    : NotebookDocumentHelper.NormalizePlainText(request.ContextSnippet, 600).Trim();
                var cleanEx = string.IsNullOrWhiteSpace(request.ExampleSentence)
                    ? null
                    : NotebookDocumentHelper.NormalizePlainText(request.ExampleSentence, 500).Trim();
                var cleanExMeaning = string.IsNullOrWhiteSpace(request.ExampleMeaning)
                    ? null
                    : NotebookDocumentHelper.NormalizePlainText(request.ExampleMeaning, 500).Trim();

                LearnerNoteEntry? savedEntry = null;
                ReviewCard? createdCard = null;
                bool alreadyExisting = false;
                ApiResponse<NotebookEntryResponse>? failure = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);

                    NotebookPage targetPage;
                    if (request.NotebookPageId.HasValue && request.NotebookPageId.Value > 0)
                    {
                        var requestedPage = await unitOfWork.NotebookPages.GetDetailedByIdAsync(request.NotebookPageId.Value, token);
                        if (requestedPage is null || requestedPage.IsDeleted || requestedPage.LearnerNote.IsDeleted)
                        {
                            failure = ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy trang đích.");
                            return;
                        }
                        if (requestedPage.UserId != userId)
                        {
                            failure = ApiResponse<NotebookEntryResponse>.Fail("Trang đích không thuộc về bạn.", HttpStatusCode.Forbidden);
                            return;
                        }
                        targetPage = requestedPage;
                    }
                    else
                    {
                        targetPage = await EnsureDefaultPageLockedAsync(userId, token);
                    }

                    // Validate linked dictionary entities if provided (never trust arbitrary IDs).
                    if (request.VocabularyId.HasValue && await unitOfWork.Vocabularies.GetPublicByIdAsync(request.VocabularyId.Value, token) is null)
                    {
                        failure = ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy từ vựng liên kết.");
                        return;
                    }
                    if (request.KanjiId.HasValue && await unitOfWork.Kanjis.GetPublicByIdAsync(request.KanjiId.Value, token) is null)
                    {
                        failure = ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy Kanji liên kết.");
                        return;
                    }
                    if (request.GrammarPatternId.HasValue && await unitOfWork.GrammarPatterns.GetPublicByIdAsync(request.GrammarPatternId.Value, token) is null)
                    {
                        failure = ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy điểm ngữ pháp liên kết.");
                        return;
                    }

                    var pageEntries = await unitOfWork.LearnerNoteEntries.GetByPageIdAsync(userId, targetPage.Id, token);
                    var existing = FindMatchingEntryOnPage(
                        pageEntries,
                        request.VocabularyId,
                        request.KanjiId,
                        request.GrammarPatternId,
                        kind,
                        cleanWord);

                    if (existing is not null)
                    {
                        savedEntry = existing;
                        alreadyExisting = true;
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    var nextOrder = pageEntries.Count == 0 ? 1 : pageEntries.Max(e => e.SortOrder) + 1;
                    savedEntry = new LearnerNoteEntry
                    {
                        LearnerNoteId = targetPage.LearnerNoteId,
                        NotebookPageId = targetPage.Id,
                        VocabularyId = request.VocabularyId,
                        KanjiId = request.KanjiId,
                        GrammarPatternId = request.GrammarPatternId,
                        ManualWord = request.VocabularyId.HasValue || request.KanjiId.HasValue || request.GrammarPatternId.HasValue ? null : cleanWord,
                        ManualReading = request.VocabularyId.HasValue || request.KanjiId.HasValue || request.GrammarPatternId.HasValue ? null : cleanReading,
                        ManualMeaning = request.VocabularyId.HasValue || request.KanjiId.HasValue || request.GrammarPatternId.HasValue ? null : cleanMeaning,
                        Note = cleanNote,
                        ItemKind = kind,
                        SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Selection" : request.SourceType.Trim(),
                        SourceId = request.SourceId,
                        SourcePath = cleanSourcePath,
                        SourceTitle = cleanSourceTitle,
                        ContextSnippet = cleanSnippet,
                        ExampleSentence = cleanEx,
                        ExampleMeaning = cleanExMeaning,
                        TagsJson = "[]",
                        SortOrder = nextOrder,
                        Revision = 1,
                        CreatedDate = utcNow,
                        ModifiedDate = utcNow,
                    };

                    if (request.EnrollSrs)
                    {
                        createdCard = await AddEntryWithReviewCardAsync(userId, savedEntry, token);
                    }
                    else
                    {
                        await unitOfWork.LearnerNoteEntries.AddAsync(savedEntry, token);
                        await unitOfWork.SaveChangesAsync(token);
                    }
                }, cancellationToken);

                if (failure is not null)
                    return failure;

                var hydrated = await unitOfWork.LearnerNoteEntries.GetDetailedByIdAsync(savedEntry!.Id, cancellationToken);
                var dto = MapEntry(hydrated ?? savedEntry);
                dto.IsAlreadyExisting = alreadyExisting;
                if (createdCard is not null)
                    dto.ReviewCardId = createdCard.Id;

                return alreadyExisting
                    ? ApiResponse<NotebookEntryResponse>.Success(dto)
                    : ApiResponse<NotebookEntryResponse>.Created(dto);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể lưu nhanh vào Sổ tay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        // =========================================================================
        // 6. Legacy / Shared File & Saved Item CRUD + Explicit SRS Boundary
        // =========================================================================

        public async Task<ApiResponse<LearnerNoteResponse>> CreateListAsync(CreateLearnerNoteRequest request, CancellationToken cancellationToken)
        {
            var res = await CreateFileAsync(new CreateNotebookFileRequest
            {
                FolderId = request.FolderId,
                Name = request.Name,
                Description = request.Description,
                CreateInitialPage = true,
            }, cancellationToken);

            if (!res.IsSuccess)
            {
                return ApiResponse<LearnerNoteResponse>.Fail(
                    res.ErrorMessage ?? "Không thể tạo tệp ghi chú.",
                    (HttpStatusCode)res.StatusCode,
                    res.ErrorCode,
                    errors: res.Errors);
            }

            var file = res.Result!;
            return ApiResponse<LearnerNoteResponse>.Created(new LearnerNoteResponse
            {
                Id = file.Id,
                Name = file.Name,
                Description = file.Description,
                FolderId = file.FolderId,
                FolderName = file.FolderName,
                IsDefault = file.IsDefault,
                SortOrder = file.SortOrder,
                Revision = file.Revision,
                PageCount = file.PageCount,
                EntryCount = file.SavedItemCount,
                CreatedAt = file.CreatedDate,
                ModifiedDate = file.ModifiedDate,
            });
        }

        public async Task<ApiResponse<List<LearnerNoteResponse>>> GetMyListsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var files = await unitOfWork.LearnerNotes.GetByUserIdAsync(userId, cancellationToken);
                var entryCounts = await unitOfWork.LearnerNoteEntries.Query()
                    .Where(x => !x.IsDeleted && x.LearnerNote.UserId == userId && !x.LearnerNote.IsDeleted)
                    .GroupBy(x => x.LearnerNoteId)
                    .Select(g => new { FileId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.FileId, x => x.Count, cancellationToken);

                var pageCounts = await unitOfWork.NotebookPages.Query()
                    .Where(x => x.UserId == userId && !x.IsDeleted && !x.LearnerNote.IsDeleted)
                    .GroupBy(x => x.LearnerNoteId)
                    .Select(g => new { FileId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.FileId, x => x.Count, cancellationToken);

                var result = files.Select(f => MapList(
                    f,
                    entryCounts.TryGetValue(f.Id, out var ec) ? ec : 0,
                    pageCounts.TryGetValue(f.Id, out var pc) ? pc : 0)).ToList();

                return ApiResponse<List<LearnerNoteResponse>>.Success(result);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<List<LearnerNoteResponse>>.Fail(
                    "Không thể tải danh sách sổ tay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<LearnerNoteResponse>> UpdateListAsync(
            int listId,
            UpdateLearnerNoteRequest request,
            CancellationToken cancellationToken)
        {
            var res = await UpdateFileAsync(listId, new UpdateNotebookFileRequest
            {
                Name = request.Name,
                Description = request.Description,
                FolderId = request.FolderId,
                ExpectedRevision = request.ExpectedRevision,
            }, cancellationToken);

            if (!res.IsSuccess)
            {
                return ApiResponse<LearnerNoteResponse>.Fail(
                    res.ErrorMessage ?? "Không thể cập nhật tệp ghi chú.",
                    (HttpStatusCode)res.StatusCode,
                    res.ErrorCode,
                    errors: res.Errors);
            }

            var file = res.Result!;
            var entriesCount = await unitOfWork.LearnerNoteEntries.Query()
                .CountAsync(x => !x.IsDeleted && x.LearnerNoteId == file.Id, cancellationToken);

            return ApiResponse<LearnerNoteResponse>.Success(new LearnerNoteResponse
            {
                Id = file.Id,
                Name = file.Name,
                Description = file.Description,
                FolderId = file.FolderId,
                FolderName = file.FolderName,
                IsDefault = file.IsDefault,
                SortOrder = file.SortOrder,
                Revision = file.Revision,
                PageCount = file.PageCount,
                EntryCount = entriesCount,
                CreatedAt = file.CreatedDate,
                ModifiedDate = file.ModifiedDate,
            });
        }

        public Task<ApiResponse> DeleteListAsync(int listId, CancellationToken cancellationToken)
            => DeleteFileAsync(listId, cascade: true, cancellationToken);

        public async Task<ApiResponse<List<NotebookEntryResponse>>> GetListEntriesAsync(int listId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var list = await unitOfWork.LearnerNotes.GetByIdAsync(listId, cancellationToken);
                if (list is null || list.IsDeleted)
                    return ApiResponse<List<NotebookEntryResponse>>.NotFound("Không tìm thấy tệp ghi chú.");
                if (list.UserId != userId)
                    return ApiResponse<List<NotebookEntryResponse>>.Fail("Tệp ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden);

                var entries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, list.Id, cancellationToken);
                return ApiResponse<List<NotebookEntryResponse>>.Success(entries.Select(MapEntry).ToList());
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<List<NotebookEntryResponse>>.Fail(
                    "Không thể tải nội dung tệp ghi chú.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> BookmarkVocabularyAsync(BookmarkVocabularyRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var vocab = await unitOfWork.Vocabularies.GetPublicByIdAsync(request.VocabularyId, cancellationToken);
                if (vocab is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy từ vựng trong ngân hàng chung.");

                LearnerNoteEntry? savedEntry = null;
                ReviewCard? reviewCard = null;
                bool existingMatched = false;
                ApiResponse<NotebookEntryResponse>? failure = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);

                    var (file, page, err) = await ResolveTargetFileAndPageLockedAsync(userId, request.LearnerNoteId, request.NotebookPageId, token);
                    if (err is not null)
                    {
                        failure = ApiResponse<NotebookEntryResponse>.Fail(err.ErrorMessage!, (HttpStatusCode)err.StatusCode, err.ErrorCode);
                        return;
                    }

                    var pageEntries = await unitOfWork.LearnerNoteEntries.GetByPageIdAsync(userId, page!.Id, token);
                    var existing = pageEntries.FirstOrDefault(e => e.VocabularyId == vocab.Id && !e.IsDeleted);
                    if (existing is not null)
                    {
                        savedEntry = existing;
                        existingMatched = true;
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    savedEntry = new LearnerNoteEntry
                    {
                        LearnerNoteId = file!.Id,
                        NotebookPageId = page.Id,
                        VocabularyId = vocab.Id,
                        ItemKind = "vocabulary",
                        SourceType = "Vocabulary",
                        SourceId = vocab.Id,
                        TagsJson = "[]",
                        Revision = 1,
                        CreatedDate = utcNow,
                        ModifiedDate = utcNow,
                    };

                    if (request.EnrollSrs)
                        reviewCard = await AddEntryWithReviewCardAsync(userId, savedEntry, token);
                    else
                    {
                        await unitOfWork.LearnerNoteEntries.AddAsync(savedEntry, token);
                        await unitOfWork.SaveChangesAsync(token);
                    }
                }, cancellationToken);

                if (failure is not null)
                    return failure;

                var hydrated = await unitOfWork.LearnerNoteEntries.GetDetailedByIdAsync(savedEntry!.Id, cancellationToken);
                var dto = MapEntry(hydrated ?? savedEntry);
                dto.IsAlreadyExisting = existingMatched;
                if (reviewCard is not null)
                    dto.ReviewCardId = reviewCard.Id;

                return existingMatched
                    ? ApiResponse<NotebookEntryResponse>.Success(dto)
                    : ApiResponse<NotebookEntryResponse>.Created(dto);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể lưu từ vựng vào Sổ tay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> BookmarkKanjiAsync(BookmarkKanjiRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var kanji = await unitOfWork.Kanjis.GetPublicByIdAsync(request.KanjiId, cancellationToken);
                if (kanji is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy Kanji trong ngân hàng chung.");

                LearnerNoteEntry? savedEntry = null;
                ReviewCard? reviewCard = null;
                bool existingMatched = false;
                ApiResponse<NotebookEntryResponse>? failure = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);

                    var (file, page, err) = await ResolveTargetFileAndPageLockedAsync(userId, request.LearnerNoteId, request.NotebookPageId, token);
                    if (err is not null)
                    {
                        failure = ApiResponse<NotebookEntryResponse>.Fail(err.ErrorMessage!, (HttpStatusCode)err.StatusCode, err.ErrorCode);
                        return;
                    }

                    var pageEntries = await unitOfWork.LearnerNoteEntries.GetByPageIdAsync(userId, page!.Id, token);
                    var existing = pageEntries.FirstOrDefault(e => e.KanjiId == kanji.Id && !e.IsDeleted);
                    if (existing is not null)
                    {
                        savedEntry = existing;
                        existingMatched = true;
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    savedEntry = new LearnerNoteEntry
                    {
                        LearnerNoteId = file!.Id,
                        NotebookPageId = page.Id,
                        KanjiId = kanji.Id,
                        ItemKind = "kanji",
                        SourceType = "Kanji",
                        SourceId = kanji.Id,
                        TagsJson = "[]",
                        Revision = 1,
                        CreatedDate = utcNow,
                        ModifiedDate = utcNow,
                    };

                    if (request.EnrollSrs)
                        reviewCard = await AddEntryWithReviewCardAsync(userId, savedEntry, token);
                    else
                    {
                        await unitOfWork.LearnerNoteEntries.AddAsync(savedEntry, token);
                        await unitOfWork.SaveChangesAsync(token);
                    }
                }, cancellationToken);

                if (failure is not null)
                    return failure;

                var hydrated = await unitOfWork.LearnerNoteEntries.GetDetailedByIdAsync(savedEntry!.Id, cancellationToken);
                var dto = MapEntry(hydrated ?? savedEntry);
                dto.IsAlreadyExisting = existingMatched;
                if (reviewCard is not null)
                    dto.ReviewCardId = reviewCard.Id;

                return existingMatched
                    ? ApiResponse<NotebookEntryResponse>.Success(dto)
                    : ApiResponse<NotebookEntryResponse>.Created(dto);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể lưu Kanji vào Sổ tay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<GrammarBookmarkResponse>> BookmarkGrammarAsync(int grammarPointId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var grammar = await unitOfWork.GrammarPatterns.GetPublicByIdAsync(grammarPointId, cancellationToken);
                if (grammar is null)
                    return ApiResponse<GrammarBookmarkResponse>.NotFound("Không tìm thấy điểm ngữ pháp.");

                var existing = await unitOfWork.ReviewCards.FindExistingCardAsync(
                    userId,
                    null,
                    null,
                    grammar.Id,
                    null,
                    null,
                    $"grammar:{grammar.Id}",
                    ReviewMode.Recognition,
                    cancellationToken);

                var utcNow = DateTime.UtcNow;
                ReviewCard reviewItem;
                if (existing is not null)
                {
                    existing.SourceLinksJson = SrsSchedulingHelper.MergeSourceLinkJson(
                        existing.SourceLinksJson, "Grammar", grammar.Id, grammar.Version, grammar.Title, null, utcNow);
                    if (existing.IsDeleted || existing.Status != SrsCardStatus.Active)
                    {
                        existing.IsDeleted = false;
                        existing.Status = SrsCardStatus.Active;
                        existing.SuspendedAt = null;
                        existing.ArchivedAt = null;
                        if (existing.NextReviewDate < utcNow.Date)
                            existing.NextReviewDate = utcNow.Date;
                    }
                    existing.ModifiedDate = utcNow;
                    unitOfWork.ReviewCards.Update(existing);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    reviewItem = existing;
                }
                else
                {
                    var meaning = !string.IsNullOrWhiteSpace(grammar.UsageNotes) ? grammar.UsageNotes : grammar.Structure;
                    reviewItem = new ReviewCard
                    {
                        UserId = userId,
                        GrammarPatternId = grammar.Id,
                        CardType = "Grammar",
                        SourceType = "Grammar",
                        SourceId = grammar.Id,
                        SourceVersion = grammar.Version,
                        SourceLabel = grammar.Title,
                        NormalizedContentKey = $"grammar:{grammar.Id}",
                        SourceLinksJson = SrsSchedulingHelper.MergeSourceLinkJson(
                            null, "Grammar", grammar.Id, grammar.Version, grammar.Title, null, utcNow),
                        FrontText = grammar.Title,
                        Reading = grammar.Structure,
                        Meaning = meaning,
                        BackText = meaning,
                        ExampleSentence = grammar.ExampleSentence,
                        ExampleMeaning = grammar.ExampleSentenceMeaning,
                        Repetitions = 0,
                        EaseFactor = SrsSchedulingHelper.DefaultEaseFactor,
                        IntervalDays = 0,
                        NextReviewDate = utcNow.Date,
                        CreatedDate = utcNow,
                    };
                    await unitOfWork.ReviewCards.AddAsync(reviewItem, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return ApiResponse<GrammarBookmarkResponse>.Created(new GrammarBookmarkResponse
                {
                    ReviewCardId = reviewItem.Id,
                    GrammarPatternId = grammar.Id,
                    GrammarTitle = grammar.Title,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<GrammarBookmarkResponse>.Fail(
                    "Không thể thêm ngữ pháp vào SRS.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> AddManualEntryAsync(AddManualEntryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var cleanWord = NotebookDocumentHelper.NormalizePlainText(request.Word, 200).Trim();
                var cleanMeaning = NotebookDocumentHelper.NormalizePlainText(request.Meaning, 500).Trim();
                if (string.IsNullOrWhiteSpace(cleanWord) || string.IsNullOrWhiteSpace(cleanMeaning))
                    return ApiResponse<NotebookEntryResponse>.Fail("Từ và nghĩa không được để trống.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");

                var cleanReading = string.IsNullOrWhiteSpace(request.Reading)
                    ? null
                    : NotebookDocumentHelper.NormalizePlainText(request.Reading, 200).Trim();
                var cleanNote = string.IsNullOrWhiteSpace(request.Note)
                    ? null
                    : NotebookDocumentHelper.NormalizePlainText(request.Note, 2000).Trim();
                var cleanKind = NormalizeItemKind(request.ItemKind, null, null, request.GrammarPatternId);
                var cleanColor = NotebookDocumentHelper.SanitizeHighlightColor(request.HighlightColor);
                var cleanEx = string.IsNullOrWhiteSpace(request.ExampleSentence) ? null : NotebookDocumentHelper.NormalizePlainText(request.ExampleSentence, 500).Trim();
                var cleanExMeaning = string.IsNullOrWhiteSpace(request.ExampleMeaning) ? null : NotebookDocumentHelper.NormalizePlainText(request.ExampleMeaning, 500).Trim();
                var cleanTagsJson = NotebookDocumentHelper.SerializeTags(request.Tags);

                LearnerNoteEntry? savedEntry = null;
                ReviewCard? reviewCard = null;
                bool existingMatched = false;
                ApiResponse<NotebookEntryResponse>? failure = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);

                    var (file, page, err) = await ResolveTargetFileAndPageLockedAsync(userId, request.LearnerNoteId, request.NotebookPageId, token);
                    if (err is not null)
                    {
                        failure = ApiResponse<NotebookEntryResponse>.Fail(err.ErrorMessage!, (HttpStatusCode)err.StatusCode, err.ErrorCode);
                        return;
                    }

                    if (request.GrammarPatternId.HasValue && await unitOfWork.GrammarPatterns.GetPublicByIdAsync(request.GrammarPatternId.Value, token) is null)
                    {
                        failure = ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy điểm ngữ pháp.");
                        return;
                    }

                    var pageEntries = await unitOfWork.LearnerNoteEntries.GetByPageIdAsync(userId, page!.Id, token);
                    var existing = FindMatchingEntryOnPage(pageEntries, null, null, request.GrammarPatternId, cleanKind, cleanWord);
                    if (existing is not null)
                    {
                        savedEntry = existing;
                        existingMatched = true;
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    var nextOrder = pageEntries.Count == 0 ? 1 : pageEntries.Max(e => e.SortOrder) + 1;
                    savedEntry = new LearnerNoteEntry
                    {
                        LearnerNoteId = file!.Id,
                        NotebookPageId = page.Id,
                        GrammarPatternId = request.GrammarPatternId,
                        ManualWord = cleanWord,
                        ManualReading = cleanReading,
                        ManualMeaning = cleanMeaning,
                        Note = cleanNote,
                        ItemKind = cleanKind,
                        HighlightColor = cleanColor,
                        SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? null : request.SourceType.Trim(),
                        SourceId = request.SourceId,
                        SourcePath = NotebookDocumentHelper.SanitizeInternalPath(request.SourcePath),
                        SourceTitle = string.IsNullOrWhiteSpace(request.SourceTitle) ? null : NotebookDocumentHelper.NormalizePlainText(request.SourceTitle, 200).Trim(),
                        ContextSnippet = string.IsNullOrWhiteSpace(request.ContextSnippet) ? null : NotebookDocumentHelper.NormalizePlainText(request.ContextSnippet, 600).Trim(),
                        ExampleSentence = cleanEx,
                        ExampleMeaning = cleanExMeaning,
                        TagsJson = cleanTagsJson,
                        SortOrder = nextOrder,
                        Revision = 1,
                        CreatedDate = utcNow,
                        ModifiedDate = utcNow,
                    };

                    if (request.EnrollSrs)
                        reviewCard = await AddEntryWithReviewCardAsync(userId, savedEntry, token);
                    else
                    {
                        await unitOfWork.LearnerNoteEntries.AddAsync(savedEntry, token);
                        await unitOfWork.SaveChangesAsync(token);
                    }
                }, cancellationToken);

                if (failure is not null)
                    return failure;

                var hydrated = await unitOfWork.LearnerNoteEntries.GetDetailedByIdAsync(savedEntry!.Id, cancellationToken);
                var dto = MapEntry(hydrated ?? savedEntry);
                dto.IsAlreadyExisting = existingMatched;
                if (reviewCard is not null)
                    dto.ReviewCardId = reviewCard.Id;

                return existingMatched
                    ? ApiResponse<NotebookEntryResponse>.Success(dto)
                    : ApiResponse<NotebookEntryResponse>.Created(dto);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể thêm mục đã lưu vào Sổ tay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> UpdateEntryAsync(
            int noteEntryId,
            UpdateNotebookEntryRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse<NotebookEntryResponse>? outcome = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var note = await unitOfWork.LearnerNoteEntries.GetByIdForUpdateAsync(noteEntryId, token);
                    if (note is null || note.IsDeleted)
                    {
                        outcome = ApiResponse<NotebookEntryResponse>.NotFound($"Không tìm thấy mục sổ tay Id = {noteEntryId}.");
                        return;
                    }

                    var list = await unitOfWork.LearnerNotes.GetByIdAsync(note.LearnerNoteId, token);
                    if (list is null || list.IsDeleted || list.UserId != userId)
                    {
                        outcome = ApiResponse<NotebookEntryResponse>.Fail("Bạn không có quyền sửa mục này.", HttpStatusCode.Forbidden);
                        return;
                    }

                    if (request.ExpectedRevision.HasValue && request.ExpectedRevision.Value != note.Revision)
                    {
                        outcome = ApiResponse<NotebookEntryResponse>.Fail(
                            "Mục này vừa được cập nhật ở nơi khác. Vui lòng tải lại.",
                            HttpStatusCode.Conflict,
                            errorCode: "NOTEBOOK_REVISION_CONFLICT");
                        return;
                    }

                    if (request.NotebookPageId.HasValue && request.NotebookPageId.Value != note.NotebookPageId)
                    {
                        var targetPage = await unitOfWork.NotebookPages.GetDetailedByIdAsync(request.NotebookPageId.Value, token);
                        if (targetPage is null || targetPage.IsDeleted || targetPage.LearnerNote.IsDeleted)
                        {
                            outcome = ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy trang đích.");
                            return;
                        }
                        if (targetPage.UserId != userId)
                        {
                            outcome = ApiResponse<NotebookEntryResponse>.Fail("Trang đích không thuộc về bạn.", HttpStatusCode.Forbidden);
                            return;
                        }
                        note.NotebookPageId = targetPage.Id;
                        note.LearnerNoteId = targetPage.LearnerNoteId;
                    }
                    else if (request.LearnerNoteId.HasValue && request.LearnerNoteId.Value != note.LearnerNoteId)
                    {
                        var (targetFile, targetPage, err) = await ResolveTargetFileAndPageLockedAsync(userId, request.LearnerNoteId.Value, null, token);
                        if (err is not null)
                        {
                            outcome = ApiResponse<NotebookEntryResponse>.Fail(err.ErrorMessage!, (HttpStatusCode)err.StatusCode, err.ErrorCode);
                            return;
                        }
                        note.LearnerNoteId = targetFile!.Id;
                        note.NotebookPageId = targetPage!.Id;
                    }

                    if (note.VocabularyId is null && note.KanjiId is null)
                    {
                        if (request.Word is not null)
                        {
                            var trimmedWord = NotebookDocumentHelper.NormalizePlainText(request.Word, 200).Trim();
                            if (string.IsNullOrWhiteSpace(trimmedWord))
                            {
                                outcome = ApiResponse<NotebookEntryResponse>.Fail("Tiêu đề mục không được để trống.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");
                                return;
                            }
                            note.ManualWord = trimmedWord;
                        }
                        if (request.Reading is not null)
                            note.ManualReading = string.IsNullOrWhiteSpace(request.Reading)
                                ? null
                                : NotebookDocumentHelper.NormalizePlainText(request.Reading, 200).Trim();
                        if (request.Meaning is not null)
                        {
                            var trimmedMeaning = NotebookDocumentHelper.NormalizePlainText(request.Meaning, 500).Trim();
                            if (string.IsNullOrWhiteSpace(trimmedMeaning))
                            {
                                outcome = ApiResponse<NotebookEntryResponse>.Fail("Nghĩa mục không được để trống.", HttpStatusCode.BadRequest, errorCode: "NOTEBOOK_INVALID_INPUT");
                                return;
                            }
                            note.ManualMeaning = trimmedMeaning;
                        }
                    }

                    if (request.Note is not null)
                        note.Note = string.IsNullOrWhiteSpace(request.Note)
                            ? null
                            : NotebookDocumentHelper.NormalizePlainText(request.Note, 2000).Trim();

                    if (request.ItemKind is not null && note.VocabularyId is null && note.KanjiId is null && note.GrammarPatternId is null)
                        note.ItemKind = NormalizeItemKind(request.ItemKind, null, null, null);

                    if (request.HighlightColor is not null)
                        note.HighlightColor = NotebookDocumentHelper.SanitizeHighlightColor(request.HighlightColor);

                    if (request.ExampleSentence is not null)
                        note.ExampleSentence = string.IsNullOrWhiteSpace(request.ExampleSentence)
                            ? null
                            : NotebookDocumentHelper.NormalizePlainText(request.ExampleSentence, 500).Trim();

                    if (request.ExampleMeaning is not null)
                        note.ExampleMeaning = string.IsNullOrWhiteSpace(request.ExampleMeaning)
                            ? null
                            : NotebookDocumentHelper.NormalizePlainText(request.ExampleMeaning, 500).Trim();

                    if (request.Tags is not null)
                        note.TagsJson = NotebookDocumentHelper.SerializeTags(request.Tags);

                    if (request.IsFavorite.HasValue)
                        note.IsFavorite = request.IsFavorite.Value;

                    note.Revision += 1;
                    note.ModifiedDate = DateTime.UtcNow;
                    unitOfWork.LearnerNoteEntries.Update(note);
                    await unitOfWork.SaveChangesAsync(token);

                    var detailed = await unitOfWork.LearnerNoteEntries.GetDetailedByIdAsync(note.Id, token);
                    outcome = ApiResponse<NotebookEntryResponse>.Success(MapEntry(detailed ?? note));
                }, cancellationToken);

                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể cập nhật mục sổ tay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> EnrollEntryToSrsAsync(
            int noteEntryId,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var note = await unitOfWork.LearnerNoteEntries.GetDetailedByIdAsync(noteEntryId, cancellationToken);
                if (note is null || note.IsDeleted)
                    return ApiResponse<NotebookEntryResponse>.NotFound($"Không tìm thấy mục sổ tay Id = {noteEntryId}.");

                if (note.LearnerNote is null || note.LearnerNote.IsDeleted || note.LearnerNote.UserId != userId)
                    return ApiResponse<NotebookEntryResponse>.Fail("Bạn không có quyền thao tác mục này.", HttpStatusCode.Forbidden);

                var existingCard = await unitOfWork.ReviewCards.GetByNoteEntryIdAsync(note.Id);
                if (existingCard is not null && !existingCard.IsDeleted && existingCard.Status == SrsCardStatus.Active)
                {
                    var existingResp = MapEntry(note);
                    existingResp.ReviewCardId = existingCard.Id;
                    existingResp.IsAlreadyExisting = true;
                    return ApiResponse<NotebookEntryResponse>.Success(existingResp);
                }

                ReviewCard? enrolledCard = null;
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    enrolledCard = await EnrollIntoReviewQueueAsync(userId, note, token);
                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                var response = MapEntry(note);
                response.ReviewCardId = enrolledCard!.Id;
                return ApiResponse<NotebookEntryResponse>.Created(response);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể thêm mục vào hàng chờ ôn tập SRS.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> RemoveEntryAsync(int noteEntryId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                ApiResponse? outcome = null;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var note = await unitOfWork.LearnerNoteEntries.GetByIdForUpdateAsync(noteEntryId, token);
                    if (note is null || note.IsDeleted)
                    {
                        outcome = ApiResponse.NotFound($"Không tìm thấy mục sổ tay Id = {noteEntryId}.");
                        return;
                    }

                    var list = await unitOfWork.LearnerNotes.GetByIdAsync(note.LearnerNoteId, token);
                    if (list is null || list.IsDeleted || list.UserId != userId)
                    {
                        outcome = ApiResponse.Fail("Bạn không có quyền xóa mục này.", HttpStatusCode.Forbidden);
                        return;
                    }

                    var utcNow = DateTime.UtcNow;
                    await DetachReviewCardsFromEntriesAsync([note.Id], utcNow, token);

                    note.IsDeleted = true;
                    note.Revision += 1;
                    note.ModifiedDate = utcNow;
                    unitOfWork.LearnerNoteEntries.Update(note);
                    await unitOfWork.SaveChangesAsync(token);

                    outcome = ApiResponse.Success();
                }, cancellationToken);

                return outcome!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Không thể xóa mục sổ tay.",
                    HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        // =========================================================================
        // Internal Helpers (Locks, Default Hierarchy, SRS Detach, Mapping)
        // =========================================================================

        private async Task<NotebookFolder> EnsureDefaultFolderLockedAsync(int userId, CancellationToken cancellationToken)
        {
            var existing = await unitOfWork.NotebookFolders.GetDefaultByUserIdAsync(userId, cancellationToken);
            if (existing is not null)
                return existing;

            var utcNow = DateTime.UtcNow;
            var folder = new NotebookFolder
            {
                UserId = userId,
                Name = DefaultFolderName,
                NormalizedName = DefaultFolderName.ToUpperInvariant(),
                SortOrder = 0,
                IsDefault = true,
                Revision = 1,
                CreatedDate = utcNow,
                ModifiedDate = utcNow,
            };
            await unitOfWork.NotebookFolders.AddAsync(folder, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return folder;
        }

        private async Task<NotebookPage> EnsureDefaultPageLockedAsync(int userId, CancellationToken cancellationToken)
        {
            var existingPage = await unitOfWork.NotebookPages.GetDefaultByUserIdAsync(userId, cancellationToken);
            if (existingPage is not null)
                return existingPage;

            var folder = await EnsureDefaultFolderLockedAsync(userId, cancellationToken);
            var existingFile = await unitOfWork.LearnerNotes.GetDefaultByUserIdAsync(userId, cancellationToken);
            var utcNow = DateTime.UtcNow;

            if (existingFile is null)
            {
                existingFile = new LearnerNote
                {
                    UserId = userId,
                    FolderId = folder.Id,
                    Name = DefaultFileName,
                    Description = null,
                    SortOrder = 0,
                    IsDefault = true,
                    Revision = 1,
                    CreatedDate = utcNow,
                    ModifiedDate = utcNow,
                };
                await unitOfWork.LearnerNotes.AddAsync(existingFile, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else if (!existingFile.FolderId.HasValue)
            {
                existingFile.FolderId = folder.Id;
                existingFile.ModifiedDate = utcNow;
                unitOfWork.LearnerNotes.Update(existingFile);
            }

            var page = new NotebookPage
            {
                UserId = userId,
                LearnerNoteId = existingFile.Id,
                Title = DefaultPageTitle,
                ContentJson = NotebookDocumentHelper.EmptyDocumentJson,
                ContentPlainText = string.Empty,
                ContentFormatVersion = 1,
                SortOrder = 0,
                IsDefault = true,
                Revision = 1,
                CreatedDate = utcNow,
                ModifiedDate = utcNow,
            };
            await unitOfWork.NotebookPages.AddAsync(page, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return page;
        }

        private async Task<(LearnerNote? File, NotebookPage? Page, ApiResponse? Error)> ResolveTargetFileAndPageLockedAsync(
            int userId,
            int requestedFileId,
            int? requestedPageId,
            CancellationToken cancellationToken)
        {
            if (requestedPageId.HasValue && requestedPageId.Value > 0)
            {
                var targetPage = await unitOfWork.NotebookPages.GetDetailedByIdAsync(requestedPageId.Value, cancellationToken);
                if (targetPage is null || targetPage.IsDeleted || targetPage.LearnerNote is null || targetPage.LearnerNote.IsDeleted)
                    return (null, null, ApiResponse.NotFound("Không tìm thấy trang ghi chú đích."));
                if (targetPage.UserId != userId)
                    return (null, null, ApiResponse.Fail("Trang ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden));
                return (targetPage.LearnerNote, targetPage, null);
            }

            if (requestedFileId > 0)
            {
                var file = await unitOfWork.LearnerNotes.GetByIdForUpdateAsync(requestedFileId, cancellationToken);
                if (file is null || file.IsDeleted)
                    return (null, null, ApiResponse.NotFound("Không tìm thấy tệp ghi chú."));
                if (file.UserId != userId)
                    return (null, null, ApiResponse.Fail("Tệp ghi chú này không thuộc về bạn.", HttpStatusCode.Forbidden));

                var filePages = await unitOfWork.NotebookPages.GetByFileIdAsync(userId, file.Id, cancellationToken);
                var page = filePages.FirstOrDefault();
                if (page is null)
                {
                    var utcNow = DateTime.UtcNow;
                    page = new NotebookPage
                    {
                        UserId = userId,
                        LearnerNoteId = file.Id,
                        Title = file.Name,
                        ContentJson = NotebookDocumentHelper.EmptyDocumentJson,
                        ContentPlainText = string.Empty,
                        ContentFormatVersion = 1,
                        SortOrder = 1,
                        Revision = 1,
                        CreatedDate = utcNow,
                        ModifiedDate = utcNow,
                    };
                    await unitOfWork.NotebookPages.AddAsync(page, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return (file, page, null);
            }

            var defaultPage = await EnsureDefaultPageLockedAsync(userId, cancellationToken);
            var defaultFile = await unitOfWork.LearnerNotes.GetByIdAsync(defaultPage.LearnerNoteId, cancellationToken);
            return (defaultFile, defaultPage, null);
        }

        private async Task SoftDeleteFilesPagesAndEntriesAsync(
            List<int> fileIds,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            if (fileIds.Count == 0)
                return;

            var pages = await unitOfWork.NotebookPages.Query()
                .Where(p => fileIds.Contains(p.LearnerNoteId) && !p.IsDeleted)
                .ToListAsync(cancellationToken);
            var entries = await unitOfWork.LearnerNoteEntries.Query()
                .Where(e => fileIds.Contains(e.LearnerNoteId) && !e.IsDeleted)
                .ToListAsync(cancellationToken);

            var entryIds = entries.Select(e => e.Id).ToList();
            await DetachReviewCardsFromEntriesAsync(entryIds, utcNow, cancellationToken);

            foreach (var entry in entries)
            {
                entry.IsDeleted = true;
                entry.Revision += 1;
                entry.ModifiedDate = utcNow;
                unitOfWork.LearnerNoteEntries.Update(entry);
            }

            foreach (var page in pages)
            {
                page.IsDeleted = true;
                page.IsDefault = false;
                page.Revision += 1;
                page.ModifiedDate = utcNow;
                unitOfWork.NotebookPages.Update(page);
            }
        }

        private async Task SoftDeleteEntriesForPagesAsync(
            List<int> pageIds,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            if (pageIds.Count == 0)
                return;

            var entries = await unitOfWork.LearnerNoteEntries.Query()
                .Where(e => e.NotebookPageId.HasValue && pageIds.Contains(e.NotebookPageId.Value) && !e.IsDeleted)
                .ToListAsync(cancellationToken);

            var entryIds = entries.Select(e => e.Id).ToList();
            await DetachReviewCardsFromEntriesAsync(entryIds, utcNow, cancellationToken);

            foreach (var entry in entries)
            {
                entry.IsDeleted = true;
                entry.Revision += 1;
                entry.ModifiedDate = utcNow;
                unitOfWork.LearnerNoteEntries.Update(entry);
            }
        }

        /// <summary>
        /// V3 SRS Boundary: removing or soft-deleting Notebook items NEVER deletes or archives existing ReviewCards.
        /// Any linked SRS card stays Active with its review history and scheduling preserved cleanly.
        /// </summary>
        private async Task DetachReviewCardsFromEntriesAsync(
            List<int> noteEntryIds,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            if (noteEntryIds.Count == 0)
                return;

            var linkedCards = await unitOfWork.ReviewCards.Query()
                .Where(c => c.LearnerNoteEntryId.HasValue && noteEntryIds.Contains(c.LearnerNoteEntryId.Value) && !c.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var card in linkedCards)
            {
                card.LearnerNoteEntryId = null;
                card.ModifiedDate = utcNow;
                unitOfWork.ReviewCards.Update(card);
            }
        }

        private static LearnerNoteEntry? FindMatchingEntryOnPage(
            List<LearnerNoteEntry> pageEntries,
            int? vocabId,
            int? kanjiId,
            int? grammarId,
            string kind,
            string cleanTerm)
        {
            if (vocabId.HasValue)
                return pageEntries.FirstOrDefault(e => e.VocabularyId == vocabId.Value && !e.IsDeleted);
            if (kanjiId.HasValue)
                return pageEntries.FirstOrDefault(e => e.KanjiId == kanjiId.Value && !e.IsDeleted);
            if (grammarId.HasValue)
                return pageEntries.FirstOrDefault(e => e.GrammarPatternId == grammarId.Value && !e.IsDeleted);

            return pageEntries.FirstOrDefault(e =>
                !e.IsDeleted &&
                e.VocabularyId is null &&
                e.KanjiId is null &&
                e.GrammarPatternId is null &&
                string.Equals(e.ItemKind, kind, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((e.ManualWord ?? string.Empty).Trim(), cleanTerm, StringComparison.Ordinal));
        }

        private async Task<ReviewCard> AddEntryWithReviewCardAsync(
            int userId,
            LearnerNoteEntry noteEntry,
            CancellationToken cancellationToken)
        {
            await unitOfWork.LearnerNoteEntries.AddAsync(noteEntry, cancellationToken);
            var reviewItem = await EnrollIntoReviewQueueAsync(userId, noteEntry, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return reviewItem;
        }

        private async Task<ReviewCard> EnrollIntoReviewQueueAsync(
            int userId,
            LearnerNoteEntry noteEntry,
            CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            var cardType = noteEntry.KanjiId.HasValue ? "Kanji"
                : noteEntry.VocabularyId.HasValue ? "Vocabulary"
                : noteEntry.GrammarPatternId.HasValue ? "Grammar"
                : "Custom";

            string front = noteEntry.ManualWord ?? string.Empty;
            string? reading = noteEntry.ManualReading;
            string meaning = noteEntry.ManualMeaning ?? noteEntry.Note ?? string.Empty;
            string? example = noteEntry.ExampleSentence;
            string? exampleMeaning = noteEntry.ExampleMeaning;
            string? audioUrl = null;
            int sourceVersion = 1;

            if (noteEntry.VocabularyId.HasValue)
            {
                var vocab = await unitOfWork.Vocabularies.GetByIdAsync(noteEntry.VocabularyId.Value, cancellationToken);
                if (vocab is not null)
                {
                    front = vocab.Word;
                    reading = vocab.Reading;
                    meaning = vocab.Meaning;
                    example ??= vocab.ExampleSentence;
                    exampleMeaning ??= vocab.ExampleSentenceMeaning;
                    audioUrl = vocab.AudioUrl;
                    sourceVersion = vocab.Version;
                }
            }
            else if (noteEntry.KanjiId.HasValue)
            {
                var kanji = await unitOfWork.Kanjis.GetByIdAsync(noteEntry.KanjiId.Value, cancellationToken);
                if (kanji is not null)
                {
                    front = kanji.Character;
                    reading = kanji.OnYomi ?? kanji.KunYomi ?? kanji.SinoVietnamese;
                    meaning = kanji.Meaning;
                    audioUrl = kanji.AudioUrl;
                    sourceVersion = kanji.Version;
                }
            }
            else if (noteEntry.GrammarPatternId.HasValue)
            {
                var grammar = await unitOfWork.GrammarPatterns.GetByIdAsync(noteEntry.GrammarPatternId.Value, cancellationToken);
                if (grammar is not null)
                {
                    front = grammar.Title;
                    reading = grammar.Structure;
                    meaning = !string.IsNullOrWhiteSpace(grammar.UsageNotes) ? grammar.UsageNotes : grammar.Structure;
                    example ??= grammar.ExampleSentence;
                    exampleMeaning ??= grammar.ExampleSentenceMeaning;
                    sourceVersion = grammar.Version;
                }
            }

            var keyEntryId = noteEntry.VocabularyId is null && noteEntry.KanjiId is null && noteEntry.GrammarPatternId is null && noteEntry.Id > 0
                ? (int?)noteEntry.Id
                : null;

            var normKey = SrsSchedulingHelper.BuildNormalizedContentKey(
                cardType,
                noteEntry.VocabularyId,
                noteEntry.KanjiId,
                noteEntry.GrammarPatternId,
                keyEntryId,
                null,
                front,
                meaning);

            var existing = await unitOfWork.ReviewCards.FindExistingCardAsync(
                userId,
                noteEntry.VocabularyId,
                noteEntry.KanjiId,
                noteEntry.GrammarPatternId,
                keyEntryId,
                null,
                normKey,
                ReviewMode.Recognition,
                cancellationToken);

            if (existing is not null)
            {
                existing.LearnerNoteEntry ??= noteEntry;
                existing.SourceLinksJson = SrsSchedulingHelper.MergeSourceLinkJson(
                    existing.SourceLinksJson,
                    "Notebook",
                    noteEntry.LearnerNoteId,
                    sourceVersion,
                    front,
                    null,
                    utcNow);
                if (existing.IsDeleted || existing.Status != SrsCardStatus.Active)
                {
                    existing.IsDeleted = false;
                    existing.Status = SrsCardStatus.Active;
                    existing.SuspendedAt = null;
                    existing.ArchivedAt = null;
                    if (existing.NextReviewDate < utcNow.Date)
                        existing.NextReviewDate = utcNow.Date;
                }
                if (string.IsNullOrWhiteSpace(existing.PersonalNote) && !string.IsNullOrWhiteSpace(noteEntry.Note))
                    existing.PersonalNote = noteEntry.Note;
                existing.ModifiedDate = utcNow;
                unitOfWork.ReviewCards.Update(existing);
                return existing;
            }

            var reviewItem = new ReviewCard
            {
                UserId = userId,
                LearnerNoteEntry = noteEntry,
                VocabularyId = noteEntry.VocabularyId,
                KanjiId = noteEntry.KanjiId,
                GrammarPatternId = noteEntry.GrammarPatternId,
                CardType = cardType,
                SourceType = "Notebook",
                SourceId = noteEntry.LearnerNoteId,
                SourceVersion = sourceVersion,
                SourceLabel = "Sổ tay cá nhân",
                NormalizedContentKey = normKey,
                SourceLinksJson = SrsSchedulingHelper.MergeSourceLinkJson(
                    null, "Notebook", noteEntry.LearnerNoteId, sourceVersion, front, null, utcNow),
                FrontText = front,
                BackText = meaning,
                Reading = reading,
                Meaning = meaning,
                ExampleSentence = example,
                ExampleMeaning = exampleMeaning,
                ContextSnippet = noteEntry.ContextSnippet,
                PersonalNote = noteEntry.Note,
                AudioUrl = audioUrl,
                Repetitions = 0,
                EaseFactor = SrsSchedulingHelper.DefaultEaseFactor,
                IntervalDays = 0,
                NextReviewDate = utcNow.Date,
                CreatedDate = utcNow,
            };
            await unitOfWork.ReviewCards.AddAsync(reviewItem, cancellationToken);
            return reviewItem;
        }

        private static string NormalizeItemKind(string? requestedKind, int? vocabId, int? kanjiId, int? grammarId)
        {
            if (vocabId.HasValue) return "vocabulary";
            if (kanjiId.HasValue) return "kanji";
            if (grammarId.HasValue) return "grammar";

            var clean = string.IsNullOrWhiteSpace(requestedKind) ? "manual" : requestedKind.Trim().ToLowerInvariant();
            return AllowedItemKinds.Contains(clean) ? clean : "manual";
        }

        private static string BuildILikePattern(string raw)
        {
            var escaped = raw.Trim()
                .Replace(@"\", @"\\", StringComparison.Ordinal)
                .Replace("%", @"\%", StringComparison.Ordinal)
                .Replace("_", @"\_", StringComparison.Ordinal);
            return $"%{escaped}%";
        }

        private static NotebookFileTreeResponse MapFileTree(
            LearnerNote file,
            string folderName,
            Dictionary<int, List<NotebookPage>> pagesByFile,
            Dictionary<int, int> countsByPage,
            Dictionary<int, int> unassignedCountsByFile)
        {
            var pages = pagesByFile.TryGetValue(file.Id, out var list) ? list : [];
            var pageDtos = pages.Select(p => MapPageSummary(
                p,
                file.Name,
                folderName,
                countsByPage.TryGetValue(p.Id, out var c) ? c : 0)).ToList();

            var unassigned = unassignedCountsByFile.TryGetValue(file.Id, out var u) ? u : 0;

            return new NotebookFileTreeResponse
            {
                Id = file.Id,
                FolderId = file.FolderId,
                FolderName = folderName,
                Name = file.Name,
                Description = file.Description,
                IsDefault = file.IsDefault,
                SortOrder = file.SortOrder,
                Revision = file.Revision,
                PageCount = pageDtos.Count,
                SavedItemCount = pageDtos.Sum(p => p.SavedItemCount) + unassigned,
                CreatedDate = file.CreatedDate ?? DateTime.UtcNow,
                ModifiedDate = file.ModifiedDate,
                Pages = pageDtos,
            };
        }

        private static NotebookPageSummaryResponse MapPageSummary(
            NotebookPage page,
            string fileName,
            string folderName,
            int savedItemCount) => new()
        {
            Id = page.Id,
            FileId = page.LearnerNoteId,
            FileName = fileName,
            FolderId = page.LearnerNote?.FolderId,
            FolderName = folderName,
            Title = page.Title,
            Excerpt = NotebookDocumentHelper.BuildExcerpt(page.ContentPlainText),
            ChecklistTotalCount = page.ChecklistTotalCount,
            ChecklistCompletedCount = page.ChecklistCompletedCount,
            IsFavorite = page.IsFavorite,
            FavoritedAtUtc = page.FavoritedAtUtc,
            IsDefault = page.IsDefault,
            SavedItemCount = savedItemCount,
            SortOrder = page.SortOrder,
            Revision = page.Revision,
            CreatedDate = page.CreatedDate ?? DateTime.UtcNow,
            ModifiedDate = page.ModifiedDate,
            LastAutosavedAtUtc = page.LastAutosavedAtUtc,
        };

        private static NotebookPageDetailResponse MapPageDetail(
            NotebookPage page,
            List<LearnerNoteEntry> entries) => new()
        {
            Id = page.Id,
            FileId = page.LearnerNoteId,
            FileName = page.LearnerNote?.Name ?? string.Empty,
            FolderId = page.LearnerNote?.FolderId,
            FolderName = page.LearnerNote?.Folder?.Name ?? DefaultFolderName,
            Title = page.Title,
            ContentJson = string.IsNullOrWhiteSpace(page.ContentJson) ? NotebookDocumentHelper.EmptyDocumentJson : page.ContentJson,
            ContentPlainText = page.ContentPlainText ?? string.Empty,
            ContentFormatVersion = page.ContentFormatVersion,
            ChecklistTotalCount = page.ChecklistTotalCount,
            ChecklistCompletedCount = page.ChecklistCompletedCount,
            IsFavorite = page.IsFavorite,
            FavoritedAtUtc = page.FavoritedAtUtc,
            IsDefault = page.IsDefault,
            SourceType = page.SourceType,
            SourceId = page.SourceId,
            SourcePath = page.SourcePath,
            SourceTitle = page.SourceTitle,
            SortOrder = page.SortOrder,
            Revision = page.Revision,
            CreatedDate = page.CreatedDate ?? DateTime.UtcNow,
            ModifiedDate = page.ModifiedDate,
            LastAutosavedAtUtc = page.LastAutosavedAtUtc,
            SavedItems = entries.Select(MapEntry).ToList(),
        };

        private static LearnerNoteResponse MapList(LearnerNote list, int entryCount, int pageCount) => new()
        {
            Id = list.Id,
            Name = list.Name,
            Description = list.Description,
            FolderId = list.FolderId,
            FolderName = list.Folder?.Name ?? DefaultFolderName,
            IsDefault = list.IsDefault,
            SortOrder = list.SortOrder,
            Revision = list.Revision,
            PageCount = pageCount,
            EntryCount = entryCount,
            CreatedAt = list.CreatedDate ?? DateTime.UtcNow,
            ModifiedDate = list.ModifiedDate,
        };

        private static NotebookEntryResponse MapEntry(LearnerNoteEntry note)
        {
            var isFromVocabBank = note.Vocabulary is not null;
            var isFromKanjiBank = note.Kanji is not null;
            var isFromGrammarBank = note.GrammarPattern is not null;

            return new NotebookEntryResponse
            {
                NoteEntryId = note.Id,
                LearnerNoteId = note.LearnerNoteId,
                FileName = note.LearnerNote?.Name ?? string.Empty,
                NotebookPageId = note.NotebookPageId,
                NotebookPageTitle = note.NotebookPage?.Title,
                FolderId = note.LearnerNote?.FolderId,
                FolderName = note.LearnerNote?.Folder?.Name ?? DefaultFolderName,
                ReviewCardId = note.ReviewCards?
                    .Where(card => !card.IsDeleted)
                    .OrderBy(card => card.Id)
                    .Select(card => (int?)card.Id)
                    .FirstOrDefault(),
                VocabularyId = note.VocabularyId,
                KanjiId = note.KanjiId,
                GrammarPatternId = note.GrammarPatternId,
                ContentType = isFromVocabBank ? "vocabulary"
                            : isFromKanjiBank ? "kanji"
                            : isFromGrammarBank ? "grammar"
                            : "manual",
                ItemKind = !string.IsNullOrWhiteSpace(note.ItemKind) ? note.ItemKind
                         : isFromVocabBank ? "vocabulary"
                         : isFromKanjiBank ? "kanji"
                         : isFromGrammarBank ? "grammar"
                         : "manual",
                Term = isFromVocabBank ? note.Vocabulary!.Word
                     : isFromKanjiBank ? note.Kanji!.Character
                     : isFromGrammarBank ? note.GrammarPattern!.Title
                     : note.ManualWord ?? string.Empty,
                Reading = isFromVocabBank ? note.Vocabulary!.Reading
                        : isFromKanjiBank ? (note.Kanji!.OnYomi ?? note.Kanji!.KunYomi ?? note.Kanji!.SinoVietnamese)
                        : isFromGrammarBank ? note.GrammarPattern!.Structure
                        : note.ManualReading,
                Meaning = isFromVocabBank ? note.Vocabulary!.Meaning
                        : isFromKanjiBank ? note.Kanji!.Meaning
                        : isFromGrammarBank ? (note.GrammarPattern!.UsageNotes ?? note.GrammarPattern!.Structure)
                        : note.ManualMeaning ?? string.Empty,
                Note = note.Note,
                HighlightColor = note.HighlightColor,
                SourceType = note.SourceType,
                SourceId = note.SourceId,
                SourcePath = note.SourcePath,
                SourceTitle = note.SourceTitle,
                ContextSnippet = note.ContextSnippet,
                ExampleSentence = note.ExampleSentence ?? note.Vocabulary?.ExampleSentence ?? note.GrammarPattern?.ExampleSentence,
                ExampleMeaning = note.ExampleMeaning ?? note.Vocabulary?.ExampleSentenceMeaning ?? note.GrammarPattern?.ExampleSentenceMeaning,
                Tags = NotebookDocumentHelper.DeserializeTags(note.TagsJson),
                IsFavorite = note.IsFavorite,
                Revision = note.Revision,
                AddedAt = note.CreatedDate ?? DateTime.UtcNow,
                ModifiedDate = note.ModifiedDate,
            };
        }

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}
