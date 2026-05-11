namespace SmartSave.Data.Migrations

open FluentMigrator

[<Migration(202605110001L, "Initial schema: Users, Stores, Products, PriceEntries, ShoppingLists, ShoppingListItems, GamificationEvents")>]
type M001_InitialSchema() =
    inherit Migration()

    override this.Up() =
        this.Execute.Sql(
            """
            CREATE TABLE Users (
                Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                Email VARCHAR(255) UNIQUE NOT NULL,
                PasswordHash VARCHAR(255) NOT NULL,
                Username VARCHAR(50) UNIQUE NOT NULL,
                GamificationPoints INT NOT NULL DEFAULT 0,
                CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE Stores (
                Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                Name VARCHAR(255) NOT NULL,
                Address VARCHAR(255) NOT NULL,
                Latitude DECIMAL(10, 8) NOT NULL,
                Longitude DECIMAL(11, 8) NOT NULL,
                Type VARCHAR(50),
                CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE Products (
                Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                Name VARCHAR(255) UNIQUE NOT NULL,
                Category VARCHAR(100),
                Barcode VARCHAR(50) UNIQUE
            );

            CREATE TABLE PriceEntries (
                Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                ProductId UUID NOT NULL REFERENCES Products(Id),
                StoreId UUID NOT NULL REFERENCES Stores(Id),
                UserId UUID NOT NULL REFERENCES Users(Id),
                Price DECIMAL(10, 2) NOT NULL,
                Unit VARCHAR(20),
                IsOnSale BOOLEAN NOT NULL DEFAULT FALSE,
                SaleDescription TEXT,
                ImageUrl VARCHAR(255),
                Verified BOOLEAN NOT NULL DEFAULT FALSE,
                CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            -- Expression-based UNIQUE must be an index, not a table CONSTRAINT
            CREATE UNIQUE INDEX UX_PriceEntry_PerDay
                ON PriceEntries (ProductId, StoreId, ((CreatedAt AT TIME ZONE 'UTC')::date));

            CREATE TABLE ShoppingLists (
                Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                UserId UUID NOT NULL REFERENCES Users(Id),
                Name VARCHAR(255) NOT NULL,
                CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE ShoppingListItems (
                Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                ShoppingListId UUID NOT NULL REFERENCES ShoppingLists(Id) ON DELETE CASCADE,
                ProductId UUID NOT NULL REFERENCES Products(Id),
                Quantity DECIMAL(10, 2) NOT NULL,
                IsBought BOOLEAN NOT NULL DEFAULT FALSE,
                CONSTRAINT UQ_ShoppingListItem UNIQUE (ShoppingListId, ProductId)
            );

            CREATE TABLE GamificationEvents (
                Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                UserId UUID NOT NULL REFERENCES Users(Id),
                EventType VARCHAR(50) NOT NULL,
                PointsAwarded INT NOT NULL,
                CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """
        )

    override this.Down() =
        this.Execute.Sql(
            """
            DROP TABLE IF EXISTS GamificationEvents;
            DROP TABLE IF EXISTS ShoppingListItems;
            DROP TABLE IF EXISTS ShoppingLists;
            DROP INDEX IF EXISTS UX_PriceEntry_PerDay;
            DROP TABLE IF EXISTS PriceEntries;
            DROP TABLE IF EXISTS Products;
            DROP TABLE IF EXISTS Stores;
            DROP TABLE IF EXISTS Users;
            """
        )
