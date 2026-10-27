using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.Files;

internal sealed class MemoryFileEditor(MemoryWriter writer)
{
    private const string LineBreak = "\n";

    public Task<MemoryOutcome<MemoryChange>> WriteAsync(Guid userId, MemoryActor actor, string? path, string? content, CancellationToken cancellationToken) =>
        writer.ChangeAsync(userId, actor, path, MemoryOperation.Write, (valid, file, _) =>
            new ChangeTarget(valid, MemoryText.Normalize(content), file?.Sensitivity ?? MemorySensitivity.Normal, Deleted: false), cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> EditAsync(Guid userId, MemoryActor actor, string? path, string? oldText, string? newText, CancellationToken cancellationToken) =>
        writer.ChangeAsync(userId, actor, path, MemoryOperation.Edit, (file, _) =>
        {
            if (file is null)
            {
                return ChangeTarget.NotFound(FileRefusals.NoFileAt(path));
            }

            var old = MemoryText.Normalize(oldText);
            if (old.Length == 0)
            {
                return ChangeTarget.Refuse("The text to replace cannot be empty.");
            }

            var occurrences = MemoryText.Occurrences(file.Content, old);
            return occurrences switch
            {
                0 => ChangeTarget.Refuse($"The text to replace was not found in {path}."),
                > 1 => ChangeTarget.Refuse($"The text to replace appears {occurrences} times in {path}; include more of the text around it so it appears once."),
                _ => new ChangeTarget(file.Path, MemoryText.ReplaceOnce(file.Content, old, MemoryText.Normalize(newText)), file.Sensitivity, Deleted: false)
            };
        }, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> AppendAsync(Guid userId, MemoryActor actor, string? path, string? text, CancellationToken cancellationToken) =>
        writer.ChangeAsync(userId, actor, path, MemoryOperation.Append, (valid, file, _) =>
        {
            var addition = MemoryText.Normalize(text);
            if (addition.Trim().Length == 0)
            {
                return ChangeTarget.Refuse("There is nothing to add.");
            }

            if (!addition.EndsWith('\n'))
            {
                addition += LineBreak;
            }

            var current = file?.Content ?? string.Empty;
            var separator = current.Length == 0 || current.EndsWith('\n') ? string.Empty : LineBreak;
            return new ChangeTarget(valid, current + separator + addition, file?.Sensitivity ?? MemorySensitivity.Normal, Deleted: false);
        }, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> DeleteAsync(Guid userId, MemoryActor actor, string? path, CancellationToken cancellationToken) =>
        writer.ChangeAsync(userId, actor, path, MemoryOperation.Delete, (valid, file, _) =>
            MemoryPaths.IsCore(valid) ? ChangeTarget.Refuse(FileRefusals.CoreCannotBeDeleted)
            : file is null ? ChangeTarget.NotFound(FileRefusals.NoFileAt(path))
            : new ChangeTarget(file.Path, file.Content, file.Sensitivity, Deleted: true), cancellationToken);

    public async Task<MemoryOutcome<MemoryChange>> MoveAsync(Guid userId, MemoryActor actor, string? from, string? to, CancellationToken cancellationToken)
    {
        if (MemoryPaths.IsInvalid(to, out var error))
        {
            return MemoryOutcome<MemoryChange>.Refused(error);
        }

        if (MemoryPaths.IsCore(from ?? string.Empty) || MemoryPaths.IsCore(to))
        {
            return MemoryOutcome<MemoryChange>.Refused("The core (/filum.md) cannot be moved, and no file can take its place.");
        }

        if (from == to)
        {
            return MemoryOutcome<MemoryChange>.Refused("The file is already at that path.");
        }

        return await writer.ChangeAsync(userId, actor, from, MemoryOperation.Move, (_, file, targetTaken) =>
            file is null ? ChangeTarget.NotFound(FileRefusals.NoFileAt(from))
            : targetTaken ? ChangeTarget.Refuse($"There is already a file at {to}.")
            : new ChangeTarget(to, file.Content, file.Sensitivity, Deleted: false), otherPath: to, cancellationToken);
    }

    public Task<MemoryOutcome<MemoryChange>> SetSensitivityAsync(Guid userId, MemoryActor actor, string? path, string? level, CancellationToken cancellationToken) =>
        writer.ChangeAsync(userId, actor, path, MemoryOperation.Sensitivity, (valid, file, _) =>
            level is null || !MemorySensitivity.All.Contains(level) ? ChangeTarget.Refuse("The level must be normal, sensitive or private.")
            : MemoryPaths.IsCore(valid) ? ChangeTarget.Refuse("The core is read at every message; its sensitivity cannot change.")
            : file is null ? ChangeTarget.NotFound(FileRefusals.NoFileAt(path))
            : new ChangeTarget(file.Path, file.Content, level, Deleted: false), cancellationToken);
}
