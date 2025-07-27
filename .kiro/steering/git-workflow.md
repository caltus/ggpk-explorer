# Git Workflow and Commit Standards

## Commit Requirements

### Mandatory Commit After Each Task
- **ALWAYS** make a commit immediately after completing each task from the implementation plan
- **NEVER** move to the next task without committing the current work
- Each commit should represent a complete, working state of the current task

### Commit Message Format

Use the following standardized format for all commits:

```
[TASK-XX] Task Name: Brief Description

Detailed description of what was implemented:
- Specific changes made
- Files added/modified
- Key functionality implemented
- Any important notes or decisions

Addresses: Requirement X.X, X.X (reference spec requirements)
```

### Examples

#### Good Commit Messages

```
[TASK-01] Project Setup: Core dependencies and structure

- Created WinUI 3 project targeting .NET 8
- Added references to LibGGPK3, LibBundle3, LibBundledGGPK3
- Configured Windows App SDK 1.6+ package references
- Set up dependency injection with Microsoft.Extensions.DependencyInjection
- Added CommunityToolkit.Mvvm for MVVM support

Addresses: Requirements 1.1, 1.2, 12.1
```

```
[TASK-06] Navigation TreeView: Lazy loading implementation

- Created NavigationTreeView UserControl with WinUI 3 TreeView
- Implemented lazy loading using TreeViewNode.HasUnrealizedChildren
- Added ProgressRing indicators for loading states
- Created TreeNodeViewModel with ObservableProperty
- Added MenuFlyout context menu for extract/properties

Addresses: Requirements 2.1, 2.3, 4.1, 4.4, 5.1, 5.2
```

#### Bad Commit Messages (Avoid These)

```
✗ "Fixed stuff"
✗ "WIP"
✗ "Updates"
✗ "Task 6 done"
```

## Commit Workflow

### Before Starting a Task
1. Ensure working directory is clean
2. Pull latest changes if working in a team
3. Create a feature branch if needed: `git checkout -b task-XX-brief-name`

### During Task Implementation
1. Make incremental commits for significant sub-steps if the task is large
2. Use descriptive commit messages even for incremental commits
3. Test that the application still builds and runs

### After Completing a Task
1. **MANDATORY**: Stage all changes related to the task
2. **MANDATORY**: Create a commit using the standardized format
3. **MANDATORY**: Verify the commit includes all necessary files
4. Push changes to remote repository
5. Only then proceed to the next task

### Git Commands for Task Completion

```bash
# Stage all changes for the completed task
git add .

# Create the mandatory commit with proper format
git commit -m "[TASK-XX] Task Name: Brief Description

Detailed description of implementation:
- Key changes made
- Files modified/added
- Functionality implemented

Addresses: Requirements X.X, X.X"

# Push to remote (if working with remote repository)
git push origin main
# or
git push origin task-XX-brief-name
```

## File Organization in Commits

### What to Include in Each Task Commit
- All source code files created/modified for the task
- Updated project files (.csproj, .sln)
- New XAML files and code-behind
- Updated configuration files if relevant
- Any new assets or resources

### What NOT to Include
- Temporary files (bin/, obj/, .vs/)
- User-specific settings files
- Build artifacts
- IDE-generated files (covered by .gitignore)

## Branch Strategy (Optional)

### For Complex Tasks
- Create feature branches: `task-XX-brief-description`
- Work on the task in the feature branch
- Commit with standard format
- Merge back to main after task completion

### For Simple Sequential Tasks
- Work directly on main branch
- Commit after each task completion
- Maintain linear history

## Quality Gates

### Before Each Commit
1. **Build Check**: Ensure the project builds successfully
2. **Basic Functionality**: Verify the application starts without errors
3. **Code Review**: Quick self-review of changes
4. **Requirement Mapping**: Confirm the task addresses specified requirements

### Commit Verification Checklist
- [ ] Task is completely finished according to task description
- [ ] All new files are included in the commit
- [ ] Commit message follows the standardized format
- [ ] Requirements are properly referenced
- [ ] Project builds successfully
- [ ] No temporary or generated files included

## Integration with Task Management

### Task Reference Format
Always reference the specific task number from `.kiro/specs/ggpk-explorer/tasks.md`:
- Task 1 = `[TASK-01]`
- Task 15 = `[TASK-15]`
- etc.

### Requirement Traceability
Include requirement references from `.kiro/specs/ggpk-explorer/requirements.md`:
- Format: `Addresses: Requirements 1.1, 2.3, 5.2`
- This maintains traceability from requirements through tasks to implementation

## Enforcement

### AI Assistant Guidelines
When working as an AI assistant on this project:
1. **NEVER** proceed to a new task without committing the current one
2. **ALWAYS** use the standardized commit message format
3. **ALWAYS** reference the task number and relevant requirements
4. **ALWAYS** verify the build succeeds before committing
5. **AUTOMATICALLY** commit changes when marking a task as completed using taskStatus tool
6. **MANDATORY** commit sequence when completing a task:
   - Stage all changes with `git add .`
   - Create commit with proper format
   - Update task status to completed only AFTER successful commit
7. If a commit fails, fix the issues before proceeding or updating task status

### Human Developer Guidelines
- Follow the same commit standards when working on the project
- Review AI-generated commits for compliance with these standards
- Use `git log --oneline` to verify commit message consistency

## Automatic Commit Enforcement

### Task Completion Workflow
When using the `taskStatus` tool to mark a task as completed, the following sequence MUST be followed:

1. **Pre-commit Validation**:
   - Verify all task requirements are met
   - Ensure project builds successfully
   - Confirm all new files are ready for commit

2. **Automatic Commit Process**:
   ```bash
   # Stage all changes
   git add .
   
   # Create standardized commit
   git commit -m "[TASK-XX] Task Name: Brief Description
   
   Detailed implementation description:
   - Key changes made
   - Files created/modified
   - Functionality implemented
   
   Addresses: Requirements X.X, X.X"
   ```

3. **Task Status Update**:
   - Only after successful commit, update task status to completed
   - If commit fails, resolve issues before marking task complete

### Enforcement Rules
- **NO EXCEPTIONS**: Every completed task MUST have a corresponding commit
- **ATOMIC OPERATIONS**: Task completion and commit are treated as a single atomic operation
- **ROLLBACK POLICY**: If commit fails, task remains in progress until issues are resolved

This workflow ensures proper version control, traceability, and maintains a clean development history throughout the GGPK Explorer project implementation.