# Dream Skill

## Purpose
A reflective memory consolidation pass. Synthesizes recent learnings into durable, well-organized memory files so future sessions orient quickly.

## When to Use
Activate this skill when the user says:
- "dream"
- "/dream"
- "consolidate my memories"
- "clean up memory"
- "organize memories"

## Instructions

You are performing a dream — a reflective pass over your memory files. Synthesize what you've learned recently into durable, well-organized memories so that future sessions can orient quickly.

Run in four phases:

### Phase 1 — Orient
- `ls` the memory directory (`~/.claude/projects/*/memory/`)
- Read `MEMORY.md`
- Skim existing topic files to understand current state

### Phase 2 — Gather recent signal
- Check for drifted facts — stale dates, references to files that no longer exist
- Grep transcripts or logs narrowly, only when needed to resolve ambiguity
- Look for contradictions between memory files and current codebase state

### Phase 3 — Consolidate
- Merge duplicate memories covering the same topic
- Convert relative dates to absolute dates (e.g., "last Thursday" → "2026-03-20")
- Delete facts contradicted by newer information
- Update stale file paths, function names, or flags that no longer exist

### Phase 4 — Prune and index
- Rebuild `MEMORY.md` to under 200 lines
- Remove pointers to deleted or merged files
- Add pointers for any new memory files created
- Keep each `MEMORY.md` entry to one line under 150 characters

Return a brief summary of what changed: files merged, facts updated, stale entries removed, and new entries added.