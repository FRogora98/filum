# A history you can trust

> Status: Implemented (2026-10-18). Reviewed (the owner, 2026-10-18, gap 4 of the engine audit, "integrity of the history")
> Area: engine (folder store: revisions, writes, vectors)
> Author/date: federico rogora (drafted by Claude), 2026-10-18

## Problem

The folder store keeps every revision of every file in `.filum/revisions.jsonl`, since 1.2 mostly as a change from the
previous revision (spec 037's delta format). An independent audit found four ways the history can be wrong without
anyone being told:
- **A damaged line breaks a chain in silence.** A line that does not parse is skipped (`JsonLines.Parse`), and a change
  whose base is missing is rebuilt from an empty text (`RevisionLog.ContentOf`): every later revision of that file, up to
  256 of them, comes back wrong, and nothing says so.
- **A crash can turn the agent's change into the person's.** A write puts the file on disk first and appends its revision
  after (`FileChangeSaver.Save`); a crash between the two leaves a file the log does not know, and the next call adopts it
  as a hand edit by the person (`FolderScanner`).
- **A rename can lose data on a crash.** A file is written to a temporary file and renamed (`AtomicFile`), but the
  temporary file is not flushed to disk before the rename.
- **Vectors of another model are mixed in.** `.filum/vectors.jsonl` does not say which embedding model made the vectors;
  after a model change, old and new vectors are compared as if they were the same space.

## Goal

The history is either right or loudly wrong, a crash never changes who wrote what, and vectors are only compared with
vectors of the same model.

## Non-goals

- No change to the public `IMemoryStore` contract other than the additions listed below; the Postgres store of hosts is
  untouched.
- No signing or encryption of the history: a hash detects damage, it does not stop someone who rewrites the log.
- No repair tool: damage is reported with where it starts; the person keeps their files.

## Desired behavior

1. **Format 3.** `.filum/format` becomes 3. A folder in format 1 or 2 is moved to 3 when opened (only the format file
   changes); its old lines are read as before. An engine that knows only format 2 refuses a format 3 folder (spec 037).
2. **Every new revision line carries the hash of its content** (`sha`: SHA-256 of the UTF-8 content, lower-case hex),
   whole lines and change lines alike. Lines written before format 3 have none.
3. **Reading checks.** When a revision's content is rebuilt or read, it is checked against its hash. A change whose base
   is missing, or a content that does not match its hash, stops the read with an error that names the file's path, the
   revision and the first revision that could not be rebuilt, and says that the files on disk are not affected. A line
   without a hash is read as before, except that a missing base is now an error too.
4. **Write order and recovery.** A change appends its revision first, then writes the file, then saves the state. When
   a folder is opened:
   - revisions newer than the saved state are applied again to the files (the change is completed, as the agent made
     it);
   - without a saved state, a file whose content is that of the revision before its latest one is completed the same
     way;
   - only what then still differs from the latest revision is a hand edit by the person.
5. **Durable writes.** A temporary file is flushed to disk before it is renamed over the real one.
6. **Vectors know their model.** `IEmbedder` gains `ModelId` (a default of null keeps existing embedders compiling).
   A store may implement the new `IMemoryVectorModels`: the id of the model its vectors were made with, and a way to
   drop them all. When the embedder's id differs from the store's, the vectors are dropped and made again; a store
   without the interface, or an embedder without an id, behaves as before. The folder store keeps the id in
   `.filum/vectors.model`; a folder without it holds vectors of the only model used before, which are kept and marked
   with the current id.
7. **Property and crash tests.** The diff of spec 037 is tested with random texts (applying the hunks between two texts
   gives the second), with FsCheck (FsCheck.Xunit 3.4.0, BSD-3-Clause, test projects only, with FSharp.Core under MIT).
   Crashes are simulated at each step of a write.

## Platform check

1. Generic: storage integrity, no domain.
2. Isolation and sensitivity: nothing changes in who can read what; error messages name a path, never content.

## Acceptance criteria

1. Given a format 1 or 2 folder (the v0.7 fixture among them), when it is opened, then its format file says 3, every old
   revision reads as before, and new revisions carry `sha`.
2. Given a format 3 folder and an engine that knows format 2, when it opens the folder, then it refuses it.
3. Given a revision line whose content was altered, when its history is read, then the read fails with an error naming
   the path and the revision; the files on disk are untouched.
4. Given a change line whose base line was removed, when that file's history is read, then the read fails the same way
   (before this spec it returned a wrong content).
5. Given a write stopped after its revision was appended and before the file was written, when the folder is opened,
   then the file holds the agent's content and the last revision's author is the agent, not the person.
6. Given the same stop with `state.json` deleted, the same holds.
7. Given a write stopped before its revision was appended (the file untouched), when the folder is opened, then nothing
   changed.
8. Given a real hand edit, when the folder is opened, then it is still adopted as the person's revision.
9. Given vectors made by model A and an embedder with id B, when vectors are needed, then they are made again with B
   and `.filum/vectors.model` says B; given a folder without the model file, its vectors are kept and the file is
   written.
10. For any two texts, applying the hunks from the first to the second gives the second (FsCheck).
