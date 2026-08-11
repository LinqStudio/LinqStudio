# LinqStudio.Demo

Shared library containing demo database models (Customer, Order, Product, OrderItem), EF Core DbContext, Bogus-based data generation, and seeding logic used by DatabaseSeeder and tests. The seeder accepts a snapshot date so multiple databases can share a schema while containing independently generated data from different periods.
