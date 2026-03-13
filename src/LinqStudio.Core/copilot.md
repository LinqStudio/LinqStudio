# LinqStudio.Core Notes

## Bug Fixes

### QueryGenerator Property - Missing Database Type Cases (2026-03-11)
Fixed: `Project.cs` QueryGenerator property switch expression was missing PostgreSQL and SQLite cases, causing NotSupportedException at runtime. Added the missing cases to match the TestConnectionAsync implementation which already had all four database types properly handled.
