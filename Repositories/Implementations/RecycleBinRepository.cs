using System.Data;
using Microsoft.Extensions.Logging;
using Npgsql;
using Repositories.Constants.Users;
using Repositories.Constants.Workspace.Files;
using Repositories.Constants.Workspace.Folders;
using Repositories.Constants.Workspace.Groups;
using Repositories.Constants.Workspace.Permissions;
using Repositories.Helpers;
using Repositories.Interfaces;
using Repositories.Models.ViewModels.RecycleBin;

namespace Repositories.Implementations
{
    public class RecycleBinRepository : IRecycleBinInterface
    {
        private const string RootTypeGroup = "group";

        private const string RootTypeFolder = "folder";

        private const string RootTypeFile = "file";

        private readonly NpgsqlConnection _conn;

        private readonly ILogger<RecycleBinRepository> _logger;

        public RecycleBinRepository(
            NpgsqlConnection conn,
            ILogger<RecycleBinRepository> logger)
        {
            _conn = conn;
            _logger = logger;
        }

        public async Task<List<RecycleBinItemVM>> GetRecycleBinItemsAsync()
        {
            try
            {
                await _conn.OpenAsync();
                await EnsureDeleteRootColumnsAsync();

                string query = BuildRecycleItemsQuery($@"
                    g.{GroupColumns.IsDeleted}=TRUE",
                    $@"
                    fol.{GroupFolderColumns.IsDeleted}=TRUE
                    AND
                    (
                        (
                            fol.{GroupFolderColumns.DeletedRootType}=@RootTypeFolder
                            AND fol.{GroupFolderColumns.DeletedRootId}=fol.{GroupFolderColumns.FolderId}
                        )
                        OR
                        (
                            fol.{GroupFolderColumns.DeletedRootType} IS NULL
                            AND NOT EXISTS
                            (
                                SELECT 1
                                FROM {GroupFolderTable.TableName} parent_folder
                                WHERE
                                    parent_folder.{GroupFolderColumns.FolderId}=fol.{GroupFolderColumns.ParentFolderId}
                                    AND parent_folder.{GroupFolderColumns.IsDeleted}=TRUE
                            )
                        )
                    )",
                    $@"
                    file.{GroupFileColumns.IsDeleted}=TRUE
                    AND
                    (
                        (
                            file.{GroupFileColumns.DeletedRootType}=@RootTypeFile
                            AND file.{GroupFileColumns.DeletedRootId}=file.{GroupFileColumns.FileId}
                        )
                        OR
                        (
                            file.{GroupFileColumns.DeletedRootType} IS NULL
                            AND NOT EXISTS
                            (
                                SELECT 1
                                FROM {GroupFolderTable.TableName} parent_folder
                                WHERE
                                    parent_folder.{GroupFolderColumns.FolderId}=file.{GroupFileColumns.FolderId}
                                    AND parent_folder.{GroupFolderColumns.IsDeleted}=TRUE
                            )
                        )
                    )");

                await using var cmd = new NpgsqlCommand(query, _conn);
                AddRootTypeParameters(cmd);

                return await ReadRecycleItemsAsync(cmd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recycle bin root items.");
                throw;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task<List<RecycleBinItemVM>> GetDeletedChildrenAsync(
            string parentType,
            long parentId)
        {
            try
            {
                await _conn.OpenAsync();
                await EnsureDeleteRootColumnsAsync();

                string query;

                if (parentType == RootTypeGroup)
                {
                    query = BuildRecycleItemsQuery(
                        "FALSE",
                        $@"
                        fol.{GroupFolderColumns.IsDeleted}=TRUE
                        AND fol.{GroupFolderColumns.ParentFolderId} IS NULL
                        AND fol.{GroupFolderColumns.DeletedRootType}=@RootTypeGroup
                        AND fol.{GroupFolderColumns.DeletedRootId}=@ParentId",
                        "FALSE");
                }
                else if (parentType == RootTypeFolder)
                {
                    query = BuildRecycleItemsQuery(
                        "FALSE",
                        $@"
                        fol.{GroupFolderColumns.IsDeleted}=TRUE
                        AND fol.{GroupFolderColumns.ParentFolderId}=@ParentId
                        AND EXISTS
                        (
                            SELECT 1
                            FROM {GroupFolderTable.TableName} parent_folder
                            WHERE
                                parent_folder.{GroupFolderColumns.FolderId}=@ParentId
                                AND parent_folder.{GroupFolderColumns.DeletedRootType}=fol.{GroupFolderColumns.DeletedRootType}
                                AND parent_folder.{GroupFolderColumns.DeletedRootId}=fol.{GroupFolderColumns.DeletedRootId}
                        )",
                        $@"
                        file.{GroupFileColumns.IsDeleted}=TRUE
                        AND file.{GroupFileColumns.FolderId}=@ParentId
                        AND EXISTS
                        (
                            SELECT 1
                            FROM {GroupFolderTable.TableName} parent_folder
                            WHERE
                                parent_folder.{GroupFolderColumns.FolderId}=@ParentId
                                AND
                                (
                                    (
                                        parent_folder.{GroupFolderColumns.DeletedRootType}=file.{GroupFileColumns.DeletedRootType}
                                        AND parent_folder.{GroupFolderColumns.DeletedRootId}=file.{GroupFileColumns.DeletedRootId}
                                    )
                                    OR file.{GroupFileColumns.DeletedRootType} IS NULL
                                    OR parent_folder.{GroupFolderColumns.DeletedRootType} IS NULL
                                )
                        )");
                }
                else
                {
                    return new List<RecycleBinItemVM>();
                }

                await using var cmd = new NpgsqlCommand(query, _conn);
                AddRootTypeParameters(cmd);
                cmd.Parameters.AddWithValue("@ParentId", parentId);

                return await ReadRecycleItemsAsync(cmd);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving deleted children for {ParentType} {ParentId}.",
                    parentType,
                    parentId);
                throw;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task DeleteGroupAsync(long groupId, long deletedBy)
        {
            await ExecuteInTransactionAsync(async transaction =>
            {
                await EnsureDeleteRootColumnsAsync(transaction);

                await ExecuteNonQueryAsync($@"
                    UPDATE {GroupTable.TableName}
                    SET
                        {GroupColumns.IsDeleted}=TRUE,
                        {GroupColumns.DeletedBy}=@DeletedBy,
                        {GroupColumns.DeletedAt}=CURRENT_TIMESTAMP
                    WHERE {GroupColumns.GroupId}=@GroupId;",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@GroupId", groupId);
                        cmd.Parameters.AddWithValue("@DeletedBy", deletedBy);
                    });

                await ExecuteNonQueryAsync($@"
                    WITH RECURSIVE FolderTree AS
                    (
                        SELECT {GroupFolderColumns.FolderId}
                        FROM {GroupFolderTable.TableName}
                        WHERE {GroupFolderColumns.GroupId}=@GroupId

                        UNION

                        SELECT child.{GroupFolderColumns.FolderId}
                        FROM {GroupFolderTable.TableName} child
                        INNER JOIN FolderTree parent
                            ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    )
                    UPDATE {GroupFolderTable.TableName} folder
                    SET
                        {GroupFolderColumns.IsDeleted}=TRUE,
                        {GroupFolderColumns.DeletedBy}=@DeletedBy,
                        {GroupFolderColumns.DeletedAt}=CURRENT_TIMESTAMP,
                        {GroupFolderColumns.DeletedRootType}=@RootTypeGroup,
                        {GroupFolderColumns.DeletedRootId}=@GroupId
                    WHERE
                        folder.{GroupFolderColumns.IsDeleted}=FALSE
                        AND folder.{GroupFolderColumns.FolderId} IN
                        (
                            SELECT {GroupFolderColumns.FolderId}
                            FROM FolderTree
                        );",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@GroupId", groupId);
                        cmd.Parameters.AddWithValue("@DeletedBy", deletedBy);
                        cmd.Parameters.AddWithValue("@RootTypeGroup", RootTypeGroup);
                    });

                await ExecuteNonQueryAsync($@"
                    UPDATE {GroupFileTable.TableName} file
                    SET
                        {GroupFileColumns.IsDeleted}=TRUE,
                        {GroupFileColumns.DeletedBy}=@DeletedBy,
                        {GroupFileColumns.DeletedAt}=CURRENT_TIMESTAMP,
                        {GroupFileColumns.DeletedRootType}=@RootTypeGroup,
                        {GroupFileColumns.DeletedRootId}=@GroupId
                    WHERE
                        file.{GroupFileColumns.GroupId}=@GroupId
                        AND file.{GroupFileColumns.IsDeleted}=FALSE;",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@GroupId", groupId);
                        cmd.Parameters.AddWithValue("@DeletedBy", deletedBy);
                        cmd.Parameters.AddWithValue("@RootTypeGroup", RootTypeGroup);
                    });
            }, "Error moving group to trash.");
        }

