open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open WebSharper.AspNetCore
open SmartSave
open SmartSave.Data
open SmartSave.Hubs

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddWebSharper()
        .AddAuthentication("WebSharper")
        .AddCookie("WebSharper", fun options ->
            options.Cookie.Name <- "SmartSave.Auth"
            options.Cookie.HttpOnly <- true
            options.Cookie.SameSite <- SameSiteMode.Lax
            options.SlidingExpiration <- true
            options.ExpireTimeSpan <- TimeSpan.FromDays(14.0)
            options.LoginPath <- PathString("/login")
            options.LogoutPath <- PathString("/logout"))
    |> ignore

    builder.Services.AddAuthorization() |> ignore

    builder.Services.AddSignalR() |> ignore
    builder.Services.AddSingleton<IUserIdProvider, NameClaimUserIdProvider>() |> ignore
    builder.Services.AddSingleton<INotifications, SignalRNotifications>() |> ignore

    Database.addPostgres builder.Configuration builder.Services |> ignore
    Database.addMigrations builder.Configuration builder.Services |> ignore

    let app = builder.Build()

    ServerServices.init app.Services

    Database.runMigrations app.Services

    if args |> Array.contains "--smoke-test" then
        let factory = app.Services.GetRequiredService<IDbConnectionFactory>()
        exit ((Smoke.run factory).GetAwaiter().GetResult())

    if not (app.Environment.IsDevelopment()) then
        app.UseExceptionHandler("/Error")
            .UseHsts()
        |> ignore

    app.UseHttpsRedirection()
#if DEBUG        
        .UseWebSharperScriptRedirect(startVite = true)
#endif
        .UseAuthentication()
        .UseAuthorization()
        .UseStaticFiles()
        .UseWebSharper(fun ws -> ws.Sitelet(Site.Main) |> ignore)
    |> ignore

    app.MapHub<NotificationsHub>("/hubs/notifications") |> ignore

    app.Run()

    0 // Exit code
