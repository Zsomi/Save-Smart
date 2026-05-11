namespace SmartSave.Data.Repositories

open System
open System.Threading.Tasks
open Dapper
open SmartSave.Shared.Domain
open SmartSave.Data

module Stores =

    [<CLIMutable>]
    type private StoreRow =
        { Id: Guid
          Name: string
          Address: string
          Latitude: decimal
          Longitude: decimal
          Type: string
          CreatedAt: DateTimeOffset }

    let private toDomain (row: StoreRow) : Store =
        { Id = StoreId row.Id
          Name = row.Name
          Address = row.Address
          Latitude = row.Latitude
          Longitude = row.Longitude
          Type = row.Type |> Option.ofObj |> Option.map StoreType.parse
          CreatedAt = row.CreatedAt }

    let private selectColumns =
        "id, name, address, latitude, longitude, type, createdat"

    let insert
        (factory: IDbConnectionFactory)
        (name: string)
        (address: string)
        (latitude: decimal)
        (longitude: decimal)
        (storeType: StoreType option)
        : Task<Store> =
        task {
            use conn = factory.Create()
            let sql =
                $"""
                INSERT INTO stores (name, address, latitude, longitude, type)
                VALUES (@Name, @Address, @Latitude, @Longitude, @Type)
                RETURNING {selectColumns}
                """
            let! row =
                conn.QuerySingleAsync<StoreRow>(
                    sql,
                    {| Name = name
                       Address = address
                       Latitude = latitude
                       Longitude = longitude
                       Type =
                           storeType
                           |> Option.map StoreType.toString
                           |> Option.toObj |}
                )
            return toDomain row
        }

    let findById (factory: IDbConnectionFactory) (StoreId id) : Task<Store option> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM stores WHERE id = @Id"
            let! rows = conn.QueryAsync<StoreRow>(sql, {| Id = id |})
            return rows |> Seq.tryHead |> Option.map toDomain
        }

    let list (factory: IDbConnectionFactory) : Task<Store list> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM stores ORDER BY name"
            let! rows = conn.QueryAsync<StoreRow>(sql)
            return rows |> Seq.map toDomain |> Seq.toList
        }

    /// Bounding-box prefilter using lat/lon degrees, then exact great-circle ranking.
    /// Approximation good enough for "stores within N km" — replace with PostGIS if precision matters.
    let findNearby
        (factory: IDbConnectionFactory)
        (latitude: decimal)
        (longitude: decimal)
        (radiusKm: decimal)
        : Task<Store list> =
        task {
            use conn = factory.Create()
            let latDelta = radiusKm / 111.0m
            let lonDelta = radiusKm / 70.0m
            let sql =
                $"""
                SELECT {selectColumns} FROM stores
                WHERE latitude BETWEEN @MinLat AND @MaxLat
                  AND longitude BETWEEN @MinLon AND @MaxLon
                ORDER BY (latitude - @Latitude) * (latitude - @Latitude)
                       + (longitude - @Longitude) * (longitude - @Longitude)
                """
            let! rows =
                conn.QueryAsync<StoreRow>(
                    sql,
                    {| MinLat = latitude - latDelta
                       MaxLat = latitude + latDelta
                       MinLon = longitude - lonDelta
                       MaxLon = longitude + lonDelta
                       Latitude = latitude
                       Longitude = longitude |}
                )
            return rows |> Seq.map toDomain |> Seq.toList
        }
