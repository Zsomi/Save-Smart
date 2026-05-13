namespace SmartSave

open System
open WebSharper
open SmartSave.Shared.Domain
open SmartSave.Shared.Auth
open SmartSave.Shared.ApiTypes
open SmartSave.Data
open SmartSave.Data.Repositories

module Server =

    let private currentUser () =
        async {
            let ctx = WebSharper.Web.Remoting.GetContext()
            let! loggedIn = ctx.UserSession.GetLoggedInUser()
            match loggedIn with
            | None -> return Error Unauthenticated
            | Some sid ->
                match Guid.TryParse sid with
                | false, _ ->
                    do! ctx.UserSession.Logout()
                    return Error Unauthenticated
                | true, guid ->
                    let factory = ServerServices.dbFactory ()
                    let! user =
                        Users.findById factory (UserId guid)
                        |> Async.AwaitTask
                    match user with
                    | Some u -> return Ok u
                    | None ->
                        do! ctx.UserSession.Logout()
                        return Error Unauthenticated
        }

    let private requireUser () = currentUser ()

    let private withUser (work: User -> Async<Result<'a, ApiError>>) =
        async {
            match! requireUser () with
            | Error e -> return Error e
            | Ok user -> return! work user
        }

    let private awardPoints (user: User) (eventType: GamificationEventType) (points: int) =
        async {
            let factory = ServerServices.dbFactory ()
            let! _ =
                GamificationEvents.insert factory user.Id eventType points
                |> Async.AwaitTask
            let! total =
                Users.addPoints factory user.Id points
                |> Async.AwaitTask
            let notifs = ServerServices.notifications ()
            do!
                notifs.PointsAwarded
                    { UserId = user.Id
                      EventType = eventType
                      Points = points
                      TotalPoints = total }
                |> Async.AwaitTask
            return ()
        }

    let private awardDailyLoginBonus (user: User) =
        async {
            let factory = ServerServices.dbFactory ()
            let! already =
                GamificationEvents.hasEventToday factory user.Id LoginBonus
                |> Async.AwaitTask
            if not already then
                do! awardPoints user LoginBonus 5
        }

    [<Rpc>]
    let Register (req: RegisterRequest) : Async<Result<User, AuthError>> =
        async {
            let ctx = WebSharper.Web.Remoting.GetContext()
            let factory = ServerServices.dbFactory ()
            let! result =
                SmartSave.Auth.Service.register factory req
                |> Async.AwaitTask
            match result with
            | Ok user ->
                let (UserId uid) = user.Id
                do! ctx.UserSession.LoginUser(uid.ToString(), persistent = true)
                do! awardDailyLoginBonus user
                return Ok user
            | Error e -> return Error e
        }

    [<Rpc>]
    let Login (req: LoginRequest) : Async<Result<User, AuthError>> =
        async {
            let ctx = WebSharper.Web.Remoting.GetContext()
            let factory = ServerServices.dbFactory ()
            let! result =
                SmartSave.Auth.Service.login factory req
                |> Async.AwaitTask
            match result with
            | Ok user ->
                let (UserId uid) = user.Id
                do! ctx.UserSession.LoginUser(uid.ToString(), persistent = true)
                do! awardDailyLoginBonus user
                return Ok user
            | Error e -> return Error e
        }

    [<Rpc>]
    let Logout () : Async<unit> =
        async {
            let ctx = WebSharper.Web.Remoting.GetContext()
            do! ctx.UserSession.Logout()
        }

    [<Rpc>]
    let Me () : Async<Result<User, AuthError>> =
        async {
            let ctx = WebSharper.Web.Remoting.GetContext()
            let! loggedIn = ctx.UserSession.GetLoggedInUser()
            match loggedIn with
            | None -> return Error NotAuthenticated
            | Some sid ->
                match Guid.TryParse sid with
                | false, _ ->
                    do! ctx.UserSession.Logout()
                    return Error NotAuthenticated
                | true, guid ->
                    let factory = ServerServices.dbFactory ()
                    let! user =
                        Users.findById factory (UserId guid)
                        |> Async.AwaitTask
                    match user with
                    | Some u -> return Ok u
                    | None ->
                        do! ctx.UserSession.Logout()
                        return Error NotAuthenticated
        }

    [<Rpc>]
    let StoresNearby
        (latitude: float)
        (longitude: float)
        (radiusKm: float)
        : Async<Store list> =
        async {
            let factory = ServerServices.dbFactory ()
            return!
                Stores.findNearby factory latitude longitude radiusKm
                |> Async.AwaitTask
        }

    [<Rpc>]
    let StoreById (id: StoreId) : Async<Result<Store, ApiError>> =
        async {
            let factory = ServerServices.dbFactory ()
            let! store = Stores.findById factory id |> Async.AwaitTask
            match store with
            | Some s -> return Ok s
            | None -> return Error (NotFound "store")
        }

    [<Rpc>]
    let StorePrices (id: StoreId) : Async<PriceEntry list> =
        async {
            let factory = ServerServices.dbFactory ()
            return! PriceEntries.listForStore factory id |> Async.AwaitTask
        }

    let private validateStoreFields
        (name: string)
        (address: string)
        (latitude: float)
        (longitude: float)
        : Result<unit, ApiError> =
        if String.IsNullOrWhiteSpace name then
            Error (ValidationFailed "Store name is required.")
        elif String.IsNullOrWhiteSpace address then
            Error (ValidationFailed "Store address is required.")
        elif latitude < -90.0 || latitude > 90.0 then
            Error (ValidationFailed "Latitude must be between -90 and 90.")
        elif longitude < -180.0 || longitude > 180.0 then
            Error (ValidationFailed "Longitude must be between -180 and 180.")
        else
            Ok ()

    let private canMutateStore (user: User) (store: Store) =
        user.IsAdmin
        || (match store.OwnerUserId with
            | Some owner -> owner = user.Id
            | None -> false)

    [<Rpc>]
    let StoreCreate (req: CreateStoreRequest) : Async<Result<Store, ApiError>> =
        withUser (fun user ->
            async {
                match validateStoreFields req.Name req.Address req.Latitude req.Longitude with
                | Error e -> return Error e
                | Ok () ->
                    let factory = ServerServices.dbFactory ()
                    let! created =
                        Stores.insert
                            factory
                            (req.Name.Trim())
                            (req.Address.Trim())
                            req.Latitude
                            req.Longitude
                            req.Type
                            (Some user.Id)
                        |> Async.AwaitTask
                    return Ok created
            })

    [<Rpc>]
    let StoreUpdate (req: UpdateStoreRequest) : Async<Result<Store, ApiError>> =
        withUser (fun user ->
            async {
                match validateStoreFields req.Name req.Address req.Latitude req.Longitude with
                | Error e -> return Error e
                | Ok () ->
                    let factory = ServerServices.dbFactory ()
                    let! existing = Stores.findById factory req.Id |> Async.AwaitTask
                    match existing with
                    | None -> return Error (NotFound "store")
                    | Some store when not (canMutateStore user store) ->
                        return Error (ValidationFailed "You do not have permission to modify this store.")
                    | Some _ ->
                        let! updated =
                            Stores.update
                                factory
                                req.Id
                                (req.Name.Trim())
                                (req.Address.Trim())
                                req.Latitude
                                req.Longitude
                                req.Type
                            |> Async.AwaitTask
                        match updated with
                        | Some s -> return Ok s
                        | None -> return Error (NotFound "store")
            })

    [<Rpc>]
    let StoreDelete (id: StoreId) : Async<Result<unit, ApiError>> =
        withUser (fun user ->
            async {
                let factory = ServerServices.dbFactory ()
                let! existing = Stores.findById factory id |> Async.AwaitTask
                match existing with
                | None -> return Error (NotFound "store")
                | Some store when not (canMutateStore user store) ->
                    return Error (ValidationFailed "You do not have permission to delete this store.")
                | Some _ ->
                    try
                        let! _ = Stores.delete factory id |> Async.AwaitTask
                        return Ok ()
                    with
                    | :? Npgsql.PostgresException as ex when ex.SqlState = "23503" ->
                        return Error (Conflict "Cannot delete a store that still has price entries.")
            })

    [<Rpc>]
    let ProductSearch (query: string) : Async<Product list> =
        async {
            let factory = ServerServices.dbFactory ()
            return! Products.search factory query |> Async.AwaitTask
        }

    [<Rpc>]
    let ProductById (id: ProductId) : Async<Result<Product, ApiError>> =
        async {
            let factory = ServerServices.dbFactory ()
            let! product = Products.findById factory id |> Async.AwaitTask
            match product with
            | Some p -> return Ok p
            | None -> return Error (NotFound "product")
        }

    [<Rpc>]
    let ProductByBarcode (barcode: string) : Async<Result<Product, ApiError>> =
        async {
            let factory = ServerServices.dbFactory ()
            let! product = Products.findByBarcode factory barcode |> Async.AwaitTask
            match product with
            | Some p -> return Ok p
            | None -> return Error (NotFound "product")
        }

    [<Rpc>]
    let ProductCreate (req: CreateProductRequest) : Async<Result<Product, ApiError>> =
        withUser (fun _ ->
            async {
                if String.IsNullOrWhiteSpace req.Name then
                    return Error (ValidationFailed "Product name is required.")
                else
                    let factory = ServerServices.dbFactory ()
                    try
                        let! created =
                            Products.insert
                                factory
                                (req.Name.Trim())
                                req.Category
                                req.Barcode
                            |> Async.AwaitTask
                        return Ok created
                    with
                    | :? Npgsql.PostgresException as ex when ex.SqlState = "23505" ->
                        return Error (Conflict "A product with this name or barcode already exists.")
            })

    [<Rpc>]
    let PriceEntrySubmit
        (req: SubmitPriceEntryRequest)
        : Async<Result<PriceEntry, ApiError>> =
        withUser (fun user ->
            async {
                if req.Price <= 0.0 then
                    return Error (ValidationFailed "Price must be greater than zero.")
                else
                    let factory = ServerServices.dbFactory ()
                    let insertParams : PriceEntries.InsertParams =
                        { ProductId = req.ProductId
                          StoreId = req.StoreId
                          UserId = user.Id
                          Price = req.Price
                          Unit = req.Unit
                          IsOnSale = req.IsOnSale
                          SaleDescription = req.SaleDescription
                          ImageUrl = req.ImageUrl }
                    try
                        let! entry =
                            PriceEntries.insert factory insertParams
                            |> Async.AwaitTask
                        let basePoints = if req.IsOnSale then 15 else 10
                        let photoBonus =
                            match req.ImageUrl with
                            | Some url when not (String.IsNullOrWhiteSpace url) -> 5
                            | _ -> 0
                        do! awardPoints user PriceReport (basePoints + photoBonus)
                        let! product = Products.findById factory req.ProductId |> Async.AwaitTask
                        let productName =
                            product |> Option.map (fun p -> p.Name) |> Option.defaultValue ""
                        let notifs = ServerServices.notifications ()
                        do!
                            notifs.PriceReported
                                { Entry = entry
                                  ProductName = productName
                                  StoreId = req.StoreId }
                            |> Async.AwaitTask
                        return Ok entry
                    with
                    | :? Npgsql.PostgresException as ex when ex.SqlState = "23505" ->
                        return Error (Conflict "A price for this product at this store has already been reported today.")
                    | :? Npgsql.PostgresException as ex when ex.SqlState = "23503" ->
                        return Error (NotFound "product or store")
            })

    [<Rpc>]
    let PriceEntryVerify
        (id: PriceEntryId)
        : Async<Result<PriceEntry, ApiError>> =
        withUser (fun user ->
            async {
                let factory = ServerServices.dbFactory ()
                let! existing = PriceEntries.findById factory id |> Async.AwaitTask
                match existing with
                | None -> return Error (NotFound "price entry")
                | Some entry when entry.Verified ->
                    return Ok entry
                | Some entry ->
                    let! _ = PriceEntries.setVerified factory id true |> Async.AwaitTask
                    let reporterId = entry.UserId
                    let! reporter = Users.findById factory reporterId |> Async.AwaitTask
                    match reporter with
                    | Some r when r.Id <> user.Id ->
                        do! awardPoints r Verification 5
                        do! awardPoints user Verification 2
                    | _ ->
                        do! awardPoints user Verification 2
                    let notifs = ServerServices.notifications ()
                    do!
                        notifs.PriceVerified
                            { EntryId = id
                              StoreId = entry.StoreId
                              Verified = true }
                        |> Async.AwaitTask
                    let! refreshed = PriceEntries.findById factory id |> Async.AwaitTask
                    return
                        match refreshed with
                        | Some e -> Ok e
                        | None -> Error (NotFound "price entry")
            })

    let private toShoppingListView
        (factory: IDbConnectionFactory)
        (list: ShoppingList)
        : Async<ShoppingListView> =
        async {
            let! items = ShoppingListItems.listForList factory list.Id |> Async.AwaitTask
            let! products = Products.list factory |> Async.AwaitTask
            let productMap =
                products |> List.map (fun p -> p.Id, p) |> Map.ofList
            let views =
                items
                |> List.choose (fun item ->
                    Map.tryFind item.ProductId productMap
                    |> Option.map (fun product ->
                        { Item = item; Product = product }))
            return { List = list; Items = views }
        }

    [<Rpc>]
    let ShoppingListCreate
        (req: CreateShoppingListRequest)
        : Async<Result<ShoppingList, ApiError>> =
        withUser (fun user ->
            async {
                if String.IsNullOrWhiteSpace req.Name then
                    return Error (ValidationFailed "List name is required.")
                else
                    let factory = ServerServices.dbFactory ()
                    let! created =
                        ShoppingLists.insert factory user.Id (req.Name.Trim())
                        |> Async.AwaitTask
                    return Ok created
            })

    [<Rpc>]
    let ShoppingListsMine () : Async<Result<ShoppingList list, ApiError>> =
        withUser (fun user ->
            async {
                let factory = ServerServices.dbFactory ()
                let! lists = ShoppingLists.listForUser factory user.Id |> Async.AwaitTask
                return Ok lists
            })

    [<Rpc>]
    let ShoppingListGet
        (id: ShoppingListId)
        : Async<Result<ShoppingListView, ApiError>> =
        withUser (fun user ->
            async {
                let factory = ServerServices.dbFactory ()
                let! list = ShoppingLists.findById factory id |> Async.AwaitTask
                match list with
                | None -> return Error (NotFound "shopping list")
                | Some l when l.UserId <> user.Id ->
                    return Error (NotFound "shopping list")
                | Some l ->
                    let! view = toShoppingListView factory l
                    return Ok view
            })

    [<Rpc>]
    let ShoppingListAddItem
        (req: AddShoppingListItemRequest)
        : Async<Result<ShoppingListItem, ApiError>> =
        withUser (fun user ->
            async {
                if req.Quantity <= 0.0 then
                    return Error (ValidationFailed "Quantity must be greater than zero.")
                else
                    let factory = ServerServices.dbFactory ()
                    let! list = ShoppingLists.findById factory req.ListId |> Async.AwaitTask
                    match list with
                    | None -> return Error (NotFound "shopping list")
                    | Some l when l.UserId <> user.Id ->
                        return Error (NotFound "shopping list")
                    | Some _ ->
                        try
                            let! item =
                                ShoppingListItems.insert
                                    factory
                                    req.ListId
                                    req.ProductId
                                    req.Quantity
                                |> Async.AwaitTask
                            return Ok item
                        with
                        | :? Npgsql.PostgresException as ex when ex.SqlState = "23505" ->
                            return Error (Conflict "This product is already on the list.")
                        | :? Npgsql.PostgresException as ex when ex.SqlState = "23503" ->
                            return Error (NotFound "product")
            })

    [<Rpc>]
    let ShoppingListToggleBought
        (itemId: ShoppingListItemId)
        (bought: bool)
        : Async<Result<unit, ApiError>> =
        withUser (fun _ ->
            async {
                let factory = ServerServices.dbFactory ()
                let! affected =
                    ShoppingListItems.setBought factory itemId bought
                    |> Async.AwaitTask
                if affected = 0 then return Error (NotFound "shopping list item")
                else return Ok ()
            })

    [<Rpc>]
    let ShoppingListRemoveItem
        (itemId: ShoppingListItemId)
        : Async<Result<unit, ApiError>> =
        withUser (fun _ ->
            async {
                let factory = ServerServices.dbFactory ()
                let! affected = ShoppingListItems.delete factory itemId |> Async.AwaitTask
                if affected = 0 then return Error (NotFound "shopping list item")
                else return Ok ()
            })

    [<Rpc>]
    let ShoppingListDelete
        (id: ShoppingListId)
        : Async<Result<unit, ApiError>> =
        withUser (fun user ->
            async {
                let factory = ServerServices.dbFactory ()
                let! list = ShoppingLists.findById factory id |> Async.AwaitTask
                match list with
                | None -> return Error (NotFound "shopping list")
                | Some l when l.UserId <> user.Id ->
                    return Error (NotFound "shopping list")
                | Some _ ->
                    let! _ = ShoppingLists.delete factory id |> Async.AwaitTask
                    return Ok ()
            })

    [<Rpc>]
    let ShoppingListOptimize
        (req: OptimizerRequest)
        : Async<Result<OptimizerResult, ApiError>> =
        withUser (fun user ->
            async {
                let factory = ServerServices.dbFactory ()
                let! list = ShoppingLists.findById factory req.ListId |> Async.AwaitTask
                match list with
                | None -> return Error (NotFound "shopping list")
                | Some l when l.UserId <> user.Id ->
                    return Error (NotFound "shopping list")
                | Some _ ->
                    return! Optimizer.optimize factory req |> Async.AwaitTask
            })

    [<Rpc>]
    let GamificationLeaderboard (limit: int) : Async<LeaderboardEntry list> =
        async {
            let factory = ServerServices.dbFactory ()
            let safeLimit = max 1 (min limit 100)
            let! rows = GamificationEvents.leaderboard factory safeLimit |> Async.AwaitTask
            return
                rows
                |> List.map (fun (uid, name, pts) ->
                    { UserId = uid; Username = name; Points = pts })
        }

    [<Rpc>]
    let GamificationMyEvents
        (limit: int)
        : Async<Result<GamificationEvent list, ApiError>> =
        withUser (fun user ->
            async {
                let factory = ServerServices.dbFactory ()
                let safeLimit = max 1 (min limit 200)
                let! events =
                    GamificationEvents.listForUser factory user.Id safeLimit
                    |> Async.AwaitTask
                return Ok events
            })
