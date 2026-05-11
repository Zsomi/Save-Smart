namespace SmartSave.Data

open System.Data
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Npgsql
open FluentMigrator.Runner
open SmartSave.Data.Migrations

type IDbConnectionFactory =
    abstract member Create: unit -> IDbConnection

type NpgsqlConnectionFactory(connectionString: string) =
    interface IDbConnectionFactory with
        member _.Create() = new NpgsqlConnection(connectionString) :> IDbConnection

module Database =

    let private connectionString (cfg: IConfiguration) =
        match cfg.GetConnectionString("Postgres") with
        | null | "" -> failwith "Missing ConnectionStrings:Postgres in configuration"
        | s -> s

    let addPostgres (cfg: IConfiguration) (services: IServiceCollection) =
        let cs = connectionString cfg
        services.AddSingleton<IDbConnectionFactory>(NpgsqlConnectionFactory(cs))

    let addMigrations (cfg: IConfiguration) (services: IServiceCollection) =
        let cs = connectionString cfg
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(fun rb ->
                rb
                    .AddPostgres()
                    .WithGlobalConnectionString(cs)
                    .ScanIn(typeof<M001_InitialSchema>.Assembly).For.Migrations()
                |> ignore)
            .AddLogging(fun lb -> lb.AddFluentMigratorConsole() |> ignore)

    let runMigrations (services: System.IServiceProvider) =
        use scope = services.CreateScope()
        let runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>()
        runner.MigrateUp()
