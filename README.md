# Dependame

Dependame is like Dependabot, except it literally depends on me. When automation hits a wall and needs a human shaped push, Dependame steps in and says “fine, I’ll do it myself”, on a schedule.

This GitHub Action automates a set of recurring pull request maintenance tasks that normally require human intervention.

## What it does

1. Automatically enables auto merge on pull requests that target a configurable branch or a set of branches defined by a pattern.

2. Organizes all open pull requests by their target branch. For each target branch, it checks which pull requests are behind their base branch. When updates are needed, it selects one pull request at a time and updates its branch to reduce merge pressure and avoid mass rebases. A configurable label (default: "ready-for-review") is applied to updated PRs and removed from PRs that fall behind, helping reviewers identify which PRs are ready for review.

3. Pushes a blank commit to open pull requests to trigger downstream GitHub Actions workflows that would not otherwise run (e.g., workflows that don't trigger on Dependabot PRs). The action checks if the last commit was already made by the authenticated user and skips those PRs to avoid repeated bumps. However, it will still bump PRs where the last commit is co-authored or automated (where the committer differs from the author), ensuring workflows are properly triggered for these edge cases.

## Intent

Dependame is designed to make dependency and pull request maintenance predictable, serialized, and explicitly controlled, while still benefiting from automation.

It is particularly well suited for repositories with complex, slow, or resource intensive GitHub Actions workflows, where uncontrolled rebases or repeated CI runs would otherwise create excessive noise and load.