        public async Task DeleteFolderAsync(long folderId, long deletedBy)
        {
            await ExecuteInTransactionAsync(async transaction =>
            {
                await EnsureDeleteRootColumnsAsync(transaction);

                await ExecuteNonQueryAsync($@"
                    WITH RECURSIVE FolderTree AS
                    (
                        SELECT {GroupFolderColumns.FolderId}
                        FROM {GroupFolderTable.TableName}
                        WHERE {GroupFolderColumns.FolderId}=@FolderId

                        UNION ALL

                        SELECT child.{GroupFolderColumns.FolderId}
                        FROM {GroupFolderTable.TableName} child
                        INNER JOIN FolderTree parent
                            ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    )
                    UPDATE {GroupFolderTable.TableName} folder
                    SET
                        {GroupFolderColumns.IsDeleted}=TRUE,
                        {GroupFolderColumns.DeletedBy}=@DeletedBy,
                        {GroupFolderColumns.DeletedAt}=CURRENT_TIMESTAMP,
                        {GroupFolderColumns.DeletedRootType}=@RootTypeFolder,
                        {GroupFolderColumns.DeletedRootId}=@FolderId
                    WHERE
                        folder.{GroupFolderColumns.IsDeleted}=FALSE
                        AND folder.{GroupFolderColumns.FolderId} IN
                        (
                            SELECT {GroupFolderColumns.FolderId}
                            FROM FolderTree
                        );",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@FolderId", folderId);
                        cmd.Parameters.AddWithValue("@DeletedBy", deletedBy);
                        cmd.Parameters.AddWithValue("@RootTypeFolder", RootTypeFolder);
                    });

                await ExecuteNonQueryAsync($@"
                    WITH RECURSIVE FolderTree AS
                    (
                        SELECT {GroupFolderColumns.FolderId}
                        FROM {GroupFolderTable.TableName}
                        WHERE {GroupFolderColumns.FolderId}=@FolderId

                        UNION ALL

                        SELECT child.{GroupFolderColumns.FolderId}
                        FROM {GroupFolderTable.TableName} child
                        INNER JOIN FolderTree parent
                            ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    )
                    UPDATE {GroupFileTable.TableName} file
                    SET
                        {GroupFileColumns.IsDeleted}=TRUE,
                        {GroupFileColumns.DeletedBy}=@DeletedBy,
                        {GroupFileColumns.DeletedAt}=CURRENT_TIMESTAMP,
                        {GroupFileColumns.DeletedRootType}=@RootTypeFolder,
                        {GroupFileColumns.DeletedRootId}=@FolderId
                    WHERE
                        file.{GroupFileColumns.IsDeleted}=FALSE
                        AND file.{GroupFileColumns.FolderId} IN
                        (
                            SELECT {GroupFolderColumns.FolderId}
                            FROM FolderTree
                        );",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@FolderId", folderId);
                        cmd.Parameters.AddWithValue("@DeletedBy", deletedBy);
                        cmd.Parameters.AddWithValue("@RootTypeFolder", RootTypeFolder);
                    });
            }, "Error moving folder to trash.");
        }

        public async Task DeleteFileAsync(long fileId, long deletedBy)
        {
            await ExecuteInTransactionAsync(async transaction =>
            {
                await EnsureDeleteRootColumnsAsync(transaction);

                await ExecuteNonQueryAsync($@"
                    UPDATE {GroupFileTable.TableName}
                    SET
                        {GroupFileColumns.IsDeleted}=TRUE,
                        {GroupFileColumns.DeletedBy}=@DeletedBy,
                        {GroupFileColumns.DeletedAt}=CURRENT_TIMESTAMP,
                        {GroupFileColumns.DeletedRootType}=@RootTypeFile,
                        {GroupFileColumns.DeletedRootId}=@FileId
                    WHERE
                        {GroupFileColumns.FileId}=@FileId
                        AND {GroupFileColumns.IsDeleted}=FALSE;",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@FileId", fileId);
                        cmd.Parameters.AddWithValue("@DeletedBy", deletedBy);
                        cmd.Parameters.AddWithValue("@RootTypeFile", RootTypeFile);
                    });
            }, "Error moving file to trash.");
        }

        public async Task RestoreGroupAsync(long groupId)
        {
            await ExecuteInTransactionAsync(async transaction =>
            {
                await EnsureDeleteRootColumnsAsync(transaction);

                await ExecuteNonQueryAsync($@"
                    UPDATE {GroupTable.TableName}
                    SET
                        {GroupColumns.IsDeleted}=FALSE,
                        {GroupColumns.DeletedBy}=NULL,
                        {GroupColumns.DeletedAt}=NULL
                    WHERE {GroupColumns.GroupId}=@GroupId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@GroupId", groupId));

                await ExecuteNonQueryAsync($@"
                    UPDATE {GroupFolderTable.TableName}
                    SET
                        {GroupFolderColumns.IsDeleted}=FALSE,
                        {GroupFolderColumns.DeletedBy}=NULL,
                        {GroupFolderColumns.DeletedAt}=NULL,
                        {GroupFolderColumns.DeletedRootType}=NULL,
                        {GroupFolderColumns.DeletedRootId}=NULL
                    WHERE
                        {GroupFolderColumns.DeletedRootType}=@RootTypeGroup
                        AND {GroupFolderColumns.DeletedRootId}=@GroupId;",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@GroupId", groupId);
                        cmd.Parameters.AddWithValue("@RootTypeGroup", RootTypeGroup);
                    });

                await ExecuteNonQueryAsync($@"
                    UPDATE {GroupFileTable.TableName}
                    SET
                        {GroupFileColumns.IsDeleted}=FALSE,
                        {GroupFileColumns.DeletedBy}=NULL,
                        {GroupFileColumns.DeletedAt}=NULL,
                        {GroupFileColumns.DeletedRootType}=NULL,
                        {GroupFileColumns.DeletedRootId}=NULL
                    WHERE
                        {GroupFileColumns.DeletedRootType}=@RootTypeGroup
                        AND {GroupFileColumns.DeletedRootId}=@GroupId;",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@GroupId", groupId);
                        cmd.Parameters.AddWithValue("@RootTypeGroup", RootTypeGroup);
                    });
            }, "Error restoring group.");
        }

        public async Task RestoreFolderAsync(long folderId)
        {
            await ExecuteInTransactionAsync(async transaction =>
            {
                await EnsureDeleteRootColumnsAsync(transaction);
                await ThrowIfDeletedParentExistsAsync(
                    folderId,
                    transaction,
                    "This folder cannot be restored because its parent folder is still in the Recycle Bin. Restore the parent folder first.");
                await ThrowIfFolderGroupDeletedAsync(
                    folderId,
                    transaction,
                    "This folder cannot be restored because its group is still in the Recycle Bin. Restore the group first.");

                await ExecuteNonQueryAsync($@"
                    WITH RECURSIVE FolderTree AS
                    (
                        SELECT
                            root.{GroupFolderColumns.FolderId},
                            root.{GroupFolderColumns.DeletedRootType},
                            root.{GroupFolderColumns.DeletedRootId}
                        FROM {GroupFolderTable.TableName} root
                        WHERE root.{GroupFolderColumns.FolderId}=@FolderId

                        UNION ALL

                        SELECT
                            child.{GroupFolderColumns.FolderId},
                            child.{GroupFolderColumns.DeletedRootType},
                            child.{GroupFolderColumns.DeletedRootId}
                        FROM {GroupFolderTable.TableName} child
                        INNER JOIN FolderTree parent
                            ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                        WHERE
                            child.{GroupFolderColumns.DeletedRootType}=parent.{GroupFolderColumns.DeletedRootType}
                            AND child.{GroupFolderColumns.DeletedRootId}=parent.{GroupFolderColumns.DeletedRootId}
                    )
                    UPDATE {GroupFileTable.TableName} file
                    SET
                        {GroupFileColumns.IsDeleted}=FALSE,
                        {GroupFileColumns.DeletedBy}=NULL,
                        {GroupFileColumns.DeletedAt}=NULL,
                        {GroupFileColumns.DeletedRootType}=NULL,
                        {GroupFileColumns.DeletedRootId}=NULL
                    FROM {GroupFolderTable.TableName} root
                    WHERE
                        root.{GroupFolderColumns.FolderId}=@FolderId
                        AND file.{GroupFileColumns.FolderId} IN
                        (
                            SELECT {GroupFolderColumns.FolderId}
                            FROM FolderTree
                        )
                        AND
                        (
                            (
                                file.{GroupFileColumns.DeletedRootType}=root.{GroupFolderColumns.DeletedRootType}
                                AND file.{GroupFileColumns.DeletedRootId}=root.{GroupFolderColumns.DeletedRootId}
                            )
                            OR file.{GroupFileColumns.DeletedRootType} IS NULL
                            OR root.{GroupFolderColumns.DeletedRootType} IS NULL
                        );",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@FolderId", folderId));

                await ExecuteNonQueryAsync($@"
                    WITH RECURSIVE FolderTree AS
                    (
                        SELECT
                            root.{GroupFolderColumns.FolderId},
                            root.{GroupFolderColumns.DeletedRootType},
                            root.{GroupFolderColumns.DeletedRootId}
                        FROM {GroupFolderTable.TableName} root
                        WHERE root.{GroupFolderColumns.FolderId}=@FolderId

                        UNION ALL

                        SELECT
                            child.{GroupFolderColumns.FolderId},
                            child.{GroupFolderColumns.DeletedRootType},
                            child.{GroupFolderColumns.DeletedRootId}
                        FROM {GroupFolderTable.TableName} child
                        INNER JOIN FolderTree parent
                            ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                        WHERE
                            child.{GroupFolderColumns.DeletedRootType}=parent.{GroupFolderColumns.DeletedRootType}
                            AND child.{GroupFolderColumns.DeletedRootId}=parent.{GroupFolderColumns.DeletedRootId}
                    )
                    UPDATE {GroupFolderTable.TableName} folder
                    SET
                        {GroupFolderColumns.IsDeleted}=FALSE,
                        {GroupFolderColumns.DeletedBy}=NULL,
                        {GroupFolderColumns.DeletedAt}=NULL,
                        {GroupFolderColumns.DeletedRootType}=NULL,
                        {GroupFolderColumns.DeletedRootId}=NULL
                    WHERE folder.{GroupFolderColumns.FolderId} IN
                    (
                        SELECT {GroupFolderColumns.FolderId}
                        FROM FolderTree
                    );",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@FolderId", folderId));
            }, "Error restoring folder.");
        }

        public async Task RestoreFileAsync(long fileId)
        {
            await ExecuteInTransactionAsync(async transaction =>
            {
                await EnsureDeleteRootColumnsAsync(transaction);
                await ThrowIfFileParentDeletedAsync(
                    fileId,
                    transaction,
                    "This file cannot be restored because its parent folder is still in the Recycle Bin.");
                await ThrowIfFileGroupDeletedAsync(
                    fileId,
                    transaction,
                    "This file cannot be restored because its group is still in the Recycle Bin. Restore the group first.");

                await ExecuteNonQueryAsync($@"
                    UPDATE {GroupFileTable.TableName}
                    SET
                        {GroupFileColumns.IsDeleted}=FALSE,
                        {GroupFileColumns.DeletedBy}=NULL,
                        {GroupFileColumns.DeletedAt}=NULL,
                        {GroupFileColumns.DeletedRootType}=NULL,
                        {GroupFileColumns.DeletedRootId}=NULL
                    WHERE {GroupFileColumns.FileId}=@FileId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@FileId", fileId));
            }, "Error restoring file.");
        }

        public async Task PermanentDeleteGroupAsync(long groupId)
        {
            await ExecuteInTransactionAsync(async transaction =>
            {
                await EnsureDeleteRootColumnsAsync(transaction);

                await ExecuteNonQueryAsync($@"
                    DELETE FROM {GroupFileRoleTable.TableName} file_role
                    USING {GroupFileTable.TableName} file
                    WHERE
                        file_role.{GroupFileRoleColumns.FileId}=file.{GroupFileColumns.FileId}
                        AND file.{GroupFileColumns.GroupId}=@GroupId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@GroupId", groupId));

                await ExecuteNonQueryAsync($@"
                    DELETE FROM {GroupFolderRoleTable.TableName} folder_role
                    USING {GroupFolderTable.TableName} folder
                    WHERE
                        folder_role.{GroupFolderRoleColumns.FolderId}=folder.{GroupFolderColumns.FolderId}
                        AND folder.{GroupFolderColumns.GroupId}=@GroupId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@GroupId", groupId));

                await ExecuteNonQueryAsync($@"
                    DELETE FROM {GroupRoleTable.TableName}
                    WHERE {GroupRoleColumns.GroupId}=@GroupId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@GroupId", groupId));

                await ExecuteNonQueryAsync($@"
                    DELETE FROM {GroupFileTable.TableName}
                    WHERE {GroupFileColumns.GroupId}=@GroupId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@GroupId", groupId));

                await ExecuteNonQueryAsync($@"
                    DELETE FROM {GroupFolderTable.TableName}
                    WHERE {GroupFolderColumns.GroupId}=@GroupId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@GroupId", groupId));

                await ExecuteNonQueryAsync($@"
                    DELETE FROM {GroupTable.TableName}
                    WHERE {GroupColumns.GroupId}=@GroupId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@GroupId", groupId));
            }, "Error permanently deleting group.");
        }

        public async Task PermanentDeleteFolderAsync(long folderId)
        {
            await ExecuteInTransactionAsync(async transaction =>
            {
                await EnsureDeleteRootColumnsAsync(transaction);
                await ThrowIfDeletedParentExistsAsync(
                    folderId,
                    transaction,
                    "This folder cannot be permanently deleted because its parent folder is still in the Recycle Bin. Permanently delete the parent folder first.");

                await ExecuteNonQueryAsync($@"
                    UPDATE {GroupFolderTable.TableName} child
                    SET {GroupFolderColumns.ParentFolderId}=NULL
                    FROM {GroupFolderTable.TableName} root
                    WHERE
                        root.{GroupFolderColumns.FolderId}=@FolderId
                        AND child.{GroupFolderColumns.ParentFolderId} IN
                        (
                            SELECT {GroupFolderColumns.FolderId}
                            FROM {GroupFolderTable.TableName}
                            WHERE
                                {GroupFolderColumns.DeletedRootType}=root.{GroupFolderColumns.DeletedRootType}
                                AND {GroupFolderColumns.DeletedRootId}=root.{GroupFolderColumns.DeletedRootId}
                        )
                        AND NOT
                        (
                            child.{GroupFolderColumns.DeletedRootType}=root.{GroupFolderColumns.DeletedRootType}
                            AND child.{GroupFolderColumns.DeletedRootId}=root.{GroupFolderColumns.DeletedRootId}
                        );",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@FolderId", folderId));

                await DeleteFilesForFolderRootAsync(folderId, transaction);
                await DeleteFoldersForFolderRootAsync(folderId, transaction);
            }, "Error permanently deleting folder.");
        }

        public async Task PermanentDeleteFileAsync(long fileId)
        {
            await ExecuteInTransactionAsync(async transaction =>
            {
                await EnsureDeleteRootColumnsAsync(transaction);
                await ThrowIfFileParentDeletedAsync(
                    fileId,
                    transaction,
                    "This file cannot be permanently deleted because its parent folder is still in the Recycle Bin.");

                await ExecuteNonQueryAsync($@"
                    DELETE FROM {GroupFileRoleTable.TableName}
                    WHERE {GroupFileRoleColumns.FileId}=@FileId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@FileId", fileId));

                await ExecuteNonQueryAsync($@"
                    DELETE FROM {GroupFileTable.TableName}
                    WHERE {GroupFileColumns.FileId}=@FileId;",
                    transaction,
                    cmd => cmd.Parameters.AddWithValue("@FileId", fileId));
            }, "Error permanently deleting file.");
        }

        public async Task<List<string>> GetGroupObjectKeysAsync(long groupId)
        {
            string query = $@"
                SELECT {GroupFileColumns.ObjectKey}
                FROM {GroupFileTable.TableName}
                WHERE
                    {GroupFileColumns.GroupId}=@GroupId
                    AND {GroupFileColumns.ObjectKey} <> '';";

            return await GetObjectKeysAsync(
                query,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@GroupId", groupId);
                });
        }

        public async Task<List<string>> GetFolderObjectKeysAsync(long folderId)
        {
            string query = $@"
                WITH RECURSIVE FolderTree AS
                (
                    SELECT
                        root.{GroupFolderColumns.FolderId},
                        root.{GroupFolderColumns.DeletedRootType},
                        root.{GroupFolderColumns.DeletedRootId}
                    FROM {GroupFolderTable.TableName} root
                    WHERE root.{GroupFolderColumns.FolderId}=@FolderId

                    UNION ALL

                    SELECT
                        child.{GroupFolderColumns.FolderId},
                        child.{GroupFolderColumns.DeletedRootType},
                        child.{GroupFolderColumns.DeletedRootId}
                    FROM {GroupFolderTable.TableName} child
                    INNER JOIN FolderTree parent
                        ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    WHERE
                        (
                            (
                                child.{GroupFolderColumns.DeletedRootType}=parent.{GroupFolderColumns.DeletedRootType}
                                AND child.{GroupFolderColumns.DeletedRootId}=parent.{GroupFolderColumns.DeletedRootId}
                            )
                            OR child.{GroupFolderColumns.DeletedRootType} IS NULL
                            OR parent.{GroupFolderColumns.DeletedRootType} IS NULL
                        )
                )
                SELECT file.{GroupFileColumns.ObjectKey}
                FROM {GroupFileTable.TableName} file
                INNER JOIN {GroupFolderTable.TableName} root
                    ON root.{GroupFolderColumns.FolderId}=@FolderId
                WHERE
                    file.{GroupFileColumns.FolderId} IN
                    (
                        SELECT {GroupFolderColumns.FolderId}
                        FROM FolderTree
                    )
                    AND
                    (
                        (
                            file.{GroupFileColumns.DeletedRootType}=root.{GroupFolderColumns.DeletedRootType}
                            AND file.{GroupFileColumns.DeletedRootId}=root.{GroupFolderColumns.DeletedRootId}
                        )
                        OR file.{GroupFileColumns.DeletedRootType} IS NULL
                        OR root.{GroupFolderColumns.DeletedRootType} IS NULL
                    )
                    AND file.{GroupFileColumns.ObjectKey} <> '';";

            return await GetObjectKeysAsync(query, cmd =>
                cmd.Parameters.AddWithValue("@FolderId", folderId));
        }

        public async Task<List<string>> GetFileObjectKeysAsync(long fileId)
        {
            string query = $@"
                SELECT {GroupFileColumns.ObjectKey}
                FROM {GroupFileTable.TableName}
                WHERE
                    {GroupFileColumns.FileId}=@FileId
                    AND {GroupFileColumns.ObjectKey} <> '';";

            return await GetObjectKeysAsync(query, cmd =>
                cmd.Parameters.AddWithValue("@FileId", fileId));
        }

        private static string BuildRecycleItemsQuery(
            string groupWhere,
            string folderWhere,
            string fileWhere)
        {
            return $@"
                SELECT
                    g.{GroupColumns.GroupId} AS Id,
                    g.{GroupColumns.GroupName} AS Name,
                    'group' AS Type,
                    g.{GroupColumns.GroupName} AS GroupName,
                    '/' || g.{GroupColumns.GroupName} AS Path,
                    COALESCE(u.{UserColumns.FirstName} || ' ' || u.{UserColumns.LastName}, '-') AS DeletedBy,
                    g.{GroupColumns.DeletedAt} AS DeletedAt,
                    COALESCE(SUM(f.{GroupFileColumns.FileSize}), 0)::BIGINT AS Size,
                    'folder' AS Icon,
                    COALESCE(g.{GroupColumns.Description}, '') AS Description,
                    g.{GroupColumns.GroupId} AS GroupId,
                    NULL::BIGINT AS ParentFolderId,
                    TRUE AS IsFolder
                FROM {GroupTable.TableName} g
                LEFT JOIN {UserTable.TableName} u
                    ON u.{UserColumns.UserId}=g.{GroupColumns.DeletedBy}
                LEFT JOIN {GroupFileTable.TableName} f
                    ON f.{GroupFileColumns.GroupId}=g.{GroupColumns.GroupId}
                    AND f.{GroupFileColumns.IsDeleted}=TRUE
                    AND f.{GroupFileColumns.DeletedRootType}=@RootTypeGroup
                    AND f.{GroupFileColumns.DeletedRootId}=g.{GroupColumns.GroupId}
                WHERE {groupWhere}
                GROUP BY
                    g.{GroupColumns.GroupId},
                    g.{GroupColumns.GroupName},
                    g.{GroupColumns.Description},
                    g.{GroupColumns.DeletedAt},
                    u.{UserColumns.FirstName},
                    u.{UserColumns.LastName}

                UNION ALL

                SELECT
                    fol.{GroupFolderColumns.FolderId} AS Id,
                    fol.{GroupFolderColumns.FolderName} AS Name,
                    'folder' AS Type,
                    g.{GroupColumns.GroupName} AS GroupName,
                    fol.{GroupFolderColumns.FullPath} AS Path,
                    COALESCE(u.{UserColumns.FirstName} || ' ' || u.{UserColumns.LastName}, '-') AS DeletedBy,
                    fol.{GroupFolderColumns.DeletedAt} AS DeletedAt,
                    COALESCE(SUM(file_item.{GroupFileColumns.FileSize}), 0)::BIGINT AS Size,
                    'folder' AS Icon,
                    COALESCE(fol.{GroupFolderColumns.Description}, '') AS Description,
                    fol.{GroupFolderColumns.GroupId} AS GroupId,
                    fol.{GroupFolderColumns.ParentFolderId} AS ParentFolderId,
                    TRUE AS IsFolder
                FROM {GroupFolderTable.TableName} fol
                INNER JOIN {GroupTable.TableName} g
                    ON g.{GroupColumns.GroupId}=fol.{GroupFolderColumns.GroupId}
                LEFT JOIN {UserTable.TableName} u
                    ON u.{UserColumns.UserId}=fol.{GroupFolderColumns.DeletedBy}
                LEFT JOIN {GroupFileTable.TableName} file_item
                    ON file_item.{GroupFileColumns.DeletedRootType}=fol.{GroupFolderColumns.DeletedRootType}
                    AND file_item.{GroupFileColumns.DeletedRootId}=fol.{GroupFolderColumns.DeletedRootId}
                WHERE {folderWhere}
                GROUP BY
                    fol.{GroupFolderColumns.FolderId},
                    fol.{GroupFolderColumns.FolderName},
                    fol.{GroupFolderColumns.FullPath},
                    fol.{GroupFolderColumns.Description},
                    fol.{GroupFolderColumns.GroupId},
                    fol.{GroupFolderColumns.ParentFolderId},
                    fol.{GroupFolderColumns.DeletedAt},
                    g.{GroupColumns.GroupName},
                    u.{UserColumns.FirstName},
                    u.{UserColumns.LastName}

                UNION ALL

                SELECT
                    file.{GroupFileColumns.FileId} AS Id,
                    file.{GroupFileColumns.FileName} AS Name,
                    'file' AS Type,
                    g.{GroupColumns.GroupName} AS GroupName,
                    fol.{GroupFolderColumns.FullPath} || '/' || file.{GroupFileColumns.FileName} AS Path,
                    COALESCE(u.{UserColumns.FirstName} || ' ' || u.{UserColumns.LastName}, '-') AS DeletedBy,
                    file.{GroupFileColumns.DeletedAt} AS DeletedAt,
                    file.{GroupFileColumns.FileSize} AS Size,
                    'file' AS Icon,
                    COALESCE(file.{GroupFileColumns.Description}, '') AS Description,
                    file.{GroupFileColumns.GroupId} AS GroupId,
                    file.{GroupFileColumns.FolderId} AS ParentFolderId,
                    FALSE AS IsFolder
                FROM {GroupFileTable.TableName} file
                INNER JOIN {GroupTable.TableName} g
                    ON g.{GroupColumns.GroupId}=file.{GroupFileColumns.GroupId}
                INNER JOIN {GroupFolderTable.TableName} fol
                    ON fol.{GroupFolderColumns.FolderId}=file.{GroupFileColumns.FolderId}
                LEFT JOIN {UserTable.TableName} u
                    ON u.{UserColumns.UserId}=file.{GroupFileColumns.DeletedBy}
                WHERE {fileWhere}
                ORDER BY Type DESC, Name;";
        }

        private async Task<List<RecycleBinItemVM>> ReadRecycleItemsAsync(NpgsqlCommand cmd)
        {
            List<RecycleBinItemVM> items = new();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                long? size = reader.IsDBNull(reader.GetOrdinal("size"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("size"));

                items.Add(new RecycleBinItemVM
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Type = reader.GetString(reader.GetOrdinal("type")),
                    GroupName = reader.GetString(reader.GetOrdinal("groupname")),
                    Path = reader.GetString(reader.GetOrdinal("path")),
                    DeletedBy = reader.GetString(reader.GetOrdinal("deletedby")),
                    DeletedAt = reader.IsDBNull(reader.GetOrdinal("deletedat"))
                        ? null
                        : reader.GetDateTime(reader.GetOrdinal("deletedat")),
                    Size = size,
                    SizeDisplay = FileSizeHelper.Format(size),
                    Icon = reader.GetString(reader.GetOrdinal("icon")),
                    Description = reader.GetString(reader.GetOrdinal("description")),
                    GroupId = reader.IsDBNull(reader.GetOrdinal("groupid"))
                        ? null
                        : reader.GetInt64(reader.GetOrdinal("groupid")),
                    ParentFolderId = reader.IsDBNull(reader.GetOrdinal("parentfolderid"))
                        ? null
                        : reader.GetInt64(reader.GetOrdinal("parentfolderid")),
                    IsFolder = reader.GetBoolean(reader.GetOrdinal("isfolder"))
                });
            }

            return items;
        }

        private async Task ThrowIfDeletedParentExistsAsync(
            long folderId,
            NpgsqlTransaction transaction,
            string message)
        {
            string query = $@"
                SELECT EXISTS
                (
                    SELECT 1
                    FROM {GroupFolderTable.TableName} folder
                    INNER JOIN {GroupFolderTable.TableName} parent
                        ON parent.{GroupFolderColumns.FolderId}=folder.{GroupFolderColumns.ParentFolderId}
                    WHERE
                        folder.{GroupFolderColumns.FolderId}=@FolderId
                        AND parent.{GroupFolderColumns.IsDeleted}=TRUE
                );";

            await using var cmd = new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@FolderId", folderId);

            if (Convert.ToBoolean(await cmd.ExecuteScalarAsync()))
            {
                throw new InvalidOperationException(message);
            }
        }

        private async Task ThrowIfFileParentDeletedAsync(
            long fileId,
            NpgsqlTransaction transaction,
            string message)
        {
            string query = $@"
                SELECT EXISTS
                (
                    SELECT 1
                    FROM {GroupFileTable.TableName} file
                    INNER JOIN {GroupFolderTable.TableName} parent
                        ON parent.{GroupFolderColumns.FolderId}=file.{GroupFileColumns.FolderId}
                    WHERE
                        file.{GroupFileColumns.FileId}=@FileId
                        AND parent.{GroupFolderColumns.IsDeleted}=TRUE
                );";

            await using var cmd = new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@FileId", fileId);

            if (Convert.ToBoolean(await cmd.ExecuteScalarAsync()))
            {
                throw new InvalidOperationException(message);
            }
        }

        private async Task ThrowIfFolderGroupDeletedAsync(
            long folderId,
            NpgsqlTransaction transaction,
            string message)
        {
            string query = $@"
                SELECT EXISTS
                (
                    SELECT 1
                    FROM {GroupFolderTable.TableName} folder
                    INNER JOIN {GroupTable.TableName} group_item
                        ON group_item.{GroupColumns.GroupId}=folder.{GroupFolderColumns.GroupId}
                    WHERE
                        folder.{GroupFolderColumns.FolderId}=@FolderId
                        AND group_item.{GroupColumns.IsDeleted}=TRUE
                );";

            await using var cmd = new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@FolderId", folderId);

            if (Convert.ToBoolean(await cmd.ExecuteScalarAsync()))
            {
                throw new InvalidOperationException(message);
            }
        }

        private async Task ThrowIfFileGroupDeletedAsync(
            long fileId,
            NpgsqlTransaction transaction,
            string message)
        {
            string query = $@"
                SELECT EXISTS
                (
                    SELECT 1
                    FROM {GroupFileTable.TableName} file
                    INNER JOIN {GroupTable.TableName} group_item
                        ON group_item.{GroupColumns.GroupId}=file.{GroupFileColumns.GroupId}
                    WHERE
                        file.{GroupFileColumns.FileId}=@FileId
                        AND group_item.{GroupColumns.IsDeleted}=TRUE
                );";

            await using var cmd = new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@FileId", fileId);

            if (Convert.ToBoolean(await cmd.ExecuteScalarAsync()))
            {
                throw new InvalidOperationException(message);
            }
        }

        private async Task DeleteFilesForRootAsync(
            string rootType,
            long rootId,
            NpgsqlTransaction transaction)
        {
            await ExecuteNonQueryAsync($@"
                DELETE FROM {GroupFileRoleTable.TableName} file_role
                USING {GroupFileTable.TableName} file
                WHERE
                    file_role.{GroupFileRoleColumns.FileId}=file.{GroupFileColumns.FileId}
                    AND file.{GroupFileColumns.DeletedRootType}=@RootType
                    AND file.{GroupFileColumns.DeletedRootId}=@RootId;",
                transaction,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@RootType", rootType);
                    cmd.Parameters.AddWithValue("@RootId", rootId);
                });

            await ExecuteNonQueryAsync($@"
                DELETE FROM {GroupFileTable.TableName}
                WHERE
                    {GroupFileColumns.DeletedRootType}=@RootType
                    AND {GroupFileColumns.DeletedRootId}=@RootId;",
                transaction,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@RootType", rootType);
                    cmd.Parameters.AddWithValue("@RootId", rootId);
                });
        }

        private async Task DeleteFoldersForRootAsync(
            string rootType,
            long rootId,
            NpgsqlTransaction transaction)
        {
            await ExecuteNonQueryAsync($@"
                DELETE FROM {GroupFolderRoleTable.TableName} folder_role
                USING {GroupFolderTable.TableName} folder
                WHERE
                    folder_role.{GroupFolderRoleColumns.FolderId}=folder.{GroupFolderColumns.FolderId}
                    AND folder.{GroupFolderColumns.DeletedRootType}=@RootType
                    AND folder.{GroupFolderColumns.DeletedRootId}=@RootId;",
                transaction,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@RootType", rootType);
                    cmd.Parameters.AddWithValue("@RootId", rootId);
                });

            await ExecuteNonQueryAsync($@"
                DELETE FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.DeletedRootType}=@RootType
                    AND {GroupFolderColumns.DeletedRootId}=@RootId;",
                transaction,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@RootType", rootType);
                    cmd.Parameters.AddWithValue("@RootId", rootId);
                });
        }

        private async Task DeleteFilesForFolderRootAsync(
            long folderId,
            NpgsqlTransaction transaction)
        {
            await ExecuteNonQueryAsync($@"
                WITH RECURSIVE FolderTree AS
                (
                    SELECT
                        root.{GroupFolderColumns.FolderId},
                        root.{GroupFolderColumns.DeletedRootType},
                        root.{GroupFolderColumns.DeletedRootId}
                    FROM {GroupFolderTable.TableName} root
                    WHERE root.{GroupFolderColumns.FolderId}=@FolderId

                    UNION ALL

                    SELECT
                        child.{GroupFolderColumns.FolderId},
                        child.{GroupFolderColumns.DeletedRootType},
                        child.{GroupFolderColumns.DeletedRootId}
                    FROM {GroupFolderTable.TableName} child
                    INNER JOIN FolderTree parent
                        ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    WHERE
                        (
                            (
                                child.{GroupFolderColumns.DeletedRootType}=parent.{GroupFolderColumns.DeletedRootType}
                                AND child.{GroupFolderColumns.DeletedRootId}=parent.{GroupFolderColumns.DeletedRootId}
                            )
                            OR child.{GroupFolderColumns.DeletedRootType} IS NULL
                            OR parent.{GroupFolderColumns.DeletedRootType} IS NULL
                        )
                )
                DELETE FROM {GroupFileRoleTable.TableName} file_role
                USING {GroupFileTable.TableName} file
                INNER JOIN {GroupFolderTable.TableName} root
                    ON root.{GroupFolderColumns.FolderId}=@FolderId
                WHERE
                    file_role.{GroupFileRoleColumns.FileId}=file.{GroupFileColumns.FileId}
                    AND file.{GroupFileColumns.FolderId} IN
                    (
                        SELECT {GroupFolderColumns.FolderId}
                        FROM FolderTree
                    )
                    AND
                    (
                        (
                            file.{GroupFileColumns.DeletedRootType}=root.{GroupFolderColumns.DeletedRootType}
                            AND file.{GroupFileColumns.DeletedRootId}=root.{GroupFolderColumns.DeletedRootId}
                        )
                        OR file.{GroupFileColumns.DeletedRootType} IS NULL
                        OR root.{GroupFolderColumns.DeletedRootType} IS NULL
                    );",
                transaction,
                cmd => cmd.Parameters.AddWithValue("@FolderId", folderId));

            await ExecuteNonQueryAsync($@"
                WITH RECURSIVE FolderTree AS
                (
                    SELECT
                        root.{GroupFolderColumns.FolderId},
                        root.{GroupFolderColumns.DeletedRootType},
                        root.{GroupFolderColumns.DeletedRootId}
                    FROM {GroupFolderTable.TableName} root
                    WHERE root.{GroupFolderColumns.FolderId}=@FolderId

                    UNION ALL

                    SELECT
                        child.{GroupFolderColumns.FolderId},
                        child.{GroupFolderColumns.DeletedRootType},
                        child.{GroupFolderColumns.DeletedRootId}
                    FROM {GroupFolderTable.TableName} child
                    INNER JOIN FolderTree parent
                        ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    WHERE
                        (
                            (
                                child.{GroupFolderColumns.DeletedRootType}=parent.{GroupFolderColumns.DeletedRootType}
                                AND child.{GroupFolderColumns.DeletedRootId}=parent.{GroupFolderColumns.DeletedRootId}
                            )
                            OR child.{GroupFolderColumns.DeletedRootType} IS NULL
                            OR parent.{GroupFolderColumns.DeletedRootType} IS NULL
                        )
                )
                DELETE FROM {GroupFileTable.TableName} file
                USING {GroupFolderTable.TableName} root
                WHERE
                    root.{GroupFolderColumns.FolderId}=@FolderId
                    AND file.{GroupFileColumns.FolderId} IN
                    (
                        SELECT {GroupFolderColumns.FolderId}
                        FROM FolderTree
                    )
                    AND
                    (
                        (
                            file.{GroupFileColumns.DeletedRootType}=root.{GroupFolderColumns.DeletedRootType}
                            AND file.{GroupFileColumns.DeletedRootId}=root.{GroupFolderColumns.DeletedRootId}
                        )
                        OR file.{GroupFileColumns.DeletedRootType} IS NULL
                        OR root.{GroupFolderColumns.DeletedRootType} IS NULL
                    );",
                transaction,
                cmd => cmd.Parameters.AddWithValue("@FolderId", folderId));
        }

        private async Task DeleteFoldersForFolderRootAsync(
            long folderId,
            NpgsqlTransaction transaction)
        {
            await ExecuteNonQueryAsync($@"
                DELETE FROM {GroupFolderRoleTable.TableName} folder_role
                USING {GroupFolderTable.TableName} folder
                INNER JOIN {GroupFolderTable.TableName} root
                    ON root.{GroupFolderColumns.FolderId}=@FolderId
                WHERE
                    folder_role.{GroupFolderRoleColumns.FolderId}=folder.{GroupFolderColumns.FolderId}
                    AND folder.{GroupFolderColumns.DeletedRootType}=root.{GroupFolderColumns.DeletedRootType}
                    AND folder.{GroupFolderColumns.DeletedRootId}=root.{GroupFolderColumns.DeletedRootId};",
                transaction,
                cmd => cmd.Parameters.AddWithValue("@FolderId", folderId));

            await ExecuteNonQueryAsync($@"
                DELETE FROM {GroupFolderTable.TableName} folder
                USING {GroupFolderTable.TableName} root
                WHERE
                    root.{GroupFolderColumns.FolderId}=@FolderId
                    AND folder.{GroupFolderColumns.DeletedRootType}=root.{GroupFolderColumns.DeletedRootType}
                    AND folder.{GroupFolderColumns.DeletedRootId}=root.{GroupFolderColumns.DeletedRootId};",
                transaction,
                cmd => cmd.Parameters.AddWithValue("@FolderId", folderId));
        }

        private async Task<List<string>> GetObjectKeysAsync(
            string query,
            Action<NpgsqlCommand> configureCommand)
        {
            List<string> objectKeys = new();

            try
            {
                await _conn.OpenAsync();
                await EnsureDeleteRootColumnsAsync();

                await using var cmd = new NpgsqlCommand(query, _conn);

                configureCommand(cmd);

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        objectKeys.Add(reader.GetString(0));
                    }
                }

                return objectKeys;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving storage object keys.");
                throw;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        private async Task ExecuteInTransactionAsync(
            Func<NpgsqlTransaction, Task> action,
            string errorMessage)
        {
            try
            {
                await _conn.OpenAsync();
                await using var transaction = await _conn.BeginTransactionAsync();

                try
                {
                    await action(transaction);
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, errorMessage);
                throw;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        private async Task ExecuteNonQueryAsync(
            string query,
            NpgsqlTransaction transaction,
            Action<NpgsqlCommand> configureCommand)
        {
            await using var cmd = new NpgsqlCommand(query, _conn, transaction);

            configureCommand(cmd);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task EnsureDeleteRootColumnsAsync(NpgsqlTransaction? transaction = null)
        {
            string query = $@"
                ALTER TABLE {GroupFolderTable.TableName}
                    ADD COLUMN IF NOT EXISTS {GroupFolderColumns.DeletedRootType} VARCHAR(20),
                    ADD COLUMN IF NOT EXISTS {GroupFolderColumns.DeletedRootId} BIGINT;

                ALTER TABLE {GroupFileTable.TableName}
                    ADD COLUMN IF NOT EXISTS {GroupFileColumns.DeletedRootType} VARCHAR(20),
                    ADD COLUMN IF NOT EXISTS {GroupFileColumns.DeletedRootId} BIGINT;

                WITH RECURSIVE DeletedFolderRoots AS
                (
                    SELECT
                        folder.{GroupFolderColumns.FolderId},
                        folder.{GroupFolderColumns.FolderId} AS RootFolderId
                    FROM {GroupFolderTable.TableName} folder
                    LEFT JOIN {GroupFolderTable.TableName} parent
                        ON parent.{GroupFolderColumns.FolderId}=folder.{GroupFolderColumns.ParentFolderId}
                    WHERE
                        folder.{GroupFolderColumns.IsDeleted}=TRUE
                        AND folder.{GroupFolderColumns.DeletedRootType} IS NULL
                        AND
                        (
                            folder.{GroupFolderColumns.ParentFolderId} IS NULL
                            OR COALESCE(parent.{GroupFolderColumns.IsDeleted}, FALSE)=FALSE
                        )

                    UNION ALL

                    SELECT
                        child.{GroupFolderColumns.FolderId},
                        root.RootFolderId
                    FROM {GroupFolderTable.TableName} child
                    INNER JOIN DeletedFolderRoots root
                        ON child.{GroupFolderColumns.ParentFolderId}=root.{GroupFolderColumns.FolderId}
                    WHERE
                        child.{GroupFolderColumns.IsDeleted}=TRUE
                        AND child.{GroupFolderColumns.DeletedRootType} IS NULL
                )
                UPDATE {GroupFolderTable.TableName} folder
                SET
                    {GroupFolderColumns.DeletedRootType}=@RootTypeFolder,
                    {GroupFolderColumns.DeletedRootId}=root.RootFolderId
                FROM DeletedFolderRoots root
                WHERE folder.{GroupFolderColumns.FolderId}=root.{GroupFolderColumns.FolderId};

                UPDATE {GroupFolderTable.TableName} folder
                SET
                    {GroupFolderColumns.DeletedRootType}=@RootTypeFolder,
                    {GroupFolderColumns.DeletedRootId}=folder.{GroupFolderColumns.FolderId}
                WHERE
                    folder.{GroupFolderColumns.IsDeleted}=TRUE
                    AND folder.{GroupFolderColumns.DeletedRootType} IS NULL;

                UPDATE {GroupFileTable.TableName} file
                SET
                    {GroupFileColumns.DeletedRootType}=folder.{GroupFolderColumns.DeletedRootType},
                    {GroupFileColumns.DeletedRootId}=folder.{GroupFolderColumns.DeletedRootId}
                FROM {GroupFolderTable.TableName} folder
                WHERE
                    folder.{GroupFolderColumns.FolderId}=file.{GroupFileColumns.FolderId}
                    AND file.{GroupFileColumns.IsDeleted}=TRUE
                    AND file.{GroupFileColumns.DeletedRootType} IS NULL
                    AND folder.{GroupFolderColumns.IsDeleted}=TRUE
                    AND folder.{GroupFolderColumns.DeletedRootType} IS NOT NULL;

                UPDATE {GroupFileTable.TableName} file
                SET
                    {GroupFileColumns.DeletedRootType}=@RootTypeFile,
                    {GroupFileColumns.DeletedRootId}=file.{GroupFileColumns.FileId}
                WHERE
                    file.{GroupFileColumns.IsDeleted}=TRUE
                    AND file.{GroupFileColumns.DeletedRootType} IS NULL;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);

            cmd.Parameters.AddWithValue("@RootTypeFolder", RootTypeFolder);
            cmd.Parameters.AddWithValue("@RootTypeFile", RootTypeFile);

            await cmd.ExecuteNonQueryAsync();
        }

        private static void AddRootTypeParameters(NpgsqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@RootTypeGroup", RootTypeGroup);
            cmd.Parameters.AddWithValue("@RootTypeFolder", RootTypeFolder);
            cmd.Parameters.AddWithValue("@RootTypeFile", RootTypeFile);
        }
    }
}
