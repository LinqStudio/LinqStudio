# Repositories

File-system backed repository implementations for project and query persistence.

## Pattern

The project ID is the **project name** (not a full file path). Files are stored at `{BasePath}/{projectId}.linq` and queries in `{BasePath}/{projectId}.linq.queries/`.

## FileSystemStorageOptions

Configures the base directory for all project/query files. Register via `AddFileSystemRepositories(services, basePath)` in `ServiceCollectionExtensions`.

## FileSystemProjectRepository

Implements `IProjectRepository`. `SaveProjectAsync` uses the provided `projectId` when non-null (overwrite/rename an existing project); when `projectId` is null it derives the ID from `project.Name` (new project).

## FileSystemQueryRepository

Implements `IQueryRepository`. Translates project ID to file path internally.
