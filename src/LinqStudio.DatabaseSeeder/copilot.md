# LinqStudio.DatabaseSeeder

Console app that seeds two dated databases for each provider (MSSQL + MySQL) with independently generated sample data on Aspire startup. Reads connection strings from environment variables injected by Aspire orchestration and passes each target's snapshot date to the shared seeding pipeline.
