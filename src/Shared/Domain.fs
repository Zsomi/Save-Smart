namespace SmartSave.Shared

open System
open WebSharper

[<JavaScript>]
module Domain =

    type UserId = UserId of Guid
    type StoreId = StoreId of Guid
    type ProductId = ProductId of Guid
    type PriceEntryId = PriceEntryId of Guid
    type ShoppingListId = ShoppingListId of Guid
    type ShoppingListItemId = ShoppingListItemId of Guid
    type GamificationEventId = GamificationEventId of Guid

    type User = {
        Id: UserId
        Email: string
        Username: string
        GamificationPoints: int
        CreatedAt: DateTimeOffset
    }

    type StoreType =
        | Supermarket
        | ConvenienceStore
        | Market
        | Other of string

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module StoreType =
        let toString =
            function
            | Supermarket -> "Supermarket"
            | ConvenienceStore -> "ConvenienceStore"
            | Market -> "Market"
            | Other s -> s

        let parse =
            function
            | "Supermarket" -> Supermarket
            | "ConvenienceStore" -> ConvenienceStore
            | "Market" -> Market
            | other -> Other other

    type Store = {
        Id: StoreId
        Name: string
        Address: string
        Latitude: decimal
        Longitude: decimal
        Type: StoreType option
        CreatedAt: DateTimeOffset
    }

    type Product = {
        Id: ProductId
        Name: string
        Category: string option
        Barcode: string option
    }

    type PriceEntry = {
        Id: PriceEntryId
        ProductId: ProductId
        StoreId: StoreId
        UserId: UserId
        Price: decimal
        Unit: string option
        IsOnSale: bool
        SaleDescription: string option
        ImageUrl: string option
        Verified: bool
        CreatedAt: DateTimeOffset
    }

    type ShoppingList = {
        Id: ShoppingListId
        UserId: UserId
        Name: string
        CreatedAt: DateTimeOffset
    }

    type ShoppingListItem = {
        Id: ShoppingListItemId
        ShoppingListId: ShoppingListId
        ProductId: ProductId
        Quantity: decimal
        IsBought: bool
    }

    type GamificationEventType =
        | PriceReport
        | Verification
        | LoginBonus
        | PhotoUpload
        | Other of string

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module GamificationEventType =
        let toString =
            function
            | PriceReport -> "PriceReport"
            | Verification -> "Verification"
            | LoginBonus -> "LoginBonus"
            | PhotoUpload -> "PhotoUpload"
            | GamificationEventType.Other s -> s

        let parse =
            function
            | "PriceReport" -> PriceReport
            | "Verification" -> Verification
            | "LoginBonus" -> LoginBonus
            | "PhotoUpload" -> PhotoUpload
            | other -> GamificationEventType.Other other

    type GamificationEvent = {
        Id: GamificationEventId
        UserId: UserId
        EventType: GamificationEventType
        PointsAwarded: int
        CreatedAt: DateTimeOffset
    }
