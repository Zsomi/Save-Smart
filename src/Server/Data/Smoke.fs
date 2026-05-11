namespace SmartSave.Data

open System
open System.Threading.Tasks
open Dapper
open SmartSave.Shared.Domain
open SmartSave.Data.Repositories

module Smoke =

    let private cleanup (factory: IDbConnectionFactory) =
        task {
            use conn = factory.Create()
            let! _ =
                conn.ExecuteAsync(
                    "TRUNCATE priceentries, shoppinglistitems, shoppinglists, gamificationevents, products, stores, users RESTART IDENTITY CASCADE"
                )
            return ()
        }

    let run (factory: IDbConnectionFactory) : Task<int> =
        task {
            let suffix = Guid.NewGuid().ToString("N").Substring(0, 8)
            printfn "=== Smoke test (run %s) ===" suffix

            let! store =
                Stores.insert factory
                    $"TestBolt-{suffix}"
                    $"Cím-{suffix}"
                    47.4979m
                    19.0402m
                    (Some Supermarket)
            printfn "store inserted: %A" store

            let! nearby = Stores.findNearby factory 47.5m 19.0m 50m
            printfn "nearby stores (50km of 47.5/19.0): %d" nearby.Length

            let! user =
                Users.insert factory
                    $"test-{suffix}@example.com"
                    "hash-placeholder"
                    $"user-{suffix}"
            printfn "user inserted: %A" user

            let! product =
                Products.insert factory
                    $"TestTermek-{suffix}"
                    (Some "Tejtermék")
                    None
            printfn "product inserted: %A" product

            let! priceEntry =
                PriceEntries.insert factory
                    { ProductId = product.Id
                      StoreId = store.Id
                      UserId = user.Id
                      Price = 499.99m
                      Unit = Some "kg"
                      IsOnSale = true
                      SaleDescription = Some "2 db vásárlása esetén"
                      ImageUrl = None }
            printfn "price entry inserted: %A" priceEntry

            let! foundByEmail = Users.findByEmail factory user.Email
            printfn "findByEmail roundtrip: %A" foundByEmail

            let! foundWithHash = Users.findByEmailWithHash factory user.Email
            printfn "findByEmailWithHash roundtrip: %A" foundWithHash

            let! pricesForStore = PriceEntries.listForStore factory store.Id
            printfn "prices for store: %d" pricesForStore.Length

            let! newPoints = Users.addPoints factory user.Id 10
            printfn "addPoints: %d" newPoints

            let! searched = Products.search factory "TestTermek"
            printfn "product search hits: %d" searched.Length

            do! cleanup factory
            printfn "cleanup: tables truncated"

            printfn "=== OK ==="
            return 0
        }
