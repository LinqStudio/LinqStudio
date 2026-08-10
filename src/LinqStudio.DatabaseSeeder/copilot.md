# LinqStudio.DatabaseSeeder

Console app that seeds two demo databases for each provider (MSSQL + MySQL) with independently generated sample data on Aspire startup. The databases use numbered names; their seed data still uses different snapshot dates for testing. Reads connection strings from environment variables injected by Aspire orchestration and passes each target's snapshot date to the shared seeding pipeline.
