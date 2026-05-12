namespace SmartSave

open WebSharper
open WebSharper.UI
open WebSharper.UI.Templating

[<JavaScript>]
module Templates =

    type MainTemplate = Templating.Template<"Main.html", ClientLoad.FromDocument, ServerLoad.WhenChanged>

[<JavaScript>]
module Client =

    let Main () : Doc = Doc.Empty
