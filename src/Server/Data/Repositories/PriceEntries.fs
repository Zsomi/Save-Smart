namespace SmartSave.Data.Repositories

open System
open System.Threading.Tasks
open Dapper
open SmartSave.Shared.Domain
open SmartSave.Data

module PriceEntries =

    [<CLIMutable>]
    type private PriceEntryRow =
        { Id: Guid
          ProductId: Guid
          StoreId: Guid
          UserId: Guid
          Price: float
          Unit: string
          IsOnSale: bool
          SaleDescription: string
          ImageUrl: string
          Verified: bool
          CreatedAt: DateTimeOffset }

    let private toDomain (row: PriceEntryRow) : PriceEntry =
        { Id = PriceEntryId row.Id
          ProductId = ProductId row.ProductId
          StoreId = StoreId row.StoreId
          UserId = UserId row.UserId
          Price = row.Price
          Unit = row.Unit |> Option.ofObj
          IsOnSale = row.IsOnSale
          SaleDescription = row.SaleDescription |> Option.ofObj
          ImageUrl = row.ImageUrl |> Option.ofObj
          Verified = row.Verified
          CreatedAt = row.CreatedAt }

    let private selectColumns =
        "id, productid, storeid, userid, price, unit, isonsale, saledescription, imageurl, verified, createdat"

    type InsertParams =
        { ProductId: ProductId
          StoreId: StoreId
          UserId: UserId
          Price: float
          Unit: string option
          IsOnSale: bool
          SaleDescription: string option
          ImageUrl: string option }

    let insert (factory: IDbConnectionFactory) (p: InsertParams) : Task<PriceEntry> =
        task {
            use conn = factory.Create()
            let sql =
                $"""
                INSERT INTO priceentries
                    (productid, storeid, userid, price, unit, isonsale, saledescription, imageurl)
                VALUES
                    (@ProductId, @StoreId, @UserId, @Price, @Unit, @IsOnSale, @SaleDescription, @ImageUrl)
                RETURNING {selectColumns}
                """
            let (ProductId pid) = p.ProductId
            let (StoreId sid) = p.StoreId
            let (UserId uid) = p.UserId
            let! row =
                conn.QuerySingleAsync<PriceEntryRow>(
                    sql,
                    {| ProductId = pid
                       StoreId = sid
                       UserId = uid
                       Price = p.Price
                       Unit = Option.toObj p.Unit
                       IsOnSale = p.IsOnSale
                       SaleDescription = Option.toObj p.SaleDescription
                       ImageUrl = Option.toObj p.ImageUrl |}
                )
            return toDomain row
        }

    let findById (factory: IDbConnectionFactory) (PriceEntryId id) : Task<PriceEntry option> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM priceentries WHERE id = @Id"
            let! rows = conn.QueryAsync<PriceEntryRow>(sql, {| Id = id |})
            return rows |> Seq.tryHead |> Option.map toDomain
        }

    let listForStore (factory: IDbConnectionFactory) (StoreId id) : Task<PriceEntry list> =
        task {
            use conn = factory.Create()
            let sql =
                $"SELECT {selectColumns} FROM priceentries WHERE storeid = @Id ORDER BY createdat DESC"
            let! rows = conn.QueryAsync<PriceEntryRow>(sql, {| Id = id |})
            return rows |> Seq.map toDomain |> Seq.toList
        }

    let listForProduct (factory: IDbConnectionFactory) (ProductId id) : Task<PriceEntry list> =
        task {
            use conn = factory.Create()
            let sql =
                $"SELECT {selectColumns} FROM priceentries WHERE productid = @Id ORDER BY createdat DESC"
            let! rows = conn.QueryAsync<PriceEntryRow>(sql, {| Id = id |})
            return rows |> Seq.map toDomain |> Seq.toList
        }

    let setVerified
        (factory: IDbConnectionFactory)
        (PriceEntryId id)
        (verified: bool)
        : Task<int> =
        task {
            use conn = factory.Create()
            let sql = "UPDATE priceentries SET verified = @Verified WHERE id = @Id"
            return! conn.ExecuteAsync(sql, {| Id = id; Verified = verified |})
        }
