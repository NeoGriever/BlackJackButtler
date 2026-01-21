#!/usr/bin/env fish
# push.fish — add/commit/push for current git repo

function die
    echo "Error: $argv" 1>&2
    exit 1
end

# Ensure we're inside a git repo
git rev-parse --is-inside-work-tree >/dev/null 2>&1; or die "Not inside a git repository."

# Determine current branch
set -l branch (git rev-parse --abbrev-ref HEAD 2>/dev/null)
test -n "$branch"; or die "Could not determine current branch."

# Prompt for commit message
echo -n "Commit message: "
read -l msg
if test -z "$msg"
    die "Commit message is empty."
end

# Stage changes
git add -A; or die "git add failed."

# If nothing to commit, still allow pushing (in case remote advanced)
set -l status_line (git status --porcelain | wc -l | string trim)
if test "$status_line" = "0"
    echo "No local changes to commit. Pushing branch '$branch'..."
    git push; or die "git push failed."
    echo "Done."
    exit 0
end

# Commit
git commit -m "$msg"; or die "git commit failed."

# Push (set upstream on first push if needed)
git push -u origin "$branch" 2>/dev/null
if test $status -ne 0
    echo "Push failed; retrying without -u (remote/upstream may already be set)..."
    git push; or die "git push failed."
end

echo "Done. Pushed '$branch' with commit: $msg"
