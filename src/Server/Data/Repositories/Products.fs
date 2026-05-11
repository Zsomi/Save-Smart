namespace SmartSave.Data.Repositories

open System
open System.Threading.Tasks
open Dapper
open SmartSave.Shared.Domain
open SmartSave.Data

module Products =

    [<CLIMutable>]
    type private ProductRow =
        { Id: Guid
          Name: string
          Category: string
          Barcode: string }

    let private toDomain (row: ProductRow) : Product =
        { Id = ProductId row.Id
          Name = row.Name
          Category = row.Category |> Option.ofObj
          Barcode = row.Barcode |> Option.ofObj }

    let private selectColumns = "id, name, category, barcode"

    let insert
        (factory: IDbConnectionFactory)
        (name: string)
        (category: string option)
        (barcode: string option)
        : Task<Product> =
        task {
            use conn = factory.Create()
            let sql =
                $"""
                INSERT INTO products (name, category, barcode)
                VALUES (@Name, @Category, @Barcode)
                RETURNING {selectColumns}
                """
            let! row =
                conn.QuerySingleAsync<ProductRow>(
                    sql,
                    {| Name = name
                       Category = Option.toObj category
                       Barcode = Option.toObj barcode |}
                )
            return toDomain row
        }

    let findById (factory: IDbConnectionFactory) (ProductId id) : Task<Product option> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM products WHERE id = @Id"
            let! rows = conn.QueryAsync<ProductRow>(sql, {| Id = id |})
            return rows |> Seq.tryHead |> Option.map toDomain
        }

    let findByBarcode (factory: IDbConnectionFactory) (barcode: string) : Task<Product option> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM products WHERE barcode = @Barcode"
            let! rows = conn.QueryAsync<ProductRow>(sql, {| Barcode = barcode |})
            return rows |> Seq.tryHead |> Option.map toDomain
        }

    let search (factory: IDbConnectionFactory) (query: string) : Task<Product list> =
        task {
            use conn = factory.Create()
            let sql =
                $"SELECT {selectColumns} FROM products WHERE name ILIKE @Pattern ORDER BY name LIMIT 50"
            let! rows = conn.QueryAsync<ProductRow>(sql, {| Pattern = $"%%{query}%%" |})
            return rows |> Seq.map toDomain |> Seq.toList
        }

    let list (factory: IDbConnectionFactory) : Task<Product list> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM products ORDER BY name"
            let! rows = conn.QueryAsync<ProductRow>(sql)
            return rows |> Seq.map toDomain |> Seq.toList
        }
