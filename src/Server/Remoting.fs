namespace SmartSave

open System
open WebSharper
open SmartSave.Shared.Domain
open SmartSave.Shared.Auth

module Server =

    [<Rpc>]
    let DoSomething (input: string) =
        let R (s: string) = System.String(Array.rev (s.ToCharArray()))
        async { return R input }

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
            | Some userId ->
                match Guid.TryParse userId with
                | false, _ ->
                    do! ctx.UserSession.Logout()
                    return Error NotAuthenticated
                | true, guid ->
                    let factory = ServerServices.dbFactory ()
                    let! user =
                        SmartSave.Data.Repositories.Users.findById factory (UserId guid)
                        |> Async.AwaitTask
                    match user with
                    | Some u -> return Ok u
                    | None ->
                        do! ctx.UserSession.Logout()
                        return Error NotAuthenticated
        }
