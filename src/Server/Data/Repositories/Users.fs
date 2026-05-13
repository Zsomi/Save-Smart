namespace SmartSave.Data.Repositories

open System
open System.Threading.Tasks
open Dapper
open SmartSave.Shared.Domain
open SmartSave.Data

module Users =

    [<CLIMutable>]
    type private UserRow =
        { Id: Guid
          Email: string
          PasswordHash: string
          Username: string
          GamificationPoints: int
          IsAdmin: bool
          CreatedAt: DateTimeOffset }

    let private toDomain (row: UserRow) : User =
        { Id = UserId row.Id
          Email = row.Email
          Username = row.Username
          GamificationPoints = row.GamificationPoints
          IsAdmin = row.IsAdmin
          CreatedAt = row.CreatedAt }

    let private selectColumns =
        "id, email, passwordhash, username, gamificationpoints, isadmin, createdat"

    let insert
        (factory: IDbConnectionFactory)
        (email: string)
        (passwordHash: string)
        (username: string)
        : Task<User> =
        task {
            use conn = factory.Create()
            let sql =
                $"""
                INSERT INTO users (email, passwordhash, username)
                VALUES (@Email, @PasswordHash, @Username)
                RETURNING {selectColumns}
                """
            let! row =
                conn.QuerySingleAsync<UserRow>(
                    sql,
                    {| Email = email
                       PasswordHash = passwordHash
                       Username = username |}
                )
            return toDomain row
        }

    let findById (factory: IDbConnectionFactory) (UserId id) : Task<User option> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM users WHERE id = @Id"
            let! rows = conn.QueryAsync<UserRow>(sql, {| Id = id |})
            return rows |> Seq.tryHead |> Option.map toDomain
        }

    let findByEmail (factory: IDbConnectionFactory) (email: string) : Task<User option> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM users WHERE email = @Email"
            let! rows = conn.QueryAsync<UserRow>(sql, {| Email = email |})
            return rows |> Seq.tryHead |> Option.map toDomain
        }

    let findByEmailWithHash
        (factory: IDbConnectionFactory)
        (email: string)
        : Task<(User * string) option> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM users WHERE email = @Email"
            let! rows = conn.QueryAsync<UserRow>(sql, {| Email = email |})
            return
                rows
                |> Seq.tryHead
                |> Option.map (fun row -> toDomain row, row.PasswordHash)
        }

    let addPoints (factory: IDbConnectionFactory) (UserId id) (delta: int) : Task<int> =
        task {
            use conn = factory.Create()
            let sql =
                """
                UPDATE users SET gamificationpoints = gamificationpoints + @Delta
                WHERE id = @Id
                RETURNING gamificationpoints
                """
            return! conn.QuerySingleAsync<int>(sql, {| Id = id; Delta = delta |})
        }

    let list (factory: IDbConnectionFactory) : Task<User list> =
        task {
            use conn = factory.Create()
            let sql = $"SELECT {selectColumns} FROM users ORDER BY createdat DESC"
            let! rows = conn.QueryAsync<UserRow>(sql)
            return rows |> Seq.map toDomain |> Seq.toList
        }
